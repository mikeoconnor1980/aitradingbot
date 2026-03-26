export interface Position {
  asset: string;
  size: number;
  side: string;
  entryPrice: number;
  markPrice: number;
  unrealisedPnl: number;
  unrealisedPnlPercent: number;
  liquidationPrice: number;
  leverage: number;
  marginMode: string;
}