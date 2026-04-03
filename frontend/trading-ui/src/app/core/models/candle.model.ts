import { ChartIndicatorValues } from "./chart-indicator.model";

export interface Candle {
  timestamp: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  indicators?: ChartIndicatorValues | null;
}