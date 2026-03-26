export interface FillEvent {
  timestamp: string;
  asset: string;
  side: string;
  size: number;
  price: number;
  fee: number;
  orderId: string;
}
