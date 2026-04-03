import { PriceVsEmaOperator } from "../models/strategy.model";

export interface PriceVsEmaOperatorOption {
  value: PriceVsEmaOperator;
  label: string;
}

export const PRICE_VS_EMA_OPERATORS: PriceVsEmaOperatorOption[] = [
  { value: "near", label: "Near (within distance)" },
  { value: "above", label: "Above" },
  { value: "below", label: "Below" },
  { value: "cross_above", label: "Cross above" },
  { value: "cross_below", label: "Cross below" },
  { value: "touch", label: "Touch (wick)" },
];