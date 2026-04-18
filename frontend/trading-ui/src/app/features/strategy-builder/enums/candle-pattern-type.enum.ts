import { CandlePatternType } from "../models/strategy.model";

export interface CandlePatternTypeOption {
  value: CandlePatternType;
  label: string;
}

export const CANDLE_PATTERN_TYPES: CandlePatternTypeOption[] = [
  { value: "bullish_engulfing", label: "Bullish Engulfing" },
  { value: "bearish_engulfing", label: "Bearish Engulfing" },
  { value: "bullish_rejection", label: "Bullish Rejection" },
  { value: "bearish_rejection", label: "Bearish Rejection" },
  { value: "bullish_continuation", label: "Bullish Continuation" },
  { value: "bearish_continuation", label: "Bearish Continuation" },
  { value: "bullish_rejection_or_engulfing", label: "Bullish Rejection or Engulfing" },
  { value: "bearish_rejection_or_engulfing", label: "Bearish Rejection or Engulfing" },
];