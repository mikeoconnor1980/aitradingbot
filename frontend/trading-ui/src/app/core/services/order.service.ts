import { Injectable, inject } from "@angular/core";
import { Observable, catchError, concat, map, of, scan } from "rxjs";
import { HttpContext } from "@angular/common/http";
import { ModifyOrderDto } from "../models/modify-order.model";
import { CloseAllProgress, PlaceOrderRequest, PlaceOrderResponse, TestSignResponse } from "../models/place-order.model";
import { SetLeverageRequest } from "../models/set-leverage.model";
import { TradableAsset } from "../models/tradable-asset.model";
import { ModifyTriggerOrderDto, PlaceTriggerOrderRequest, PlaceTriggerOrderResponse } from "../models/trigger-order.model";
import { Position } from "../models/position.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class OrderService {
  private readonly _apiClient = inject(ApiRestClient);

  public getAvailableAssets(): Observable<TradableAsset[]> {
    return this._apiClient.get<TradableAsset[]>("orders/assets");
  }

  public placeOrder(request: PlaceOrderRequest): Observable<PlaceOrderResponse> {
    return this._apiClient.post<PlaceOrderResponse>("orders", request);
  }

  public testSign(): Observable<TestSignResponse> {
    return this._apiClient.post<TestSignResponse>("orders/test-sign", {});
  }

  public cancelOrder(orderId: string): Observable<void> {
    return this._apiClient.delete<void>(`orders/${orderId}`);
  }

  public cancelAllOrders(asset: string): Observable<void> {
    return this._apiClient.delete<void>(`orders?asset=${encodeURIComponent(asset)}`);
  }

  public modifyOrder(orderId: string, dto: ModifyOrderDto): Observable<void> {
    return this._apiClient.put<void>(`orders/${orderId}`, dto);
  }

  public placeTriggerOrder(request: PlaceTriggerOrderRequest): Observable<PlaceTriggerOrderResponse> {
    return this._apiClient.post<PlaceTriggerOrderResponse>("orders/trigger", request);
  }

  public modifyTriggerOrder(orderId: string, dto: ModifyTriggerOrderDto): Observable<void> {
    return this._apiClient.put<void>(`orders/trigger/${orderId}`, dto);
  }

  public cancelTriggerOrder(orderId: string): Observable<void> {
    return this._apiClient.delete<void>(`orders/trigger/${orderId}`);
  }

  public setLeverage(request: SetLeverageRequest, context?: HttpContext): Observable<void> {
    return this._apiClient.put<void>("orders/leverage", request, context);
  }

  public closeAllPositions(positions: Position[]): Observable<CloseAllProgress> {
    const closeRequests = positions.map((position) => {
      const closeSide: "buy" | "sell" = position.side === "Long" ? "sell" : "buy";
      const request: PlaceOrderRequest = {
        asset: position.asset,
        side: closeSide,
        orderType: "market",
        price: null,
        size: Math.abs(position.size)
      };

      return this.placeOrder(request).pipe(
        map(() => true as const),
        catchError(() => of(false as const))
      );
    });

    return concat(...closeRequests).pipe(
      scan(
        (progress, success) => ({
          completed: progress.completed + 1,
          succeeded: progress.succeeded + (success ? 1 : 0),
          failed: progress.failed + (success ? 0 : 1),
          total: positions.length
        }),
        {
          completed: 0,
          succeeded: 0,
          failed: 0,
          total: positions.length
        } as CloseAllProgress
      )
    );
  }
}