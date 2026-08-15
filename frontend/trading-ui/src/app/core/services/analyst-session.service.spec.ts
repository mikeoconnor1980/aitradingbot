import { TestBed } from "@angular/core/testing";
import { Subject } from "rxjs";
import { AnalystService } from "./analyst.service";
import { AnalystSessionService } from "./analyst-session.service";
import { TradingAnalystResult } from "../models/analyst.model";

describe("AnalystSessionService", () => {
  let service: AnalystSessionService;
  let response$: Subject<TradingAnalystResult>;
  let analystService: jasmine.SpyObj<AnalystService>;

  beforeEach(() => {
    response$ = new Subject<TradingAnalystResult>();
    analystService = jasmine.createSpyObj<AnalystService>("AnalystService", ["analyse"]);
    analystService.analyse.and.returnValue(response$);
    TestBed.configureTestingModule({ providers: [{ provide: AnalystService, useValue: analystService }] });
    service = TestBed.inject(AnalystSessionService);
  });

  it("preserves typed context through submission and cancellation", () => {
    const context = { intent: "ExplainStrategyEntry" as const, strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59" };
    service.start(context, "Why did this strategy not enter?", "/strategies/one/edit");

    expect(analystService.analyse).toHaveBeenCalledWith("Why did this strategy not enter?", context);
    expect(service.context()).toEqual(context);
    expect(service.previousRoute()).toBe("/strategies/one/edit");

    service.cancel();

    expect(service.isLoading()).toBeFalse();
    expect(service.messages().at(-1)?.content).toBe("Analysis cancelled.");
  });

  it("retains results until an explicit new investigation", () => {
    service.submit("What is happening with BTC?");
    response$.next({ response: "BTC is trending higher.", succeeded: true, toolInvocations: [] });

    expect(service.messages().length).toBe(2);
    service.clear();
    expect(service.messages()).toEqual([]);
  });

  it("accepts only validated direct route contexts", () => {
    const valid = service.routeContext({ get: (key: string) => ({ intent: "ExplainStrategyEntry", strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59" })[key] ?? null });
    const invalid = service.routeContext({ get: () => "not-a-guid" });

    expect(valid).toEqual({ intent: "ExplainStrategyEntry", strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59" });
    expect(invalid).toBeUndefined();
  });
});