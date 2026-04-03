import { StrategyConfig } from "./strategy.model";

export interface StrategyIntentDto {
  config: StrategyConfig;
  confidence: number;
  assumptions: AssumptionDto[];
  clarificationNeeded: string | null;
}

export interface AssumptionDto {
  fieldName: string;
  assumedValue: string;
  reason: string;
}