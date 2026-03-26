import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ModifyOrderDto } from "../models/modify-order.model";
import { PlaceOrderRequest, PlaceOrderResponse, TestSignResponse } from "../models/place-order.model";
import { SetLeverageRequest } from "../models/set-leverage.model";
import { TradableAsset } from "../models/tradable-asset.model";
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

  public setLeverage(request: SetLeverageRequest): Observable<void> {
    return this._apiClient.put<void>("orders/leverage", request);
  }
}