export interface FearGreedStatusDto {
  latestValue: number | null;
  latestClassification: string | null;
  latestTimestamp: string | null;
  totalReadings: number;
  earliestTimestamp: string | null;
}

export interface FearGreedReadingDto {
  value: number;
  classification: string;
  timestamp: number;
}

export interface FearGreedBackfillResultDto {
  fetched: number;
  inserted: number;
}
