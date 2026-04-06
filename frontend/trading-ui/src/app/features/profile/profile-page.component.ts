import { AsyncPipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { map } from "rxjs";
import { HealthService } from "../../core/services/health.service";
import { environment } from "../../../environments/environment";

@Component({
  selector: "app-profile-page",
  standalone: true,
  imports: [AsyncPipe, MatCardModule, MatIconModule, MatButtonModule],
  templateUrl: "./profile-page.component.html",
  styleUrl: "./profile-page.component.scss"
})
export class ProfilePageComponent {
  private readonly _healthService = inject(HealthService);

  public readonly user = {
    displayName: "Trader",
    email: "trader@example.com",
    membership: "Pro",
    joinedDate: "January 2026"
  };

  public readonly wallet$ = this._healthService.health$.pipe(
    map((h) => h ? { address: h.walletAddress, network: h.network } : null)
  );

  public readonly appVersion = environment.appVersion;
}
