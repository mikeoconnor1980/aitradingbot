export interface MarketInfo {
  asset: string;
  midPrice: number;
  markPrice: number;
  indexPrice: number;
  fundingRate: number;
  volume24h: number;
  openInterest: number;
  priceChange24hPercent: number;
}