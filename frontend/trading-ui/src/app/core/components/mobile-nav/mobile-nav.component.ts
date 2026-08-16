import { Component, HostListener, inject, signal } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { RouterLink, RouterLinkActive } from "@angular/router";
import { SubscriptionService } from "../../services/subscription.service";

@Component({
  selector: "app-mobile-nav",
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatIconModule],
  templateUrl: "./mobile-nav.component.html",
  styleUrl: "./mobile-nav.component.scss"
})
export class MobileNavComponent {
  private readonly _subscriptionService = inject(SubscriptionService);

  public readonly moreOpen = signal(false);

  public get canAccessOrderEntry(): boolean {
    return this._subscriptionService.currentStatus?.isActive ?? false;
  }

  public toggleMore(): void {
    this.moreOpen.update((open) => !open);
  }

  public closeMore(): void {
    this.moreOpen.set(false);
  }

  @HostListener("document:keydown.escape")
  public onEscape(): void {
    this.closeMore();
  }
}
