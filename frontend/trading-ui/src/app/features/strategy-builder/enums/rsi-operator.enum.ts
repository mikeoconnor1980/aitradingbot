import { RsiOperator } from "../models/strategy.model";

export interface RsiOperatorOption {
  value: RsiOperator;
  label: string;
}

export const RSI_OPERATORS: RsiOperatorOption[] = [
  { value: "lt", label: "Less than (<)" },
  { value: "lte", label: "Less than or equal (<=)" },
  { value: "gt", label: "Greater than (>)" },
  { value: "gte", label: "Greater than or equal (>=)" },
  { value: "cross_above", label: "Crosses above" },
  { value: "cross_below", label: "Crosses below" },
];

export function getRsiOperatorDisplayName(operator: RsiOperator): string {
  const found = RSI_OPERATORS.find((entry) => entry.value === operator);
  return found?.label ?? operator;
}