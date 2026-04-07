export interface LlmContextDto {
  symbol: string;
  marketSentiment: string;
  macroRegime: string;
  eventRisk: string;
  confidence: number;
  derivedRegime: string;
  summary: string;
  generatedAtUtc: number;
}
