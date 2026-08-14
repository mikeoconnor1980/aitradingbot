import { ComponentFixture, TestBed } from "@angular/core/testing";
import { provideHttpClientTesting } from "@angular/common/http/testing";
import { provideHttpClient } from "@angular/common/http";
import { signal } from "@angular/core";
import { provideRouter } from "@angular/router";
import { of } from "rxjs";
import { AppComponent } from "./app.component";
import { ConnectionStatus } from "./core/models/connection-status.model";
import { HealthResponse } from "./core/models/health-response.model";
import { HealthService } from "./core/services/health.service";
import { ProfileService, UserProfile } from "./core/services/profile.service";
import { AuthService } from "./core/services/auth.service";
import { SignalRService } from "./core/services/signalr.service";
import { LayoutService } from "./core/services/layout.service";

const signalRServiceMock: Pick<SignalRService, "connectionStatus$" | "fillEvent$" | "orderUpdate$" | "executionLog$"> = {
  connectionStatus$: of({
    source: "SignalR",
    status: "Connected",
    detail: null,
    retryCount: 0
  } as ConnectionStatus),
  fillEvent$: of(),
  orderUpdate$: of(),
  executionLog$: of()
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

const profileServiceMock: Pick<ProfileService, "profile$" | "load"> = {
  profile$: of({
    id: "1",
    email: "test@test.com",
    displayName: "Test",
    preferredNetwork: "testnet",
    llmModels: { strategy: "gemini-2.5-flash-lite", review: "gemini-2.5-flash" }
  } as UserProfile),
  load: (): undefined => undefined
};

const authServiceMock = {
  user$: of({ displayName: "Test", email: "test@test.com", isAdmin: false }),
  isAuthenticated$: of(true),
  syncCurrentUser: () => of({ id: "1", displayName: "Test", email: "test@test.com", isAdmin: false })
};

const layoutServiceMock: Pick<LayoutService, "isMobile"> = {
  isMobile: signal(false)
};

describe("AppComponent", () => {
  let fixture: ComponentFixture<AppComponent>;
  let app: AppComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SignalRService, useValue: signalRServiceMock },
        { provide: HealthService, useValue: healthServiceMock },
        { provide: ProfileService, useValue: profileServiceMock },
        { provide: AuthService, useValue: authServiceMock },
        { provide: LayoutService, useValue: layoutServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    app = fixture.componentInstance;
  });

  it("should create the app", () => {
    expect(app).toBeTruthy();
  });

  it("should have the expected title", () => {
    expect(app.title).toEqual("TradePilot");
  });

  it("should render the TradePilot brand in navigation", () => {
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector(".sidebar__brand")?.textContent).toContain("TradePilot");
  });

  it("should group navigation by user intent", async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const groupHeadings = Array.from(fixture.nativeElement.querySelectorAll(".sidebar__group-title")) as HTMLElement[];
    const headingTexts = groupHeadings.map((heading: HTMLElement) =>
      (heading.textContent ?? "").trim()
    );

    expect(headingTexts).toEqual(["Monitor", "Build & Research", "Execute & Automate", "Operate"]);
  });

  it("should keep desktop routes reachable within the intent groups", async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const navLinks = Array.from(fixture.nativeElement.querySelectorAll(".sidebar__nav .sidebar__link")) as HTMLAnchorElement[];
    const linkTexts = navLinks.map((link: HTMLAnchorElement) =>
      (link.querySelector(".sidebar__label")?.textContent ?? "").trim()
    );

    expect(navLinks.length).toBe(12);
    expect(linkTexts).toContain("Optimizer");
    expect(linkTexts).toContain("Strategies");
    expect(linkTexts).toContain("Connection");
    expect(linkTexts).toContain("Order Entry");
  });

  it("should render the status pill as a link to /connection", () => {
    fixture.detectChanges();

    const pill = fixture.nativeElement.querySelector(".app-shell__status") as HTMLAnchorElement;

    expect(pill.tagName).toBe("A");
    expect(pill.getAttribute("href") ?? "").toContain("/connection");
  });

  it("should show the testnet label when the preferred network is testnet", async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const badge = fixture.nativeElement.querySelector(".app-shell__network-badge") as HTMLElement;

    expect(badge).toBeTruthy();
    expect(badge.textContent?.trim()).toBe("Testnet");
  });
});
