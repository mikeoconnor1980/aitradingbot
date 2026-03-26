import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting } from "@angular/common/http/testing";
import { of, throwError } from "rxjs";
import { Position } from "../models/position.model";
import { CloseAllProgress } from "../models/place-order.model";
import { OrderService } from "./order.service";

const mockPositions: Position[] = [
  {
    asset: "BTC",
    side: "Long",
    size: 0.001,
    entryPrice: 50000,
    markPrice: 51000,
    unrealisedPnl: 64.13,
    unrealisedPnlPercent: 12.8,
    liquidationPrice: 40000,
    leverage: 10,
    marginMode: "cross",
    marginUsed: 5.1,
    fundingRate: -0.0001
  },
  {
    asset: "ETH",
    side: "Short",
    size: 0.5,
    entryPrice: 3000,
    markPrice: 3050,
    unrealisedPnl: -22.19,
    unrealisedPnlPercent: -1.5,
    liquidationPrice: 3500,
    leverage: 5,
    marginMode: "cross",
    marginUsed: 305,
    fundingRate: 0.0002
  }
];

describe("OrderService", () => {
  let service: OrderService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(OrderService);
  });

  describe("closeAllPositions", () => {
    it("should emit progress for each position", () => {
      spyOn(service, "placeOrder").and.returnValues(
        of({ success: true, orderId: "1", status: "filled", detail: null }),
        of({ success: true, orderId: "2", status: "filled", detail: null })
      );

      const emissions: CloseAllProgress[] = [];
      service.closeAllPositions(mockPositions).subscribe((progress) => emissions.push(progress));

      expect(service.placeOrder).toHaveBeenCalledTimes(2);
      expect(service.placeOrder).toHaveBeenCalledWith(
        jasmine.objectContaining({ asset: "BTC", side: "sell", orderType: "market", price: null, size: 0.001 })
      );
      expect(service.placeOrder).toHaveBeenCalledWith(
        jasmine.objectContaining({ asset: "ETH", side: "buy", orderType: "market", price: null, size: 0.5 })
      );
      expect(emissions.length).toBe(2);
      expect(emissions[0]).toEqual({ completed: 1, succeeded: 1, failed: 0, total: 2 });
      expect(emissions[1]).toEqual({ completed: 2, succeeded: 2, failed: 0, total: 2 });
    });

    it("should handle partial failures", () => {
      spyOn(service, "placeOrder").and.returnValues(
        of({ success: true, orderId: "1", status: "filled", detail: null }),
        throwError(() => new Error("fail"))
      );

      const emissions: CloseAllProgress[] = [];
      service.closeAllPositions(mockPositions).subscribe((progress) => emissions.push(progress));

      expect(emissions.length).toBe(2);
      expect(emissions[1]).toEqual({ completed: 2, succeeded: 1, failed: 1, total: 2 });
    });

    it("should report all failed when all requests fail", () => {
      spyOn(service, "placeOrder").and.returnValues(
        throwError(() => new Error("fail1")),
        throwError(() => new Error("fail2"))
      );

      const emissions: CloseAllProgress[] = [];
      service.closeAllPositions(mockPositions).subscribe((progress) => emissions.push(progress));

      expect(emissions.length).toBe(2);
      expect(emissions[1]).toEqual({ completed: 2, succeeded: 0, failed: 2, total: 2 });
    });
  });
});
