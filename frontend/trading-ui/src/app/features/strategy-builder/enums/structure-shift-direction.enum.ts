import { StructureShiftDirection } from "../models/strategy.model";

export interface StructureShiftDirectionOption {
  value: StructureShiftDirection;
  label: string;
}

export const STRUCTURE_SHIFT_DIRECTIONS: StructureShiftDirectionOption[] = [
  { value: "bullish", label: "Bullish" },
  { value: "bearish", label: "Bearish" },
];