import { DatePipe } from "@angular/common";
import { HttpContext } from "@angular/common/http";
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatDialog } from "@angular/material/dialog";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatIconModule } from "@angular/material/icon";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { PagedResult } from "../../../../core/models/paged-result.model";
import { SKIP_ERROR_NOTIFICATION } from "../../../../core/interceptors/http-context-tokens";
import { NotificationService } from "../../../../core/services/notification.service";
import { ConfirmDialogComponent, ConfirmDialogData } from "../../../order-entry/confirm-dialog/confirm-dialog.component";
import { StrategyDiffDto, StrategyRevisionSummaryDto } from "../../models/strategy.model";
import { StrategyApiService } from "../../services/strategy-api.service";
import { DiffViewComponent } from "../diff-view/diff-view.component";

@Component({
  selector: "app-revision-history-panel",
  standalone: true,
  imports: [
    DatePipe,
    DiffViewComponent,
    MatButtonModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./revision-history-panel.component.html",
  styleUrl: "./revision-history-panel.component.scss"
})
export class RevisionHistoryPanelComponent implements OnChanges {
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _notifications = inject(NotificationService);
  private readonly _dialog = inject(MatDialog);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  @Input({ required: true })
  public strategyId!: string;

  @Output()
  public readonly restored = new EventEmitter<void>();

  public readonly displayedColumns = ["select", "revisionNumber", "source", "changeSummary", "createdAt", "actions"];
  public revisions: StrategyRevisionSummaryDto[] = [];
  public totalCount = 0;
  public page = 1;
  public pageSize = 20;
  public isLoading = false;
  public isDiffLoading = false;
  public selectedFrom: number | null = null;
  public selectedTo: number | null = null;
  public diff: StrategyDiffDto | null = null;

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["strategyId"]?.currentValue) {
      this.page = 1;
      this._resetSelection();
      this.loadRevisions();
    }
  }

  public loadRevisions(): void {
    this.isLoading = true;

    this._strategyApi.getVersions(this.strategyId, this.page, this.pageSize, this._localErrorContext).subscribe({
      next: (result: PagedResult<StrategyRevisionSummaryDto>) => {
        this.revisions = result.items;
        this.totalCount = result.totalCount;
        this.page = result.page;
        this.pageSize = result.pageSize;
        this.isLoading = false;
      },
      error: () => {
        this.revisions = [];
        this.totalCount = 0;
        this.isLoading = false;
        this._notifications.error("Failed to load revision history.");
      }
    });
  }

  public onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this._resetSelection();
    this.loadRevisions();
  }

  public toggleSelection(revisionNumber: number): void {
    if (this.selectedFrom === revisionNumber) {
      this.selectedFrom = null;
      this.diff = null;
      return;
    }

    if (this.selectedTo === revisionNumber) {
      this.selectedTo = null;
      this.diff = null;
      return;
    }

    if (this.selectedFrom === null) {
      this.selectedFrom = revisionNumber;
      this.diff = null;
      return;
    }

    if (this.selectedTo === null) {
      this.selectedTo = revisionNumber;
      this._loadDiff();
    }
  }

  public isSelected(revisionNumber: number): boolean {
    return this.selectedFrom === revisionNumber || this.selectedTo === revisionNumber;
  }

  public canSelect(revisionNumber: number): boolean {
    return this.isSelected(revisionNumber) || this.selectedFrom === null || this.selectedTo === null;
  }

  public restore(revision: StrategyRevisionSummaryDto): void {
    const dialogData: ConfirmDialogData = {
      title: `Restore Revision ${revision.revisionNumber}`,
      message: `Create a new revision from revision ${revision.revisionNumber}?`,
      confirmText: "Restore",
      cancelText: "Cancel"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "400px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this._strategyApi.restoreVersion(this.strategyId, revision.revisionNumber, this._localErrorContext).subscribe({
        next: () => {
          this._notifications.success(`Revision ${revision.revisionNumber} restored.`);
          this._resetSelection();
          this.loadRevisions();
          this.restored.emit();
        },
        error: () => {
          this._notifications.error("Failed to restore revision.");
        }
      });
    });
  }

  private _loadDiff(): void {
    if (this.selectedFrom === null || this.selectedTo === null) {
      this.diff = null;
      return;
    }

    this.isDiffLoading = true;

    this._strategyApi.getDiff(this.strategyId, this.selectedFrom, this.selectedTo, this._localErrorContext).subscribe({
      next: (result: StrategyDiffDto) => {
        this.diff = result;
        this.isDiffLoading = false;
      },
      error: () => {
        this.diff = null;
        this.isDiffLoading = false;
        this._notifications.error("Failed to compute diff.");
      }
    });
  }

  private _resetSelection(): void {
    this.selectedFrom = null;
    this.selectedTo = null;
    this.diff = null;
    this.isDiffLoading = false;
  }
}