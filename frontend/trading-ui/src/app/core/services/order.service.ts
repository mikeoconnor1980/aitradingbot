import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { PlaceOrderRequest, PlaceOrderResponse, TestSignResponse } from "../models/place-order.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class OrderService {
  private readonly _apiClient = inject(ApiRestClient);

  public placeOrder(request: PlaceOrderRequest): Observable<PlaceOrderResponse> {
    return this._apiClient.post<PlaceOrderResponse>("orders", request);
  }

  public testSign(): Observable<TestSignResponse> {
    return this._apiClient.post<TestSignResponse>("orders/test-sign", {});
  }
}