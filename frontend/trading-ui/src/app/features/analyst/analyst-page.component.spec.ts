import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { ActivatedRoute, Router, convertToParamMap } from "@angular/router";
import { Subject } from "rxjs";
import { AnalystService } from "../../core/services/analyst.service";
import { TradingAnalystResult } from "../../core/models/analyst.model";
import { AnalystPageComponent } from "./analyst-page.component";

describe("AnalystPageComponent", () => {
  let fixture: ComponentFixture<AnalystPageComponent>;
  let component: AnalystPageComponent;
  let response$: Subject<TradingAnalystResult>;
  let analystService: jasmine.SpyObj<AnalystService>;
  let router: jasmine.SpyObj<Router>;

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
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AnalystPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("submits a prompt and renders structured evidence", () => {
    component.prompt = "What is happening with BTC?";
    component.submit();
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
    component.submit("How do my positions look?");
    component.cancel();

    expect(component.isLoading).toBeFalse();
    expect(component.messages.at(-1)?.content).toBe("Analysis cancelled.");
  });

  it("navigates only from structured result references", () => {
    component.openReference({
      toolCallId: "tool-1",
      toolName: "analyse_market",
      arguments: "{}",
      succeeded: true,
      duration: "00:00:00.100",
      wasCached: false,
      result: { symbol: "BTC-PERP" }
    });

    expect(router.navigate).toHaveBeenCalledWith(["/market-data"], { queryParams: { symbol: "BTC-PERP" } });
  });

  it("uses a validated strategy route context", () => {
    TestBed.resetTestingModule();
    const route = { snapshot: { queryParamMap: convertToParamMap({ intent: "ExplainStrategyEntry", strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59" }) } };
    TestBed.configureTestingModule({
      imports: [AnalystPageComponent, NoopAnimationsModule],
      providers: [
        { provide: AnalystService, useValue: analystService },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: route }
      ]
    });
    const contextualFixture = TestBed.createComponent(AnalystPageComponent);
    contextualFixture.componentInstance.ngOnInit();

    expect(analystService.analyse).toHaveBeenCalledWith("Why did this strategy not enter?", {
      intent: "ExplainStrategyEntry",
      strategyId: "b34126f6-ef27-4b4e-9d94-1c8bfe264e59"
    });
  });
});