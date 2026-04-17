import { CommonModule } from "@angular/common";
import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { FearGreedStatusDto } from "../../core/models/fear-greed.models";
import { FearGreedService } from "../../core/services/fear-greed.service";
import { NotificationFacade } from "../../core/services/notification-facade.service";

@Component({
  selector: "app-fear-greed-management",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: "./fear-greed-management.component.html",
  styleUrl: "./fear-greed-management.component.scss",
})
export class FearGreedManagementComponent implements OnInit {
  private readonly _fearGreedService = inject(FearGreedService);
  private readonly _notificationService = inject(NotificationFacade);
  private readonly _destroyRef = inject(DestroyRef);

  public status: FearGreedStatusDto | null = null;
  public isLoading = false;
  public isBackfilling = false;

  public ngOnInit(): void {
    this.loadStatus();
  }

  public loadStatus(): void {
    this.isLoading = true;
    this._fearGreedService
      .getStatus()
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (status) => {
          this.status = status;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
          this._notificationService.error(
            "Failed to load Fear & Greed status."
          );
        },
      });
  }

  public backfill(): void {
    this.isBackfilling = true;
    this._fearGreedService
      .backfill()
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (result) => {
          this.isBackfilling = false;
          this._notificationService.success(
            `Backfill complete: ${result.fetched} fetched, ${result.inserted} new readings inserted.`
          );
          this.loadStatus();
        },
        error: () => {
          this.isBackfilling = false;
          this._notificationService.error("Backfill failed.");
        },
      });
  }

  public getClassificationColor(classification: string | null): string {
    switch (classification) {
      case "Extreme Fear":
        return "#ea3943";
      case "Fear":
        return "#ea8c00";
      case "Neutral":
        return "#c3c3c3";
      case "Greed":
        return "#93d900";
      case "Extreme Greed":
        return "#16c784";
      default:
        return "#c3c3c3";
    }
  }
}
