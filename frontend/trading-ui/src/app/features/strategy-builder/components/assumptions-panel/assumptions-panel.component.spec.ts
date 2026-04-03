import { ComponentFixture, TestBed } from "@angular/core/testing";
import { By } from "@angular/platform-browser";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { AssumptionsPanelComponent } from "./assumptions-panel.component";

describe("AssumptionsPanelComponent", () => {
  let fixture: ComponentFixture<AssumptionsPanelComponent>;
  let component: AssumptionsPanelComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssumptionsPanelComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(AssumptionsPanelComponent);
    component = fixture.componentInstance;
  });

  it("should render each assumption", () => {
    component.assumptions = [
      {
        fieldName: "timeframe",
        assumedValue: "15m",
        reason: "Default timeframe used"
      }
    ];
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain("timeframe");
    expect(text).toContain("15m");
    expect(text).toContain("Default timeframe used");
  });

  it("should emit the field name when Edit is clicked", () => {
    const emitSpy = spyOn(component.editField, "emit");
    component.assumptions = [
      {
        fieldName: "market",
        assumedValue: "BTC-USD",
        reason: "Market inferred from description"
      }
    ];
    fixture.detectChanges();

    fixture.debugElement.query(By.css("button")).nativeElement.click();

    expect(emitSpy).toHaveBeenCalledWith("market");
  });
});