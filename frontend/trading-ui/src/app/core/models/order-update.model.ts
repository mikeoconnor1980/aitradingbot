export interface OrderUpdate {
  timestamp: string;
  orderId: string;
  asset: string;
  status: string;
  filledSize: number;
  remainingSize: number;
}
