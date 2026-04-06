import { Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatChipsModule } from "@angular/material/chips";
import { MatTableModule } from "@angular/material/table";
import { MatSelectModule } from "@angular/material/select";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatTooltipModule } from "@angular/material/tooltip";
import { FormsModule } from "@angular/forms";
import { MacroCalendarService } from "./macro-calendar.service";
import { MacroEventListItem, IMPORTANCE_LABELS, STATUS_LABELS } from "./models/macro-event.model";

@Component({
  selector: "app-macro-calendar-page",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTableModule,
    MatSelectModule,
    MatFormFieldModule,
    MatTooltipModule,
    FormsModule
  ],
  templateUrl: "./macro-calendar-page.component.html",
  styleUrl: "./macro-calendar-page.component.scss"
})
export class MacroCalendarPageComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _macroService = inject(MacroCalendarService);

  public events: MacroEventListItem[] = [];
  public activeBlocks: MacroEventListItem[] = [];
  public isLoading = false;
  public isSyncing = false;
  public lastSyncMessage = "";
  public selectedCurrency = "";

  public readonly importanceLabels = IMPORTANCE_LABELS;
  public readonly statusLabels = STATUS_LABELS;

  public readonly displayedColumns = [
    "importance", "title", "country", "currency", "category",
    "scheduledAt", "status", "forecast", "previous", "actual", "blockWindow"
  ];

  public readonly currencyOptions = ["", "USD", "EUR", "GBP", "JPY", "AUD"];

  public ngOnInit(): void {
    this.load();
  }

  public load(): void {
    this.isLoading = true;

    const now = Date.now();
    const sevenDaysMs = 7 * 24 * 60 * 60 * 1000;
    const currency = this.selectedCurrency || undefined;

    this._macroService.getUpcomingEvents(now, now + sevenDaysMs, currency)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (result) => {
          this.events = result;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });

    this._macroService.getActiveBlocks()
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (result) => this.activeBlocks = result
      });
  }

  public onSync(): void {
    this.isSyncing = true;
    this.lastSyncMessage = "";

    this._macroService.triggerSync()
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (result) => {
          this.isSyncing = false;
          this.lastSyncMessage = `Synced: ${result.fetched} fetched, ${result.inserted} new, ${result.updated} updated`;
          this.load();
        },
        error: () => {
          this.isSyncing = false;
          this.lastSyncMessage = "Sync failed";
        }
      });
  }

  public onCurrencyChange(): void {
    this.load();
  }

  public getImportanceClass(importance: number): string {
    switch (importance) {
      case 4: return "importance-critical";
      case 3: return "importance-high";
      case 2: return "importance-medium";
      case 1: return "importance-low";
      default: return "importance-unknown";
    }
  }

  public formatTimestamp(ms: number): string {
    return new Date(ms).toLocaleString("en-GB", {
      day: "2-digit",
      month: "short",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      timeZone: "UTC",
      timeZoneName: "short"
    });
  }

  public formatTime(ms: number): string {
    return new Date(ms).toLocaleTimeString("en-GB", {
      hour: "2-digit",
      minute: "2-digit",
      timeZone: "UTC"
    });
  }
}
