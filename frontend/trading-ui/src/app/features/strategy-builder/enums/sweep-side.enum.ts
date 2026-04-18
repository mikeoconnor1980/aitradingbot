import { SweepSide } from "../models/strategy.model";

export interface SweepSideOption {
  value: SweepSide;
  label: string;
}

export const SWEEP_SIDES: SweepSideOption[] = [
  { value: "upside", label: "Upside" },
  { value: "downside", label: "Downside" },
];