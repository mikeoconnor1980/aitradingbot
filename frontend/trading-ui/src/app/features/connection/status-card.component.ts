import { AsyncPipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { HealthResponse } from "../../core/models/health-response.model";
import { HealthService } from "../../core/services/health.service";

@Component({
  selector: "app-status-card",
  standalone: true,
  imports: [AsyncPipe],
  templateUrl: "./status-card.component.html",
  styleUrl: "./status-card.component.scss"
})
export class StatusCardComponent {
  private readonly _healthService = inject(HealthService);

  public readonly health$ = this._healthService.health$;

  public onRefresh(): void {
    this._healthService.refresh();
  }

  public truncateWalletAddress(address: string): string {
    if (!address) {
      return "N/A";
    }

    if (address.length <= 14) {
      return address;
    }

    return `${address.slice(0, 6)}...${address.slice(-4)}`;
  }

  public connectionStatusText(health: HealthResponse): string {
    return health.status === "connected" ? "Connected" : "Disconnected";
  }
}
