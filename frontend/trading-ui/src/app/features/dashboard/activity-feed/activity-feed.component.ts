import { Component, DestroyRef, inject, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { AccountStateService } from "../../../core/services/account-state.service";
import { UserEvent } from "../../../core/models/user-event.model";
import { FillEvent } from "../../../core/models/fill-event.model";
import { OrderUpdate } from "../../../core/models/order-update.model";

@Component({
  selector: "app-activity-feed",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./activity-feed.component.html",
  styleUrls: ["./activity-feed.component.scss"]
})
export class ActivityFeedComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _accountState = inject(AccountStateService);

  public events: UserEvent[] = [];

  public ngOnInit(): void {
    this._accountState.events$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((events: UserEvent[]) => {
        this.events = events;
      });
  }

  public isFill(event: UserEvent): event is UserEvent & { data: FillEvent } {
    return event.type === "Fill";
  }

  public isOrderUpdate(event: UserEvent): event is UserEvent & { data: OrderUpdate } {
    return event.type === "OrderUpdate";
  }

  public getEventDescription(event: UserEvent): string {
    if (this.isFill(event)) {
      const fill = event.data as FillEvent;
      return `${fill.side} ${fill.size} ${fill.asset} @ ${fill.price}`;
    }
    const order = event.data as OrderUpdate;
    return `${order.asset} — ${order.status} (filled: ${order.filledSize}, remaining: ${order.remainingSize})`;
  }
}
