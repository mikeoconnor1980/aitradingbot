import { DatePipe, TitleCasePipe } from "@angular/common";
import { HttpContext } from "@angular/common/http";
import { Component, OnInit, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatChipsModule } from "@angular/material/chips";
import { MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { NotificationFacade } from "../../core/services/notification-facade.service";
import { ConfirmDialogComponent, ConfirmDialogData } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { StrategyTemplateDto } from "../strategy-builder/models/strategy.model";
import { StrategyApiService } from "../strategy-builder/services/strategy-api.service";
import {
  RenameStrategyTemplateDialogComponent,
  RenameStrategyTemplateDialogResult
} from "./rename-strategy-template-dialog.component";

@Component({
  selector: "app-strategy-library-page",
  standalone: true,
  imports: [
    DatePipe,
    TitleCasePipe,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./strategy-library-page.component.html",
  styleUrl: "./strategy-library-page.component.scss"
})
export class StrategyLibraryPageComponent implements OnInit {
  private readonly _dialog = inject(MatDialog);
  private readonly _strategyApi = inject(StrategyApiService);
  private readonly _notifications = inject(NotificationFacade);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  public readonly displayedColumns = ["name", "source", "mode", "tags", "updatedAt", "actions"];
  public templates: StrategyTemplateDto[] = [];
  public isLoading = true;
  public renamingTemplateId: string | null = null;
  public removingTemplateId: string | null = null;

  public ngOnInit(): void {
    this._loadTemplates();
  }

  public canManage(template: StrategyTemplateDto): boolean {
    return !template.isSystemTemplate;
  }

  public onRename(template: StrategyTemplateDto): void {
    if (!this.canManage(template) || this.renamingTemplateId !== null || this.removingTemplateId !== null) {
      return;
    }

    const dialogRef = this._dialog.open(RenameStrategyTemplateDialogComponent, {
      width: "480px",
      data: {
        name: template.name,
        description: template.description,
        existingNames: this.templates
          .filter((item) => item.id !== template.id)
          .map((item) => item.name)
      }
    });

    dialogRef.afterClosed().subscribe((result: RenameStrategyTemplateDialogResult | undefined) => {
      if (result === undefined) {
        return;
      }

      this.renamingTemplateId = template.id;
      this._strategyApi.renameTemplate(template.id, result, this._localErrorContext).subscribe({
        next: () => {
          this.renamingTemplateId = null;
          this._notifications.success(`Template '${result.name}' updated.`);
          this._loadTemplates();
        },
        error: () => {
          this.renamingTemplateId = null;
          this._notifications.error("Failed to update strategy template.");
        }
      });
    });
  }

  public onRemove(template: StrategyTemplateDto): void {
    if (!this.canManage(template) || this.renamingTemplateId !== null || this.removingTemplateId !== null) {
      return;
    }

    const dialogData: ConfirmDialogData = {
      title: "Remove Strategy Template",
      message: `Remove '${template.name}' from the shared strategy library? Existing cloned strategies will remain available.`,
      confirmText: "Remove",
      cancelText: "Cancel"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "420px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.removingTemplateId = template.id;
      this._strategyApi.unpublishTemplate(template.id, this._localErrorContext).subscribe({
        next: () => {
          this.removingTemplateId = null;
          this._notifications.success(`Template '${template.name}' removed from the library.`);
          this._loadTemplates();
        },
        error: () => {
          this.removingTemplateId = null;
          this._notifications.error("Failed to remove strategy template.");
        }
      });
    });
  }

  private _loadTemplates(): void {
    this.isLoading = true;

    this._strategyApi.getTemplates(this._localErrorContext).subscribe({
      next: (templates) => {
        this.templates = templates;
        this.isLoading = false;
      },
      error: () => {
        this.templates = [];
        this.isLoading = false;
        this._notifications.error("Failed to load strategy library.");
      }
    });
  }
}