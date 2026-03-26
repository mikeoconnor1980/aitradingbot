export interface FillEvent {
  timestamp: string;
  asset: string;
  side: string;
  direction: string;
  size: number;
  price: number;
  fee: number;
  closedPnl: number;
  orderId: string;
}
