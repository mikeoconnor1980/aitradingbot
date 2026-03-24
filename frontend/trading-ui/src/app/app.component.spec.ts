import { of } from "rxjs";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { AppComponent } from "./app.component";
import { HealthService } from "./core/services/health.service";

const healthServiceMock: Pick<HealthService, "health$" | "refresh"> = {
  health$: of(null),
  refresh: () => undefined
};

describe("AppComponent", () => {
  let fixture: ComponentFixture<AppComponent>;
  let app: AppComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [{ provide: HealthService, useValue: healthServiceMock }]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    app = fixture.componentInstance;
  });

  it("should create the app", () => {
    expect(app).toBeTruthy();
  });

  it("should have the expected title", () => {
    expect(app.title).toEqual("Hyperliquid POC");
  });

  it("should render title", () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector("h1")?.textContent).toContain("Hyperliquid POC");
  });
});
