import { AsyncPipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ReactiveFormsModule, FormControl, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { map } from "rxjs";
import { HealthService } from "../../core/services/health.service";
import { WalletService } from "../../core/services/wallet.service";
import { environment } from "../../../environments/environment";

@Component({
  selector: "app-profile-page",
  standalone: true,
  imports: [AsyncPipe, ReactiveFormsModule, MatCardModule, MatIconModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: "./profile-page.component.html",
  styleUrl: "./profile-page.component.scss"
})
export class ProfilePageComponent implements OnInit {
  private readonly _healthService = inject(HealthService);
  private readonly _walletService = inject(WalletService);

  public readonly user = {
    displayName: "Trader",
    email: "trader@example.com",
    membership: "Pro",
    joinedDate: "January 2026"
  };

  public readonly wallet$ = this._healthService.health$.pipe(
    map((h) => h ? { address: h.walletAddress, network: h.network } : null)
  );

  public readonly walletStatus$ = this._walletService.status$;
  public readonly appVersion = environment.appVersion;

  public readonly privateKeyControl = new FormControl("", [
    Validators.required,
    Validators.pattern(/^0x[0-9a-fA-F]{64}$/)
  ]);

  public readonly showKeyInput = signal(false);
  public readonly configuring = signal(false);
  public readonly configError = signal<string | null>(null);

  public ngOnInit(): void {
    this._walletService.refreshStatus();
  }

  public onConfigureWallet(): void {
    this.showKeyInput.set(true);
    this.configError.set(null);
  }

  public onCancelConfigure(): void {
    this.showKeyInput.set(false);
    this.privateKeyControl.reset();
    this.configError.set(null);
  }

  public onSubmitKey(): void {
    if (this.privateKeyControl.invalid) {
      return;
    }

    this.configuring.set(true);
    this.configError.set(null);

    this._walletService.configure(this.privateKeyControl.value!).subscribe({
      next: () => {
        this.configuring.set(false);
        this.showKeyInput.set(false);
        this.privateKeyControl.reset();
        this._healthService.refresh();
      },
      error: (err) => {
        this.configuring.set(false);
        this.configError.set(err.error?.detail ?? "Failed to configure wallet. Check your private key.");
      }
    });
  }

  public onDisconnectWallet(): void {
    this._walletService.disconnect().subscribe({
      next: () => this._healthService.refresh()
    });
  }
}
