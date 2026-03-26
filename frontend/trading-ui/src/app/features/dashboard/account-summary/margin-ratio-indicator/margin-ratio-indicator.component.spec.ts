import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { MarginRatioIndicatorComponent } from "./margin-ratio-indicator.component";

describe("MarginRatioIndicatorComponent", () => {
  let component: MarginRatioIndicatorComponent;
  let fixture: ComponentFixture<MarginRatioIndicatorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarginRatioIndicatorComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(MarginRatioIndicatorComponent);
    component = fixture.componentInstance;
  });

  it("should create", () => {
    component.ratio = 0;
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it("should return 0 percentage for ratio 0", () => {
    component.ratio = 0;
    expect(component.percentage).toBe(0);
    expect(component.threshold.cssClass).toBe("low");
  });

  it("should cap percentage at 100", () => {
    component.ratio = 1.5;
    expect(component.percentage).toBe(100);
  });

  it("should render low threshold for ratio 0.15", () => {
    component.ratio = 0.15;
    fixture.detectChanges();

    expect(component.threshold.cssClass).toBe("low");
    expect(component.threshold.label).toBe("Low risk");
    expect(fixture.nativeElement.querySelector(".margin-ratio__bar--low")).toBeTruthy();
    expect(fixture.nativeElement.querySelector(".margin-ratio__warning-icon")).toBeFalsy();
  });

  it("should render moderate threshold for ratio 0.45", () => {
    component.ratio = 0.45;
    fixture.detectChanges();

    expect(component.threshold.cssClass).toBe("moderate");
    expect(component.threshold.label).toBe("Moderate");
    expect(fixture.nativeElement.querySelector(".margin-ratio__bar--moderate")).toBeTruthy();
    expect(fixture.nativeElement.querySelector(".margin-ratio__warning-icon")).toBeFalsy();
  });

  it("should render elevated threshold for ratio 0.70", () => {
    component.ratio = 0.7;
    fixture.detectChanges();

    expect(component.threshold.cssClass).toBe("elevated");
    expect(component.threshold.label).toBe("Elevated");
    expect(fixture.nativeElement.querySelector(".margin-ratio__bar--elevated")).toBeTruthy();
    expect(fixture.nativeElement.querySelector(".margin-ratio__warning-icon")).toBeFalsy();
  });

  it("should render critical threshold for ratio 0.90", () => {
    component.ratio = 0.9;
    fixture.detectChanges();

    const container = fixture.nativeElement.querySelector(".margin-ratio");
    const tooltipMessage = container?.getAttribute("ng-reflect-message");

    expect(component.threshold.cssClass).toBe("critical");
    expect(component.threshold.label).toBe("Critical — near liquidation");
    expect(component.isCritical).toBeTrue();
    expect(fixture.nativeElement.querySelector(".margin-ratio__bar--critical")).toBeTruthy();
    expect(fixture.nativeElement.querySelector(".margin-ratio__warning-icon")).toBeTruthy();
    expect(container.classList).toContain("margin-ratio--critical");
    expect(tooltipMessage).toContain("Critical");
  });
});