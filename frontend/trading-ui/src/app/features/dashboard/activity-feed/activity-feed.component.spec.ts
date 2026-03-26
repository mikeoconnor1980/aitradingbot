import { ComponentFixture, TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { AccountStateService } from "../../../core/services/account-state.service";
import { HyperliquidApiService } from "../../../core/services/hyperliquid-api.service";
import { FillEvent } from "../../../core/models/fill-event.model";
import { ActivityFeedComponent } from "./activity-feed.component";

const fillSeed: FillEvent[] = [
  {
    timestamp: "2026-03-26T20:42:16Z",
    asset: "BTC",
    side: "Sell",
    direction: "Open Short",
    size: 0.01489,
    price: 69062,
    fee: 0.4627,
    closedPnl: 0,
    orderId: "50499724853"
  },
  {
    timestamp: "2026-03-26T20:42:16Z",
    asset: "BTC",
    side: "Sell",
    direction: "Open Short",
    size: 0.00511,
    price: 69062,
    fee: 0.1588,
    closedPnl: 0,
    orderId: "50499724853"
  },
  {
    timestamp: "2026-03-26T20:17:19Z",
    asset: "SUI",
    side: "Buy",
    direction: "Open Long",
    size: 100,
    price: 0.92845,
    fee: 0.01,
    closedPnl: 0,
    orderId: "12345"
  },
  {
    timestamp: "2026-03-26T20:16:53Z",
    asset: "ADA",
    side: "Sell",
    direction: "Close Long",
    size: 50,
    price: 0.25537,
    fee: 0.01,
    closedPnl: 12.34,
    orderId: "67890"
  }
];

describe("ActivityFeedComponent", () => {
  let component: ActivityFeedComponent;
  let fixture: ComponentFixture<ActivityFeedComponent>;

  beforeEach(async () => {
    const accountState = new AccountStateService();
    accountState.seedFillEvents(fillSeed);

    await TestBed.configureTestingModule({
      imports: [ActivityFeedComponent],
      providers: [
        { provide: AccountStateService, useValue: accountState },
        { provide: HyperliquidApiService, useValue: { getRecentFills: () => of(fillSeed) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ActivityFeedComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should render Hyperliquid direction labels for fill events", () => {
    const badgeElements = Array.from(fixture.nativeElement.querySelectorAll(".activity-feed__badge")) as HTMLElement[];
    const badges = badgeElements.map((element) => element.textContent?.trim());

    expect(badges).toEqual(["Open Short", "Open Long", "Close Long"]);
    expect(badgeElements[0].classList.contains("activity-feed__badge--open-short")).toBeTrue();
    expect(badgeElements[1].classList.contains("activity-feed__badge--open-long")).toBeTrue();
    expect(badgeElements[2].classList.contains("activity-feed__badge--close-long")).toBeTrue();
  });

  it("should render realized pnl in a dedicated column", () => {
    const pnlCells = Array.from(fixture.nativeElement.querySelectorAll(".activity-feed__pnl"))
      .map((element) => (element as HTMLElement).textContent?.trim());

    expect(component.displayEvents[2].description).toBe("Sell 50.00 ADA @ 0.25537");
    expect(pnlCells).toEqual(["—", "—", "$+12.34"]);
  });

  it("should aggregate fills from the same order into a single summary row", () => {
    expect(component.displayEvents).toHaveSize(3);
    expect(component.displayEvents[0].description).toBe("Sell 0.02 BTC @ 69062");
    expect(component.displayEvents[0].orderId).toBe("50499724853");
  });

  it("should show itemized fills in an expandable details row", () => {
    const toggle = fixture.nativeElement.querySelector(".activity-feed__details-toggle") as HTMLButtonElement;

    toggle.click();
    fixture.detectChanges();

    const detailText = fixture.nativeElement.querySelector(".activity-feed__details-cell")?.textContent;
    const itemRows = fixture.nativeElement.querySelectorAll(".activity-feed__items-table tbody tr");

    expect(detailText).toContain("Order ID");
    expect(detailText).toContain("50499724853");
    expect(itemRows.length).toBe(2);
  });
});