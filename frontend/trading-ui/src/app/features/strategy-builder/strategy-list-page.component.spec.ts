import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MatDialog } from "@angular/material/dialog";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { Router } from "@angular/router";
import { of } from "rxjs";
import { NotificationFacade } from "../../core/services/notification-facade.service";
import { StrategySummaryDto } from "./models/strategy.model";
import { StrategyListPageComponent } from "./strategy-list-page.component";
import { StrategyApiService } from "./services/strategy-api.service";

describe("StrategyListPageComponent", () => {
  let fixture: ComponentFixture<StrategyListPageComponent>;
  let component: StrategyListPageComponent;
  let strategyApiSpy: jasmine.SpyObj<StrategyApiService>;
  let notificationSpy: jasmine.SpyObj<NotificationFacade>;
  let dialogSpy: jasmine.SpyObj<MatDialog>;

  const strategy: StrategySummaryDto = {
    id: "strategy-1",
    name: "BTC Grid",
    market: "BTC-USD",
    timeframe: "15m",
    direction: "long",
    strategyMode: "grid",
    version: 1,
    createdAt: "2026-04-02T10:00:00Z",
    updatedAt: "2026-04-02T12:00:00Z"
  };

  function createComponent(): void {
    fixture = TestBed.createComponent(StrategyListPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  beforeEach(async () => {
    strategyApiSpy = jasmine.createSpyObj<StrategyApiService>("StrategyApiService", ["getStrategies", "deleteStrategy"]);
    notificationSpy = jasmine.createSpyObj<NotificationFacade>("NotificationFacade", ["success", "error"]);
    dialogSpy = jasmine.createSpyObj<MatDialog>("MatDialog", ["open"]);
    dialogSpy.open.and.returnValue({ afterClosed: () => of(true) } as never);
    strategyApiSpy.getStrategies.and.returnValue(of([]));
    strategyApiSpy.deleteStrategy.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [StrategyListPageComponent, NoopAnimationsModule],
      providers: [
        { provide: Router, useValue: jasmine.createSpyObj("Router", ["navigate"]) },
        { provide: StrategyApiService, useValue: strategyApiSpy },
        { provide: NotificationFacade, useValue: notificationSpy },
        { provide: MatDialog, useValue: dialogSpy }
      ]
    }).compileComponents();
  });

  it("should display the empty state when no strategies exist", () => {
    createComponent();

    const emptyState = fixture.nativeElement.querySelector(".strategy-list__state--empty") as HTMLElement;

    expect(emptyState).not.toBeNull();
    expect(emptyState.textContent).toContain("No strategies yet");
  });

  it("should render a table row for loaded strategies", () => {
    strategyApiSpy.getStrategies.and.returnValue(of([strategy]));

    createComponent();

    const content = fixture.nativeElement.textContent as string;

    expect(content).toContain("BTC Grid");
    expect(content).toContain("BTC-USD");
    expect(content).toContain("15m");
  });

  it("should delete a strategy after confirmation", () => {
    strategyApiSpy.getStrategies.and.returnValue(of([strategy]));

    createComponent();
    component.onDelete(strategy);

    expect(dialogSpy.open).toHaveBeenCalled();
    expect(strategyApiSpy.deleteStrategy).toHaveBeenCalledWith("strategy-1", jasmine.anything());
    expect(notificationSpy.success).toHaveBeenCalledWith("Strategy 'BTC Grid' deleted");
  });
});