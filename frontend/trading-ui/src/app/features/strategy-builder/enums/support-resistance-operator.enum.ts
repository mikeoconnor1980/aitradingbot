import { SupportResistanceOperator } from "../models/strategy.model";

export interface SupportResistanceOperatorOption {
  value: SupportResistanceOperator;
  label: string;
}

export const SUPPORT_RESISTANCE_OPERATORS: SupportResistanceOperatorOption[] = [
  { value: "near_support", label: "Near support level" },
  { value: "near_resistance", label: "Near resistance level" },
  { value: "above_support", label: "Above support level" },
  { value: "below_resistance", label: "Below resistance level" },
  { value: "bounce_support", label: "Bounce off support" },
  { value: "bounce_resistance", label: "Bounce off resistance" },
];
