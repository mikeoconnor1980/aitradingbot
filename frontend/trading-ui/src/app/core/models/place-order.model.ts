export interface PlaceOrderRequest {
  asset: string;
  side: "buy" | "sell";
  orderType: "market" | "limit";
  price: number | null;
  size: number;
}

export interface PlaceOrderResponse {
  success: boolean;
  orderId: string | null;
  status: string | null;
  detail: string | null;
}

export interface SignatureInfo {
  v: number;
  r: string;
  s: string;
}

export interface TestSignResponse {
  domainSeparator: string;
  typeHash: string;
  messageHash: string;
  signature: SignatureInfo;
}

export interface CloseAllProgress {
  readonly completed: number;
  readonly succeeded: number;
  readonly failed: number;
  readonly total: number;
}