import { CommonModule } from "@angular/common";
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { ConnectionStatus } from "./core/models/connection-status.model";
import { HealthResponse } from "./core/models/health-response.model";
import { HealthService } from "./core/services/health.service";
import { SignalRService } from "./core/services/signalr.service";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss"
})
export class AppComponent implements OnInit {
  private readonly _signalRService = inject(SignalRService);
  private readonly _healthService = inject(HealthService);
  private readonly _destroyRef = inject(DestroyRef);

  public title = "Trading Dashboard";

  public connectionStatus: ConnectionStatus = {
    source: "SignalR",
    status: "Disconnected",
    detail: null,
    retryCount: 0
  };
  public health: HealthResponse | null = null;

  public ngOnInit(): void {
    this._signalRService.connectionStatus$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((status: ConnectionStatus) => {
        this.connectionStatus = status;
      });

    this._healthService.health$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((health: HealthResponse | null) => {
        this.health = health;
      });
  }

  public get statusClass(): string {
    if (this.health !== null) {
      return this.health.status === "connected" ? "status--connected" : "status--disconnected";
    }

    switch (this.connectionStatus.status) {
      case "Connected":
        return "status--connected";
      case "Reconnecting":
        return "status--reconnecting";
      case "Disconnected":
      default:
        return "status--disconnected";
    }
  }

  public get statusLabel(): string {
    if (this.health !== null) {
      const network = this.health.network.trim().toLowerCase();

      if (network === "testnet") {
        return "Testnet";
      }

      return this.health.status === "connected" ? "Connected" : "Disconnected";
    }

    return this.connectionStatus.status;
  }
}
