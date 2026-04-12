export interface PortfolioHeat {
  heatPercent: number;
  maxHeatPercent: number;
  equity: number;
  positions: PortfolioHeatPosition[];
}

export interface PortfolioHeatPosition {
  symbol: string;
  riskUsd: number;
  riskPercent: number;
}