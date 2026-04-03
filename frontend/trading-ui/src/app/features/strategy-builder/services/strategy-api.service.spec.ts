import { HttpContext } from "@angular/common/http";
import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { ApiRestClient } from "../../../core/services/api-rest-client.service";
import { StrategyApiService } from "./strategy-api.service";

describe("StrategyApiService", () => {
  let service: StrategyApiService;
  let apiClientSpy: jasmine.SpyObj<ApiRestClient>;

  beforeEach(() => {
    apiClientSpy = jasmine.createSpyObj<ApiRestClient>("ApiRestClient", ["get", "post", "put", "delete"]);
    apiClientSpy.post.and.returnValue(of({}));

    TestBed.configureTestingModule({
      providers: [
        StrategyApiService,
        { provide: ApiRestClient, useValue: apiClientSpy }
      ]
    });

    service = TestBed.inject(StrategyApiService);
  });

  it("should post strategy text to the interpret endpoint", () => {
    const context = new HttpContext();
    service.interpretStrategy("Buy BTC on RSI 30", context).subscribe();

    expect(apiClientSpy.post).toHaveBeenCalledWith("strategies/interpret", { text: "Buy BTC on RSI 30" }, context);
  });
});