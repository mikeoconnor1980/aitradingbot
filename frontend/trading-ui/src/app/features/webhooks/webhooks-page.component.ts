import { HttpContext } from "@angular/common/http";
import { Component, DestroyRef, OnInit, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatChipsModule } from "@angular/material/chips";
import { MatDialog } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { interval } from "rxjs";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { RelativeTimePipe } from "../../core/components/notification-panel/relative-time.pipe";
import { TradableAsset } from "../../core/models/tradable-asset.model";
import { AgentInfo, AgentService } from "../../core/services/agent.service";
import { NotificationFacade } from "../../core/services/notification-facade.service";
import {
  CreateWebhookRequest,
  UpdateWebhookRequest,
  WebhookApiService,
  WebhookConfigDto
} from "../../core/services/webhook-api.service";
import { OrderService } from "../../core/services/order.service";
import { environment } from "../../../environments/environment";
import { ConfirmDialogComponent, ConfirmDialogData } from "../order-entry/confirm-dialog/confirm-dialog.component";
import {
  CreateWebhookDialogComponent,
  CreateWebhookDialogData,
  CreateWebhookDialogResult
} from "./create-webhook-dialog.component";

@Component({
  selector: "app-webhooks-page",
  standalone: true,
  imports: [
    RelativeTimePipe,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./webhooks-page.component.html",
  styleUrl: "./webhooks-page.component.scss"
})
export class WebhooksPageComponent implements OnInit {
  private readonly _dialog = inject(MatDialog);
  private readonly _webhookApi = inject(WebhookApiService);
  private readonly _orderService = inject(OrderService);
  private readonly _agentService = inject(AgentService);
  private readonly _notifications = inject(NotificationFacade);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  public readonly displayedColumns = ["label", "asset", "agent", "status", "url", "lastTriggered", "actions"];
  public readonly relativeTimeRefresh = signal(Date.now());
  public webhooks: WebhookConfigDto[] = [];
  public assets: TradableAsset[] = [];
  public agents: AgentInfo[] = [];
  public isLoading = true;
  public busyWebhookId: string | null = null;

  public constructor() {
    interval(10000)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(() => {
        this.relativeTimeRefresh.set(Date.now());
      });
  }

  public ngOnInit(): void {
    this._agentService.refreshAgents();
    this._agentService.agents$.subscribe((agents) => {
      this.agents = agents.filter((agent) => agent.state !== "disconnected" && agent.state !== "killed");
    });

    this._orderService.getAvailableAssets().subscribe({
      next: (assets) => {
        this.assets = assets;
      },
      error: () => {
        this.assets = [];
      }
    });

    this._loadWebhooks();
  }

  public buildWebhookUrl(token: string): string {
    const baseUrl = environment.apiBaseUrl.replace(/\/api\/?$/, "");
    return `${baseUrl}/api/webhooks/tradingview/${token}`;
  }

  public getAgentLabel(targetAgentId: string | null): string {
    if (!targetAgentId) {
      return "Auto";
    }

    const agent = this.agents.find((item) => item.agentId === targetAgentId);
    return agent ? `${agent.agentId} · ${agent.machineName}` : targetAgentId;
  }

  public onCreateWebhook(): void {
    const dialogData: CreateWebhookDialogData = {
      assets: this.assets,
      agents: this.agents
    };

    this._dialog.open(CreateWebhookDialogComponent, {
      width: "480px",
      data: dialogData
    }).afterClosed().subscribe((result: CreateWebhookDialogResult | undefined) => {
      if (!result) {
        return;
      }

      const request: CreateWebhookRequest = {
        label: result.label,
        defaultAsset: result.defaultAsset,
        targetAgentId: result.targetAgentId
      };

      this._webhookApi.createWebhook(request).subscribe({
        next: (webhook) => {
          this.webhooks = [webhook, ...this.webhooks];
          this._notifications.success(`Webhook '${webhook.label}' created.`);
        },
        error: () => {
          this._notifications.error("Failed to create webhook.");
        }
      });
    });
  }

  public onCopyWebhookUrl(webhook: WebhookConfigDto): void {
    navigator.clipboard.writeText(this.buildWebhookUrl(webhook.token));
    this._notifications.success(`Copied webhook URL for '${webhook.label}'.`);
  }

  public onToggleEnabled(webhook: WebhookConfigDto, enabled: boolean): void {
    this.busyWebhookId = webhook.id;

    const request: UpdateWebhookRequest = {
      label: webhook.label,
      defaultAsset: webhook.defaultAsset,
      targetAgentId: webhook.targetAgentId,
      isEnabled: enabled
    };

    this._webhookApi.updateWebhook(webhook.id, request).subscribe({
      next: (updated) => {
        this._replaceWebhook(updated);
        this.busyWebhookId = null;
      },
      error: () => {
        this.busyWebhookId = null;
        this._notifications.error("Failed to update webhook.");
      }
    });
  }

  public onRegenerate(webhook: WebhookConfigDto): void {
    const dialogData: ConfirmDialogData = {
      title: "Regenerate Webhook Token",
      message: `Regenerate the TradingView webhook URL for '${webhook.label}'? Existing alerts will stop working immediately.`,
      confirmText: "Regenerate",
      cancelText: "Cancel"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "420px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.busyWebhookId = webhook.id;
      this._webhookApi.regenerateToken(webhook.id).subscribe({
        next: (updated) => {
          this._replaceWebhook(updated);
          this.busyWebhookId = null;
          this._notifications.success(`Regenerated webhook token for '${updated.label}'.`);
        },
        error: () => {
          this.busyWebhookId = null;
          this._notifications.error("Failed to regenerate webhook token.");
        }
      });
    });
  }

  public onDelete(webhook: WebhookConfigDto): void {
    const dialogData: ConfirmDialogData = {
      title: "Delete Webhook",
      message: `Delete '${webhook.label}'? TradingView alerts using this URL will fail immediately.`,
      confirmText: "Delete",
      cancelText: "Cancel"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "420px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.busyWebhookId = webhook.id;
      this._webhookApi.deleteWebhook(webhook.id).subscribe({
        next: () => {
          this.webhooks = this.webhooks.filter((item) => item.id !== webhook.id);
          this.busyWebhookId = null;
          this._notifications.success(`Deleted webhook '${webhook.label}'.`);
        },
        error: () => {
          this.busyWebhookId = null;
          this._notifications.error("Failed to delete webhook.");
        }
      });
    });
  }

  private _loadWebhooks(): void {
    this.isLoading = true;
    this._webhookApi.getWebhooks().subscribe({
      next: (webhooks) => {
        this.webhooks = webhooks;
        this.isLoading = false;
      },
      error: () => {
        this.webhooks = [];
        this.isLoading = false;
        this._notifications.error("Failed to load webhooks.");
      }
    });
  }

  private _replaceWebhook(updated: WebhookConfigDto): void {
    this.webhooks = this.webhooks.map((item) => item.id === updated.id ? updated : item);
  }
}