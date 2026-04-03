import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { ConfidenceBadgeComponent } from "./confidence-badge.component";

describe("ConfidenceBadgeComponent", () => {
  let fixture: ComponentFixture<ConfidenceBadgeComponent>;
  let component: ConfidenceBadgeComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfidenceBadgeComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfidenceBadgeComponent);
    component = fixture.componentInstance;
  });

  it("should classify high confidence correctly", () => {
    component.confidence = 0.91;
    fixture.detectChanges();

    expect(component.level).toBe("high");
    expect(fixture.nativeElement.textContent).toContain("91% confidence");
  });

  it("should show warning text for low confidence", () => {
    component.confidence = 0.4;
    fixture.detectChanges();

    expect(component.level).toBe("low");
    expect(fixture.nativeElement.textContent).toContain("needs extra review");
  });
});