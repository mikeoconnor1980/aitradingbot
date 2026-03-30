import { CycleChartComponent } from "./cycle-chart.component";

describe("CycleChartComponent", () => {
  it("should given one filled level when grid prices are ascending then mark the highest level as filled", () => {
    const component = new CycleChartComponent();
    const summary = {
      gridCycleId: "cycle-1",
      deployTimestampUtc: 0,
      anchorPrice: 87705,
      levelsPlaced: 3,
      levelPrices: [78934.5, 83000, 87266.48],
      levelsFilled: 1,
      takeProfitPrice: 89884.47,
      stopLossPrice: null,
      exitReason: "TakeProfit",
      cyclePnl: 3,
      cycleDurationMs: 0,
      closeTimestampUtc: 0
    };

    expect((component as any)._isFilledLevel(summary, 0)).toBeFalse();
    expect((component as any)._isFilledLevel(summary, 1)).toBeFalse();
    expect((component as any)._isFilledLevel(summary, 2)).toBeTrue();
  });

  it("should given two filled levels when grid prices are ascending then mark the two highest levels as filled", () => {
    const component = new CycleChartComponent();
    const summary = {
      gridCycleId: "cycle-1",
      deployTimestampUtc: 0,
      anchorPrice: 87705,
      levelsPlaced: 4,
      levelPrices: [78934.5, 81000, 84000, 87266.48],
      levelsFilled: 2,
      takeProfitPrice: 89884.47,
      stopLossPrice: null,
      exitReason: "TakeProfit",
      cyclePnl: 3,
      cycleDurationMs: 0,
      closeTimestampUtc: 0
    };

    expect((component as any)._isFilledLevel(summary, 0)).toBeFalse();
    expect((component as any)._isFilledLevel(summary, 1)).toBeFalse();
    expect((component as any)._isFilledLevel(summary, 2)).toBeTrue();
    expect((component as any)._isFilledLevel(summary, 3)).toBeTrue();
  });
});