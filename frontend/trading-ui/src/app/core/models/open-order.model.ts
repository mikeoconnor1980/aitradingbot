export interface OpenOrder {
  orderId: string;
  asset: string;
  side: string;
  price: number;
  size: number;
  orderType: string;
  status: string;
  triggerPrice?: number | null;
  tpslType?: "sl" | "tp" | null;
  isReduceOnly?: boolean;
}