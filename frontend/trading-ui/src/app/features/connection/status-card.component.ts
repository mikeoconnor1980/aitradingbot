import { AsyncPipe } from "@angular/common";
import { Component, inject } from "@angular/core";
import { RouterLink } from "@angular/router";
import { ConnectionState, ConnectionStatus } from "../../core/models/connection-status.model";
import { HealthResponse } from "../../core/models/health-response.model";
import { HealthService } from "../../core/services/health.service";
import { SignalRService } from "../../core/services/signalr.service";
import { environment } from "../../../environments/environment";

@Component({
  selector: "app-status-card",
  standalone: true,
  imports: [AsyncPipe, RouterLink],
  templateUrl: "./status-card.component.html",
  styleUrl: "./status-card.component.scss"
})
export class StatusCardComponent {
  private readonly _healthService = inject(HealthService);
  private readonly _signalRService = inject(SignalRService);

  public readonly appVersion = environment.appVersion;
  public readonly health$ = this._healthService.health$;
  public readonly signalRTransportStatus$ = this._signalRService.transportConnectionStatus$;

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

  public walletBadgeText(health: HealthResponse): string {
    const network = health.network.trim().toLowerCase();

    if (network === "testnet") {
      return "Testnet";
    }

    return this.connectionStatusText(health);
  }

  public signalRStatusText(status: ConnectionStatus): string {
    return status.status;
  }

  public signalRRetryText(retryCount: number): string {
    return retryCount === 1 ? "1 retry" : `${retryCount} retries`;
  }

  public healthDetailText(health: HealthResponse): string {
    if (health.error) {
      return health.error;
    }

    return health.status === "connected"
      ? "Backend API and wallet health checks are responding normally."
      : "Backend API or wallet verification is currently unavailable.";
  }

  public signalRDetailText(status: ConnectionStatus): string {
    if (status.detail) {
      return status.detail;
    }

    switch (status.status) {
      case "Connected":
        return "Live updates are flowing through the SignalR hub.";
      case "Reconnecting":
        return "The client is retrying the SignalR transport connection.";
      case "Disconnected":
      default:
        return "The browser is not currently connected to the live SignalR transport.";
    }
  }

  public displayTimestamp(timestamp: string): string {
    if (!timestamp) {
      return "N/A";
    }

    const parsed = new Date(timestamp);

    if (Number.isNaN(parsed.getTime())) {
      return timestamp;
    }

    return parsed.toISOString().slice(0, 16).replace("T", " ") + " UTC";
  }

  public toneForStatus(status: HealthResponse["status"] | ConnectionState): "connected" | "reconnecting" | "disconnected" {
    switch (status) {
      case "connected":
      case "Connected":
        return "connected";
      case "Reconnecting":
        return "reconnecting";
      case "disconnected":
      case "Disconnected":
      default:
        return "disconnected";
    }
  }
}
