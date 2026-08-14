import { ComponentFixture, TestBed } from "@angular/core/testing";
import { signal } from "@angular/core";
import { AccountSummaryComponent } from "./account-summary.component";
import { LayoutService } from "../../../core/services/layout.service";

describe("AccountSummaryComponent", () => {
  let fixture: ComponentFixture<AccountSummaryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AccountSummaryComponent],
      providers: [{ provide: LayoutService, useValue: { isMobile: signal(false) } }]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountSummaryComponent);
    fixture.componentInstance.summary = {
      equity: 12500.5,
      availableMargin: 8400,
      crossMarginRatio: 0.24,
      maintenanceMargin: 620,
      unrealisedPnl: -125.25
    };
    fixture.detectChanges();
  });

  it("presents equity and unrealised PnL as the primary account values", () => {
    const labelElements = fixture.nativeElement.querySelectorAll(".account-summary__primary .account-summary__label") as NodeListOf<Element>;
    const valueElements = fixture.nativeElement.querySelectorAll(".account-summary__primary-value") as NodeListOf<Element>;
    const labels = Array.from(labelElements)
      .map((element: Element) => element.textContent?.trim());
    const values = Array.from(valueElements)
      .map((element: Element) => element.textContent?.replace(/\s+/g, "").trim());

    expect(labels).toEqual(["Equity", "Unrealised P&L"]);
    expect(values[0]).toContain("$12,500.50");
    expect(values[1]).toContain("-$125.25");
  });

  it("pairs risk labels with their values", () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? "";

    expect(text).toContain("Available margin");
    expect(text).toContain("Cross-margin ratio");
    expect(text).toContain("Drawdown");
  });
});
