import { ComponentFixture, TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { of } from "rxjs";
import { AppComponent } from "./app.component";
import { ConnectionStatus } from "./core/models/connection-status.model";
import { HealthResponse } from "./core/models/health-response.model";
import { HealthService } from "./core/services/health.service";
import { SignalRService } from "./core/services/signalr.service";

const signalRServiceMock: Pick<SignalRService, "connectionStatus$"> = {
  connectionStatus$: of({
    source: "SignalR",
    status: "Connected",
    detail: null,
    retryCount: 0
  } as ConnectionStatus)
};

const healthServiceMock: Pick<HealthService, "health$"> = {
  health$: of({
    status: "connected",
    walletAddress: "0x1234567890abcdef1234",
    network: "testnet",
    timestamp: "2026-03-30T00:00:00Z",
    error: null
  } as HealthResponse)
};

describe("AppComponent", () => {
  let fixture: ComponentFixture<AppComponent>;
  let app: AppComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: SignalRService, useValue: signalRServiceMock },
        { provide: HealthService, useValue: healthServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    app = fixture.componentInstance;
  });

  it("should create the app", () => {
    expect(app).toBeTruthy();
  });

  it("should have the expected title", () => {
    expect(app.title).toEqual("Trading Dashboard");
  });

  it("should render title", () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector("h1")?.textContent).toContain("Trading Dashboard");
  });

  it("should not have a Connection nav link", () => {
    fixture.detectChanges();

    const navLinks = Array.from(fixture.nativeElement.querySelectorAll(".app-shell__link")) as HTMLAnchorElement[];
    const linkTexts = navLinks.map((link: HTMLAnchorElement) => link.textContent?.trim() ?? "");

    expect(linkTexts).not.toContain("Connection");
  });

  it("should have exactly 6 nav links including Optimizer and Strategies", () => {
    fixture.detectChanges();

    const navLinks = Array.from(fixture.nativeElement.querySelectorAll(".app-shell__link")) as HTMLAnchorElement[];
    const linkTexts = navLinks.map((link: HTMLAnchorElement) => link.textContent?.trim() ?? "");

    expect(navLinks.length).toBe(6);
    expect(linkTexts).toContain("Optimizer");
    expect(linkTexts).toContain("Strategies");
  });

  it("should render the status pill as a link to /connection", () => {
    fixture.detectChanges();

    const pill = fixture.nativeElement.querySelector(".app-shell__status") as HTMLAnchorElement;

    expect(pill.tagName).toBe("A");
    expect(pill.getAttribute("href") ?? "").toContain("/connection");
  });

  it("should show the testnet label when the wallet health is connected to testnet", () => {
    fixture.detectChanges();

    const label = fixture.nativeElement.querySelector(".app-shell__status-label") as HTMLElement;

    expect(label.textContent?.trim()).toBe("Testnet");
  });
});
