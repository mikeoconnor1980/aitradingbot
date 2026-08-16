import { ComponentFixture, TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { MobileNavComponent } from "./mobile-nav.component";
import { SubscriptionService } from "../../services/subscription.service";

describe("MobileNavComponent", () => {
  let fixture: ComponentFixture<MobileNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MobileNavComponent],
      providers: [
        provideRouter([]),
        { provide: SubscriptionService, useValue: { currentStatus: { isActive: true } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MobileNavComponent);
    fixture.detectChanges();
  });

  it("renders the four primary mobile destinations", () => {
    const elements = fixture.nativeElement.querySelectorAll(".mobile-nav__label") as NodeListOf<Element>;
    const labels = Array.from(elements)
      .map((element: Element) => element.textContent?.trim());

    expect(labels).toEqual(["Overview", "Markets", "Trade", "More"]);
  });

  it("opens and closes the accessible More sheet", () => {
    const moreButton = fixture.nativeElement.querySelector("button.mobile-nav__tab") as HTMLButtonElement;
    moreButton.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.moreOpen()).toBeTrue();
    expect(fixture.nativeElement.querySelector(".mobile-more")?.getAttribute("role")).toBe("dialog");

    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    fixture.detectChanges();

    expect(fixture.componentInstance.moreOpen()).toBeFalse();
  });
});
