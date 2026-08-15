import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { ActivatedRoute, Router, convertToParamMap } from "@angular/router";
import { Subject } from "rxjs";
import { AnalystService } from "../../core/services/analyst.service";
import { TradingAnalystResult } from "../../core/models/analyst.model";
import { AnalystSessionService } from "../../core/services/analyst-session.service";
import { AnalystPageComponent } from "./analyst-page.component";

describe("AnalystPageComponent", () => {
  let fixture: ComponentFixture<AnalystPageComponent>;
  let component: AnalystPageComponent;
  let response$: Subject<TradingAnalystResult>;
  let analystService: jasmine.SpyObj<AnalystService>;
  let router: jasmine.SpyObj<Router>;
  let routeParamMap = convertToParamMap({});

  beforeEach(async () => {
    response$ = new Subject<TradingAnalystResult>();
    analystService = jasmine.createSpyObj<AnalystService>("AnalystService", ["analyse"]);
    analystService.analyse.and.returnValue(response$);
    router = jasmine.createSpyObj<Router>("Router", ["navigate"]);
    router.navigate.and.resolveTo(true);

    await TestBed.configureTestingModule({
      imports: [AnalystPageComponent, NoopAnimationsModule],
      providers: [
        { provide: AnalystService, useValue: analystService },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { get snapshot() { return { queryParamMap: routeParamMap }; } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AnalystPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("renders shared session messages and structured evidence", () => {
    const session = TestBed.inject(AnalystSessionService);
    session.submit("What is happening with BTC?");
    response$.next({
      response: "BTC is trending higher.",
      succeeded: true,
      toolInvocations: [{
        toolCallId: "tool-1",
        toolName: "analyse_market",
        arguments: "{}",
        succeeded: true,
        duration: "00:00:00.100",
        wasCached: false,
        result: { symbol: "BTC-PERP", trend: "bullish" }
      }]
    });
    fixture.detectChanges();

    expect(analystService.analyse).toHaveBeenCalledWith("What is happening with BTC?", undefined);
    expect(fixture.nativeElement.textContent).toContain("BTC is trending higher.");
    expect(fixture.nativeElement.textContent).toContain("analyse market");
  });

  it("cancels an in-flight request", () => {
    const session = TestBed.inject(AnalystSessionService);
    session.submit("How do my positions look?");
    session.cancel();

    expect(session.isLoading()).toBeFalse();
    expect(session.messages().at(-1)?.content).toBe("Analysis cancelled.");
  });

  it("uses a validated strategy route context", () => {
    TestBed.inject(AnalystSessionService).clear();
    routeParamMap = convertToParamMap({ intent: "ExplainStrategyEntry", strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59" });
    component.ngOnInit();

    expect(analystService.analyse).toHaveBeenCalledWith("Why did this strategy not enter?", {
      intent: "ExplainStrategyEntry",
      strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59"
    });
  });
});