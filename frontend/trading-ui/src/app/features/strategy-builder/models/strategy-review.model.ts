export interface StrategyReviewDto {
  id: string;
  strategyId: string;
  revisionNumber: number;
  reviewMarkdown: string;
  modelName: string;
  isFallback: boolean;
  createdAtUtc: number;
}