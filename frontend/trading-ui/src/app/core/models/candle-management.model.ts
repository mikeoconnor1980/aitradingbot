export interface AllCandleCoverageResponse {
  symbols: SymbolCoverage[];
}

export interface SymbolCoverage {
  symbol: string;
  intervals: IntervalCoverageDetail[];
}

export interface IntervalCoverageDetail {
  interval: string;
  from: string | null;
  to: string | null;
  candleCount: number;
}

export interface IngestCandlesRequest {
  symbol: string;
  intervals: string[];
  startTime?: number | null;
  endTime?: number | null;
  includeMarkPrice?: boolean;
}

export interface IngestionResult {
  totalFetched: number;
  totalInserted: number;
  totalSkipped: number;
  elapsedMs: number;
  intervals: IntervalIngestionResult[];
}

export interface IntervalIngestionResult {
  interval: string;
  fetched: number;
  inserted: number;
  skipped: number;
  earliestCandle: string | null;
  latestCandle: string | null;
  error: string | null;
}
