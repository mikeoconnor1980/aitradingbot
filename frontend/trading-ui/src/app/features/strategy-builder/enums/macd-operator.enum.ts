import { MacdOperator } from "../models/strategy.model";

export interface MacdOperatorOption {
  value: MacdOperator;
  label: string;
}

export const MACD_OPERATORS: MacdOperatorOption[] = [
  { value: "cross_above_signal", label: "Crosses above signal line" },
  { value: "cross_below_signal", label: "Crosses below signal line" },
  { value: "above_zero", label: "Above zero line" },
  { value: "below_zero", label: "Below zero line" },
  { value: "histogram_rising", label: "Histogram rising" },
  { value: "histogram_falling", label: "Histogram falling" },
];