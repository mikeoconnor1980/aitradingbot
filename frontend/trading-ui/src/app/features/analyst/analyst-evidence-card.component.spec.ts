import { ComponentFixture, TestBed } from "@angular/core/testing";
import { AnalystEvidenceCardComponent } from "./analyst-evidence-card.component";

describe("AnalystEvidenceCardComponent", () => {
  let fixture: ComponentFixture<AnalystEvidenceCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AnalystEvidenceCardComponent] }).compileComponents();
    fixture = TestBed.createComponent(AnalystEvidenceCardComponent);
  });

  it("renders strategy decisions and rule evidence without an execution action", () => {
    fixture.componentInstance.invocation = {
      toolCallId: "tool-1",
      toolName: "get_latest_strategy_evaluation",
      arguments: "{}",
      succeeded: true,
      duration: "00:00:00.100",
      wasCached: false,
      result: {
        decision: "no_trade",
        primaryRejectionReason: "RSI threshold not met",
        strategyName: "v10.4",
        rules: [{ id: "rule-1", name: "RSI", passed: false, actualValue: "58", expectedValue: "64", reason: "Below threshold" }]
      }
    };
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain("RSI threshold not met");
    expect(fixture.nativeElement.textContent).toContain("Below threshold");
    expect(fixture.nativeElement.querySelector("button")).toBeNull();
  });

  it("renders baseline and candidate backtest metrics without selecting a winner", () => {
    fixture.componentInstance.invocation = {
      toolCallId: "tool-2",
      toolName: "run_backtest_experiment",
      arguments: "{}",
      succeeded: true,
      duration: "00:00:00.100",
      wasCached: false,
      result: {
        baseline: { totalPnl: 120, maxDrawdownPercent: 4, totalTrades: 8, profitFactor: 1.4 },
        candidates: [{ label: "RSI 64", metrics: { totalPnl: 140, maxDrawdownPercent: 5, totalTrades: 7, profitFactor: 1.5 } }]
      }
    };
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain("RSI 64");
    expect(fixture.nativeElement.textContent).not.toContain("winner");
  });
});