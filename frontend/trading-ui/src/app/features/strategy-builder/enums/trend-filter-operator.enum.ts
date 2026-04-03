import { TrendOperator } from "../models/strategy.model";

export interface TrendFilterOperatorOption {
  value: TrendOperator;
  label: string;
}

export const TREND_FILTER_OPERATORS: TrendFilterOperatorOption[] = [
  { value: "gt", label: "Greater than (>)" },
  { value: "lt", label: "Less than (<)" },
  { value: "gte", label: "Greater than or equal (>=)" },
  { value: "lte", label: "Less than or equal (<=)" },
  { value: "cross_above", label: "Crosses above" },
  { value: "cross_below", label: "Crosses below" },
  { value: "above", label: "Above" },
  { value: "below", label: "Below" },
];

export function getTrendFilterOperatorDisplayName(operator: TrendOperator): string {
  const found = TREND_FILTER_OPERATORS.find((entry) => entry.value === operator);
  return found?.label ?? operator;
}