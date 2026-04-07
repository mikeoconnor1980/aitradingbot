export interface LiveFill {
  id: string;
  orderId: string;
  symbol: string;
  side: string;
  direction: string;
  price: number;
  size: number;
  fee: number;
  closedPnl: number;
  filledAtUtc: string;
  userId: string;
}

export interface GridCycle {
  id: string;
  gridCycleId: string;
  strategyName: string;
  symbol: string;
  anchorPrice: number;
  totalLevels: number;
  filledLevels: number;
  lifecycle: string;
  startedAtUtc: string;
  closedAtUtc: string | null;
  closeReason: string | null;
  realisedPnl: number | null;
}

export interface LiveOrder {
  id: string;
  orderId: string;
  gridCycleId: string;
  level: number;
  symbol: string;
  side: string;
  orderType: string;
  price: number;
  size: number;
  tradeType: string;
  status: string;
  placedAtUtc: string;
  filledAtUtc: string | null;
  cancelledAtUtc: string | null;
}
