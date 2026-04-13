export interface DrawdownState {
  drawdownPercent: number;
  highWaterMark: number;
  scalingFactor: number;
  isCircuitBreakerActive: boolean;
}