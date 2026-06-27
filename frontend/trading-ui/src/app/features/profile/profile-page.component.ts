import { AsyncPipe, DatePipe, SlicePipe } from "@angular/common";
import { HttpErrorResponse } from "@angular/common/http";
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
import { ApiVersionInfo, ApiVersionService } from "../../core/services/api-version.service";
import { HealthService } from "../../core/services/health.service";
import { ProfileService } from "../../core/services/profile.service";
import { SubscriptionService } from "../../core/services/subscription.service";
import { WalletService } from "../../core/services/wallet.service";
import { ActivatedRoute, Router } from "@angular/router";
import { environment } from "../../../environments/environment";
import { TelegramLinkComponent } from "./telegram-link.component";
import { ExchangeCredential, ExchangeCredentialsService } from "../../core/services/exchange-credentials.service";

interface ApiVersionViewModel {
  data: ApiVersionInfo | null;
  error: boolean;
}

interface InstallerInfoViewModel {
  data: InstallerInfo | null;
  error: boolean;
  errorMessage: string | null;
}

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
  private readonly _apiVersionService = inject(ApiVersionService);
  private readonly _profileService = inject(ProfileService);
  private readonly _walletService = inject(WalletService);
  private readonly _subscriptionService = inject(SubscriptionService);
  private readonly _exchangeCredentialsService = inject(ExchangeCredentialsService);
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
    map((info) => ({ data: info, error: false, errorMessage: null })),
    catchError((error: unknown) => of({
      data: null as InstallerInfo | null,
      error: true,
      errorMessage: this.getInstallerInfoErrorMessage(error)
    }))
  ) as import("rxjs").Observable<InstallerInfoViewModel>;
  public readonly apiVersionInfo$ = this._apiVersionService.getVersion().pipe(
    map((info) => ({ data: info, error: false })),
    catchError(() => of({ data: null as ApiVersionInfo | null, error: true }))
  ) as import("rxjs").Observable<ApiVersionViewModel>;
  public readonly hasConnectedAgent$ = this._agentService.agents$.pipe(
    map((agents) => agents.some((a) => a.state !== "disconnected" && a.state !== "killed"))
  );
  public readonly sha256Copied = signal(false);
  public readonly installerDownloadInFlight = signal<"exe" | "zip" | null>(null);
  public readonly installerDownloadError = signal<string | null>(null);

  public readonly networkControl = new FormControl("mainnet");
  public readonly exchangeControl = new FormControl("Hyperliquid", { nonNullable: true });
  public readonly networkSaving = signal(false);
  public readonly exchangeSaving = signal(false);

  public readonly credentialLabelControl = new FormControl("Primary Binance", { nonNullable: true });
  public readonly credentialApiKeyControl = new FormControl("", { nonNullable: true, validators: [Validators.required] });
  public readonly credentialApiSecretControl = new FormControl("", { nonNullable: true, validators: [Validators.required] });
  public readonly credentialSaving = signal(false);
  public readonly credentialTesting = signal(false);
  public readonly credentialsLoading = signal(false);
  public readonly credentialError = signal<string | null>(null);
  public readonly credentialSuccess = signal<string | null>(null);
  public credentials: ExchangeCredential[] = [];

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
    this.loadCredentials();
    this.upgradePrompt.set(this.getUpgradePromptLabel(this._route.snapshot.queryParamMap.get("upgrade")));
    this.profile$.subscribe((profile) => {
      if (profile) {
        this.networkControl.setValue(profile.preferredNetwork, { emitEvent: false });
        this.exchangeControl.setValue(profile.preferredExchange ?? "Hyperliquid", { emitEvent: false });
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
        if (err.status === 404) {
          this.subscribeError.set(
            tier === "pro"
              ? "This deployed API revision does not support Pro subscriptions yet. Redeploy the API to pick up the latest subscription endpoints."
              : "This deployed API revision does not support tiered subscriptions yet. The app will fall back to the legacy Beginner trial route when available."
          );
          return;
        }

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

  public onExchangeChange(exchange: string): void {
    this.exchangeSaving.set(true);
    this._profileService.updateExchange(exchange).subscribe({
      next: () => this.exchangeSaving.set(false),
      error: () => this.exchangeSaving.set(false)
    });
  }

  public onDownloadInstaller(format: "exe" | "zip", fileName?: string): void {
    this.installerDownloadInFlight.set(format);
    this.installerDownloadError.set(null);

    this._agentService.downloadInstaller(format, fileName).subscribe({
      next: () => this.installerDownloadInFlight.set(null),
      error: (error: unknown) => {
        this.installerDownloadInFlight.set(null);
        this.installerDownloadError.set(this.getInstallerDownloadErrorMessage(error));
      }
    });
  }

  public loadCredentials(): void {
    this.credentialsLoading.set(true);
    this._exchangeCredentialsService.list().subscribe({
      next: (credentials) => {
        this.credentials = credentials;
        this.credentialsLoading.set(false);
      },
      error: () => {
        this.credentials = [];
        this.credentialsLoading.set(false);
      }
    });
  }

  public onSaveBinanceCredential(): void {
    if (this.credentialApiKeyControl.invalid || this.credentialApiSecretControl.invalid) {
      this.credentialApiKeyControl.markAsTouched();
      this.credentialApiSecretControl.markAsTouched();
      return;
    }

    this.credentialSaving.set(true);
    this.credentialError.set(null);
    this.credentialSuccess.set(null);

    this._exchangeCredentialsService.save(
      "Binance",
      this.credentialApiKeyControl.value,
      this.credentialApiSecretControl.value,
      this.credentialLabelControl.value.trim() || "Primary Binance")
      .subscribe({
        next: () => {
          this.credentialSaving.set(false);
          this.credentialApiSecretControl.reset("");
          this.credentialSuccess.set("Binance credentials saved.");
          this.loadCredentials();
        },
        error: (err) => {
          this.credentialSaving.set(false);
          this.credentialError.set(err.error?.errorMessage ?? err.error?.message ?? "Failed to save Binance credentials.");
        }
      });
  }

  public onDeleteCredential(id: string): void {
    this.credentialError.set(null);
    this.credentialSuccess.set(null);
    this._exchangeCredentialsService.remove(id).subscribe({
      next: () => {
        this.credentialSuccess.set("Credential removed.");
        this.loadCredentials();
      },
      error: (err) => {
        this.credentialError.set(err.error?.errorMessage ?? err.error?.message ?? "Failed to remove credential.");
      }
    });
  }

  public onTestBinanceCredential(): void {
    this.credentialTesting.set(true);
    this.credentialError.set(null);
    this.credentialSuccess.set(null);

    this._exchangeCredentialsService.test("Binance").subscribe({
      next: () => {
        this.credentialTesting.set(false);
        this.credentialSuccess.set("Binance credentials validated successfully.");
      },
      error: (err) => {
        this.credentialTesting.set(false);
        this.credentialError.set(err.error?.errorMessage ?? err.error?.message ?? "Credential validation failed.");
      }
    });
  }

  public get activeBinanceCredential(): ExchangeCredential | null {
    return this.credentials.find((credential) => credential.exchange === "Binance" && credential.isActive) ?? null;
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

  private getInstallerDownloadErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        return "Sign in again to download the execution agent installer.";
      }

      if (error.status === 403) {
        return "An active subscription is required to download the execution agent installer.";
      }

      if (error.status === 404) {
        return "The selected installer package is not available on the server.";
      }
    }

    return "Unable to download the execution agent installer right now.";
  }

  private getInstallerInfoErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return "Unable to reach the API. Check that the control plane is running.";
      }

      if (error.status === 401) {
        return "Sign in again to load execution-agent release details.";
      }

      if (error.status >= 500) {
        return "The API could not read installer metadata. Check storage access and release logs.";
      }
    }

    return "Unable to load execution-agent release details right now.";
  }

  public formatFileSize(bytes: number | null): string {
    if (bytes === null || bytes === 0) return "";
    const mb = bytes / (1024 * 1024);
    return mb >= 1 ? `${mb.toFixed(1)} MB` : `${(bytes / 1024).toFixed(0)} KB`;
  }

  public getInstallerStatusLabel(status: string): string {
    if (status === "Available") return "Published";
    if (status === "ManifestFoundBlobMissing") return "Repair Needed";
    if (status === "FallbackConfigured") return "Fallback";
    if (status === "NoManifest") return "Not Published";
    return status || "Unknown";
  }

  public hasInstallerChecksum(info: InstallerInfo, format: "exe" | "zip"): boolean {
    if (format === "exe") {
      return Boolean(info.exeSha256Hash ?? info.sha256Hash);
    }

    return Boolean(info.zipSha256Hash);
  }

  public getInstallerIntegritySummary(info: InstallerInfo): string {
    const exeHashAvailable = this.hasInstallerChecksum(info, "exe");
    const zipHashAvailable = this.hasInstallerChecksum(info, "zip");

    if (exeHashAvailable && zipHashAvailable) {
      return "SHA256 published for EXE and ZIP";
    }

    if (exeHashAvailable) {
      return info.zipAvailable ? "SHA256 published for EXE only" : "SHA256 published";
    }

    if (zipHashAvailable) {
      return info.exeAvailable ? "SHA256 published for ZIP only" : "SHA256 published";
    }

    return "No SHA256 checksum published";
  }

  public getInstallerAttentionMessage(info: InstallerInfo): string | null {
    if (info.status === "NoManifest") {
      return "No release manifest has been published yet. Run the Windows packaging workflow and promote a latest.json manifest.";
    }

    if (info.status === "ManifestFoundBlobMissing") {
      if (!info.exeAvailable && !info.zipAvailable) {
        return "Release metadata exists, but both installer packages are missing from storage.";
      }

      if (!info.exeAvailable) {
        return "Release metadata exists, but the recommended Windows installer EXE is missing from storage.";
      }

      if (!info.zipAvailable) {
        return "Release metadata exists, but the ZIP fallback package is missing from storage.";
      }

      return "Release metadata exists, but one or more installer files are unavailable.";
    }

    if (info.status === "FallbackConfigured") {
      return "The API is serving fallback installer metadata from configuration. Publish latest.json so the UI and agents use CI-generated release data.";
    }

    if (info.exeAvailable && !this.hasInstallerChecksum(info, "exe")) {
      return "The Windows installer EXE is available, but its SHA256 checksum is missing.";
    }

    if (info.zipAvailable && !this.hasInstallerChecksum(info, "zip")) {
      return "The ZIP fallback package is available, but its SHA256 checksum is missing.";
    }

    return null;
  }

  public getPrimaryInstallerHash(info: InstallerInfo): string | null {
    return info.exeSha256Hash ?? info.sha256Hash ?? info.zipSha256Hash ?? null;
  }

  public getPrimaryInstallerHashLabel(info: InstallerInfo): string {
    return info.exeSha256Hash ?? info.sha256Hash
      ? "Installer SHA256"
      : "ZIP SHA256";
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

  public formatCommit(commitSha: string): string {
    if (!commitSha || commitSha === "unknown") {
      return "Unknown";
    }

    return commitSha.slice(0, 7);
  }

  public formatBuildTime(value: string): string | null {
    if (!value || value === "unknown") {
      return null;
    }

    return value;
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
