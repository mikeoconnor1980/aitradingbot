import { ComponentFixture, TestBed } from "@angular/core/testing";
import { CycleNarrativeComponent } from "./cycle-narrative.component";
import { BacktestDebugResponse, OrderEventType } from "../../../core/models/backtest-debug.model";

describe("CycleNarrativeComponent", () => {
  let component: CycleNarrativeComponent;
  let fixture: ComponentFixture<CycleNarrativeComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CycleNarrativeComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CycleNarrativeComponent);
    component = fixture.componentInstance;
  });

  it("should given missing buy fill events when summary has filled levels then show the summary fallback line", () => {
    fixture.componentRef.setInput("debugData", createDebugData({
      gridCycleSummary: {
        levelsPlaced: 20,
        levelsFilled: 1,
        exitReason: "StopLoss",
        cyclePnl: -12
      },
      orderEvents: []
    }));

    fixture.detectChanges();

    expect(component.narrativeLines.some((line) => line.text === "1 of 20 levels filled before exit.")).toBeTrue();
  });

  it("should given no filled levels when summary has zero fills then show the no-fill message", () => {
    fixture.componentRef.setInput("debugData", createDebugData({
      gridCycleSummary: {
        levelsPlaced: 10,
        levelsFilled: 0,
        exitReason: "TakeProfit",
        cyclePnl: 0
      },
      orderEvents: []
    }));

    fixture.detectChanges();

    expect(component.narrativeLines.some((line) => line.text === "No grid levels were filled before exit. Price never traded down to the resting buy levels.")).toBeTrue();
  });

  it("should given a buy fill event when summary exists then describe the fill event", () => {
    fixture.componentRef.setInput("debugData", createDebugData({
      gridCycleSummary: {
        levelsPlaced: 10,
        levelsFilled: 1,
        exitReason: "TakeProfit",
        cyclePnl: 8
      },
      orderEvents: [
        {
          timestampUtc: Date.parse("2026-01-21T20:15:00Z"),
          eventType: OrderEventType.Filled,
          orderId: "fill-1",
          side: "Buy",
          orderType: "Limit",
          price: 89561.74,
          size: 0.0011,
          fillPrice: 89561.74,
          fee: 0.0100,
          isMaker: true,
          cancellationReason: null,
          gridCycleId: "cycle-1"
        }
      ]
    }));

    fixture.detectChanges();

    expect(component.narrativeLines.some((line) => line.text.includes("First position opened on"))).toBeTrue();
    expect(component.narrativeLines.some((line) => line.text.includes("filling level 1 of 10"))).toBeTrue();
  });

  it("should given delayed first fill when cycle closes then show both cycle and hold durations", () => {
    fixture.componentRef.setInput("debugData", createDebugData({
      gridCycleSummary: {
        deployTimestampUtc: Date.parse("2026-01-01T00:00:00Z"),
        closeTimestampUtc: Date.parse("2026-01-21T19:30:00Z"),
        cycleDurationMs: ((20 * 24) + 19) * 60 * 60 * 1000,
        exitReason: "TakeProfit",
        takeProfitPrice: 89884.47,
        levelsPlaced: 20,
        levelsFilled: 1,
        cyclePnl: 3
      },
      orderEvents: [
        {
          timestampUtc: Date.parse("2026-01-21T17:00:00Z"),
          eventType: OrderEventType.Filled,
          orderId: "fill-1",
          side: "Buy",
          orderType: "Limit",
          price: 87266.48,
          size: 0.0011,
          fillPrice: 87266.48,
          fee: 0.0100,
          isMaker: true,
          cancellationReason: null,
          gridCycleId: "cycle-1"
        }
      ]
    }));

    fixture.detectChanges();

    expect(component.narrativeLines.some((line) => line.text.includes("resting buy orders below the market"))).toBeTrue();
    expect(component.narrativeLines.some((line) => line.text.includes("The open position had been active for 2h 30m."))).toBeTrue();
  });

  it("should given a market buy fill when summary exists then describe it as an initial market buy", () => {
    fixture.componentRef.setInput("debugData", createDebugData({
      gridCycleSummary: {
        levelsPlaced: 10,
        levelsFilled: 1,
        exitReason: "TakeProfit",
        cyclePnl: 8
      },
      orderEvents: [
        {
          timestampUtc: Date.parse("2026-01-21T20:15:00Z"),
          eventType: OrderEventType.Filled,
          orderId: "fill-1",
          side: "Buy",
          orderType: "Market",
          price: 90011.8,
          size: 0.0011,
          fillPrice: 90020.0,
          fee: 0.0100,
          isMaker: false,
          cancellationReason: null,
          gridCycleId: "cycle-1"
        }
      ]
    }));

    fixture.detectChanges();

    expect(component.narrativeLines.some((line) => line.text.includes("initial market buy"))).toBeTrue();
  });
});

function createDebugData(overrides: {
  gridCycleSummary?: Partial<NonNullable<BacktestDebugResponse["gridCycleSummary"]>>;
  orderEvents?: BacktestDebugResponse["orderEvents"];
} = {}): BacktestDebugResponse {
  return {
    cycleId: "cycle-1",
    candleEvaluations: [],
    orderEvents: overrides.orderEvents ?? [],
    gridCycleSummary: {
      gridCycleId: "cycle-1",
      deployTimestampUtc: Date.parse("2026-01-21T19:30:00Z"),
      anchorPrice: 90011.8,
      levelsPlaced: 10,
      levelPrices: [89561.74, 89111.68],
      levelsFilled: 0,
      takeProfitPrice: 92248.59,
      stopLossPrice: 78817.1,
      exitReason: "Unknown",
      cyclePnl: 0,
      cycleDurationMs: 1000,
      closeTimestampUtc: Date.parse("2026-01-31T17:00:00Z"),
      ...(overrides.gridCycleSummary ?? {})
    }
  };
}