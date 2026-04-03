import { MacdOperator } from "../models/strategy.model";

export interface MacdOperatorOption {
  value: MacdOperator;
  label: string;
}

export const MACD_OPERATORS: MacdOperatorOption[] = [
  { value: "cross_above", label: "MACD crosses above signal" },
  { value: "cross_below", label: "MACD crosses below signal" },
  { value: "gt", label: "MACD greater than signal" },
  { value: "lt", label: "MACD less than signal" },
];