export interface PriceUpdate {
  asset: string;
  lastPrice: number;
  high24h: number;
  low24h: number;
  volume24h: number;
  timestamp: number;
}
