import { AsyncPipe, DatePipe, SlicePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ReactiveFormsModule, FormControl, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatTooltipModule } from "@angular/material/tooltip";
import { map, catchError, of, combineLatest } from "rxjs";
import { AuthService } from "../../core/services/auth.service";
import { AgentService } from "../../core/services/agent.service";
import { InstallerInfo } from "../../core/models/installer-info.model";
import { HealthService } from "../../core/services/health.service";
import { ProfileService } from "../../core/services/profile.service";
import { SubscriptionService } from "../../core/services/subscription.service";
import { WalletService } from "../../core/services/wallet.service";
import { ActivatedRoute, Router } from "@angular/router";
import { environment } from "../../../environments/environment";
import { TelegramLinkComponent } from "./telegram-link.component";

@Component({
  selector: "app-profile-page",
  standalone: true,
  imports: [AsyncPipe, DatePipe, SlicePipe, ReactiveFormsModule, MatCardModule, MatIconModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTooltipModule, TelegramLinkComponent],
  templateUrl: "./profile-page.component.html",
  styleUrl: "./profile-page.component.scss"
})
export class ProfilePageComponent implements OnInit {
  private readonly _authService = inject(AuthService);
  private readonly _agentService = inject(AgentService);
  private readonly _healthService = inject(HealthService);
  private readonly _profileService = inject(ProfileService);
  private readonly _walletService = inject(WalletService);
  private readonly _subscriptionService = inject(SubscriptionService);
  private readonly _router = inject(Router);
  private readonly _route = inject(ActivatedRoute);

  public get user() {
    const current = this._authService.currentUser;
    return {
      displayName: current?.displayName ?? "Trader",
      email: current?.email ?? ""
    };
  }

  public readonly subscription$ = this._subscriptionService.status$;
  public readonly subscribing = signal(false);
  public readonly cancellingSubscription = signal(false);
  public readonly subscribeError = signal<string | null>(null);
  public readonly upgradePrompt = signal<string | null>(null);

  public readonly wallet$ = this._healthService.health$.pipe(
    map((h) => h ? { address: h.walletAddress, network: h.network } : null)
  );

  public readonly walletStatus$ = this._walletService.status$;
  public readonly profile$ = this._profileService.profile$;
  public readonly appVersion = environment.appVersion;

  public readonly installerInfo$ = this._agentService.getInstallerInfo().pipe(
    map((info) => ({ data: info, error: false })),
    catchError(() => of({ data: null as InstallerInfo | null, error: true }))
  );
  public readonly hasConnectedAgent$ = this._agentService.agents$.pipe(
    map((agents) => agents.some((a) => a.state !== "disconnected" && a.state !== "killed"))
  );
  public readonly sha256Copied = signal(false);
  public readonly exeDownloadUrl = this._agentService.getInstallerDownloadUrl("exe");
  public readonly zipDownloadUrl = this._agentService.getInstallerDownloadUrl("zip");

  public readonly networkControl = new FormControl("mainnet");
  public readonly networkSaving = signal(false);

  public readonly walletAddressControl = new FormControl("", [
    Validators.required,
    Validators.pattern(/^0x[0-9a-fA-F]{40}$/)
  ]);

  public readonly showKeyInput = signal(false);
  public readonly configuring = signal(false);
  public readonly configError = signal<string | null>(null);

  public ngOnInit(): void {
    this._walletService.refreshStatus();
    this._profileService.load();
    this._subscriptionService.loadStatus();
    this._agentService.refreshAgents();
    this.upgradePrompt.set(this.getUpgradePromptLabel(this._route.snapshot.queryParamMap.get("upgrade")));
    this.profile$.subscribe((profile) => {
      if (profile) {
        this.networkControl.setValue(profile.preferredNetwork, { emitEvent: false });
      }
    });
  }

  public onSubscribe(tier: "beginner" | "pro"): void {
    this.subscribing.set(true);
    this.subscribeError.set(null);

    this._subscriptionService.subscribe(tier).subscribe({
      next: () => {
        this.subscribing.set(false);
        this._profileService.load();
      },
      error: (err) => {
        this.subscribing.set(false);
        this.subscribeError.set(err.error?.errorMessage ?? "Failed to activate subscription.");
      }
    });
  }

  public onCancelSubscription(): void {
    this.cancellingSubscription.set(true);
    this.subscribeError.set(null);

    this._subscriptionService.cancelSubscription().subscribe({
      next: () => {
        this.cancellingSubscription.set(false);
        this._profileService.load();
      },
      error: (err) => {
        this.cancellingSubscription.set(false);
        if (err.status === 404) {
          this.subscribeError.set("This deployed API revision does not support subscription cancellation yet. Redeploy the API to pick up the latest subscription endpoints.");
          return;
        }

        this.subscribeError.set(err.error?.errorMessage ?? "Failed to cancel subscription.");
      }
    });
  }

  public onConfigureWallet(): void {
    this.showKeyInput.set(true);
    this.configError.set(null);
  }

  public onCancelConfigure(): void {
    this.showKeyInput.set(false);
    this.walletAddressControl.reset();
    this.configError.set(null);
  }

  public onSubmitKey(): void {
    if (this.walletAddressControl.invalid) {
      return;
    }

    this.configuring.set(true);
    this.configError.set(null);

    this._walletService.configure(this.walletAddressControl.value!).subscribe({
      next: () => {
        this.configuring.set(false);
        this.showKeyInput.set(false);
        this.walletAddressControl.reset();
        this._healthService.refresh();
      },
      error: (err) => {
        this.configuring.set(false);
        this.configError.set(err.error?.message ?? "Failed to configure wallet. Check your wallet address.");
      }
    });
  }

  public onDisconnectWallet(): void {
    this._walletService.disconnect().subscribe({
      next: () => this._healthService.refresh()
    });
  }

  public onNetworkChange(network: string): void {
    this.networkSaving.set(true);
    this._profileService.updateNetwork(network).subscribe({
      next: () => this.networkSaving.set(false),
      error: () => this.networkSaving.set(false)
    });
  }

  public onLogout(): void {
    this._authService.logout();
    this._router.navigate(["/login"]);
  }

  public onCopySha256(hash: string): void {
    navigator.clipboard.writeText(hash);
    this.sha256Copied.set(true);
    setTimeout(() => this.sha256Copied.set(false), 2000);
  }

  public formatFileSize(bytes: number | null): string {
    if (bytes === null || bytes === 0) return "";
    const mb = bytes / (1024 * 1024);
    return mb >= 1 ? `${mb.toFixed(1)} MB` : `${(bytes / 1024).toFixed(0)} KB`;
  }

  public getTierName(tier: string | null): string {
    if (tier === "beginner") return "Beginner";
    if (tier === "pro") return "Pro";
    if (tier === "free") return "Beginner";
    return "Unknown";
  }

  public getStatusLabel(status: string | null): string {
    if (status === "active") return "Active";
    if (status === "expired") return "Expired";
    if (status === "cancelled") return "Cancelled";
    return "None";
  }

  private getUpgradePromptLabel(value: string | null): string | null {
    if (!value) {
      return null;
    }

    const normalized = value.trim().toLowerCase();

    if (normalized === "macro-calendar") {
      return "Macro Calendar";
    }

    if (normalized === "optimizer") {
      return "Optimizer";
    }

    if (normalized === "webhooks") {
      return "TradingView Webhooks";
    }

    return "this feature";
  }
}
