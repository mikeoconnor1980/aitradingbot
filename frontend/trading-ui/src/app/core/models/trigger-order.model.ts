import { PlaceOrderResponse } from "./place-order.model";

export interface PlaceTriggerOrderRequest {
  asset: string;
  side: "buy" | "sell";
  size: number;
  triggerPrice: number;
  tpslType: "sl" | "tp";
}

export interface ModifyTriggerOrderDto {
  triggerPrice: number;
  size: number;
}

export type PlaceTriggerOrderResponse = PlaceOrderResponse;