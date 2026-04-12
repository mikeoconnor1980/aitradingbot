import { AsyncPipe, DatePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ReactiveFormsModule, FormControl, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { map } from "rxjs";
import { AuthService } from "../../core/services/auth.service";
import { HealthService } from "../../core/services/health.service";
import { ProfileService } from "../../core/services/profile.service";
import { SubscriptionService } from "../../core/services/subscription.service";
import { WalletService } from "../../core/services/wallet.service";
import { Router } from "@angular/router";
import { environment } from "../../../environments/environment";

@Component({
  selector: "app-profile-page",
  standalone: true,
  imports: [AsyncPipe, DatePipe, ReactiveFormsModule, MatCardModule, MatIconModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: "./profile-page.component.html",
  styleUrl: "./profile-page.component.scss"
})
export class ProfilePageComponent implements OnInit {
  private readonly _authService = inject(AuthService);
  private readonly _healthService = inject(HealthService);
  private readonly _profileService = inject(ProfileService);
  private readonly _walletService = inject(WalletService);
  private readonly _subscriptionService = inject(SubscriptionService);
  private readonly _router = inject(Router);

  public get user() {
    const current = this._authService.currentUser;
    return {
      displayName: current?.displayName ?? "Trader",
      email: current?.email ?? ""
    };
  }

  public readonly subscription$ = this._subscriptionService.status$;
  public readonly subscribing = signal(false);
  public readonly subscribeError = signal<string | null>(null);

  public readonly wallet$ = this._healthService.health$.pipe(
    map((h) => h ? { address: h.walletAddress, network: h.network } : null)
  );

  public readonly walletStatus$ = this._walletService.status$;
  public readonly profile$ = this._profileService.profile$;
  public readonly appVersion = environment.appVersion;

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
    this.profile$.subscribe((profile) => {
      if (profile) {
        this.networkControl.setValue(profile.preferredNetwork, { emitEvent: false });
      }
    });
  }

  public onSubscribeFreeTier(): void {
    this.subscribing.set(true);
    this.subscribeError.set(null);

    this._subscriptionService.subscribeFreeTier().subscribe({
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

  public getTierName(tier: string | null): string {
    if (tier === "free") return "Free";
    return "Unknown";
  }

  public getStatusLabel(status: string | null): string {
    if (status === "active") return "Active";
    if (status === "expired") return "Expired";
    if (status === "cancelled") return "Cancelled";
    return "None";
  }
}
