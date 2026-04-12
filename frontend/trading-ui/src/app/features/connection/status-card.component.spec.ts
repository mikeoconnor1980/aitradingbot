import { ComponentFixture, TestBed } from "@angular/core/testing";
import { provideRouter } from "@angular/router";
import { BehaviorSubject } from "rxjs";
import { ConnectionStatus } from "../../core/models/connection-status.model";
import { HealthResponse } from "../../core/models/health-response.model";
import { HealthService } from "../../core/services/health.service";
import { SignalRService } from "../../core/services/signalr.service";
import { StatusCardComponent } from "./status-card.component";

describe("StatusCardComponent", () => {
  let component: StatusCardComponent;
  let fixture: ComponentFixture<StatusCardComponent>;
  let healthState: BehaviorSubject<HealthResponse | null>;
  let signalRTransportState: BehaviorSubject<ConnectionStatus>;
  let healthServiceMock: Pick<HealthService, "health$" | "refresh">;
  let signalRServiceMock: Pick<SignalRService, "transportConnectionStatus$">;

  const connectedHealth: HealthResponse = {
    status: "connected",
    walletAddress: "0x1234567890abcdef1234",
    network: "testnet",
    timestamp: "2026-03-30T00:00:00Z",
    error: null
  };

  beforeEach(async () => {
    healthState = new BehaviorSubject<HealthResponse | null>(connectedHealth);
    signalRTransportState = new BehaviorSubject<ConnectionStatus>({
      source: "SignalR",
      status: "Connected",
      detail: null,
      retryCount: 0
    });

    healthServiceMock = {
      health$: healthState.asObservable(),
      refresh: jasmine.createSpy("refresh")
    };

    signalRServiceMock = {
      transportConnectionStatus$: signalRTransportState.asObservable()
    };

    await TestBed.configureTestingModule({
      imports: [StatusCardComponent],
      providers: [
        provideRouter([]),
        { provide: HealthService, useValue: healthServiceMock },
        { provide: SignalRService, useValue: signalRServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StatusCardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should create", () => {
    expect(component).toBeTruthy();
  });

  it("should render shell-aligned connection panels", () => {
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain("Connection Health");
    expect(compiled.textContent).toContain("Wallet / Hyperliquid");
    expect(compiled.textContent).toContain("SignalR");
    expect(compiled.textContent).toContain("Execution connection");
    expect(compiled.textContent).toContain("Live transport");
    expect(compiled.textContent).toContain("0x1234...1234");
    expect(compiled.textContent).toContain("testnet");
    expect(compiled.textContent).toContain("Testnet");
    expect(compiled.textContent).toContain("2026-03-30 00:00 UTC");
    expect(compiled.textContent).toContain("Live updates are flowing through the SignalR hub.");
  });

  it("should render reconnecting SignalR details when retries are in progress", () => {
    signalRTransportState.next({
      source: "SignalR",
      status: "Reconnecting",
      detail: "Retrying after transport error",
      retryCount: 2
    });

    fixture.detectChanges();

    const signalRSection = fixture.nativeElement.querySelector('[aria-label="SignalR connection state"]') as HTMLElement;
    const badge = signalRSection.querySelector(".status-card__badge") as HTMLElement;

    expect(signalRSection.textContent).toContain("Reconnecting");
    expect(signalRSection.textContent).toContain("Retry Count");
    expect(signalRSection.textContent).toContain("2");
    expect(signalRSection.textContent).toContain("2 retries");
    expect(signalRSection.textContent).toContain("Retrying after transport error");
    expect(badge.classList).toContain("status-card__badge--reconnecting");
  });

  it("should render disconnected SignalR details when the hub is offline", () => {
    signalRTransportState.next({
      source: "SignalR",
      status: "Disconnected",
      detail: "Initial connection failed after all retry attempts",
      retryCount: 6
    });

    fixture.detectChanges();

    const signalRSection = fixture.nativeElement.querySelector('[aria-label="SignalR connection state"]') as HTMLElement;
    const badge = signalRSection.querySelector(".status-card__badge") as HTMLElement;

    expect(signalRSection.textContent).toContain("Disconnected");
    expect(signalRSection.textContent).toContain("6 retries");
    expect(signalRSection.textContent).toContain("Initial connection failed after all retry attempts");
    expect(badge.classList).toContain("status-card__badge--disconnected");
  });

  it("should render backend errors in the wallet panel", () => {
    healthState.next({
      status: "disconnected",
      walletAddress: "",
      network: "",
      timestamp: "",
      error: "Failed to reach backend API"
    });

    fixture.detectChanges();

    const walletSection = fixture.nativeElement.querySelector('[aria-label="Wallet and Hyperliquid connection"]') as HTMLElement;

    expect(walletSection.textContent).toContain("Disconnected");
    expect(walletSection.textContent).toContain("Failed to reach backend API");
    expect(walletSection.textContent).toContain("N/A");
  });

  it("should refresh backend health when the button is clicked", () => {
    const button = fixture.nativeElement.querySelector(".status-card__refresh") as HTMLButtonElement;

    button.click();

    expect(healthServiceMock.refresh).toHaveBeenCalled();
  });

  it("should use connected wording for mainnet badges", () => {
    healthState.next({
      status: "connected",
      walletAddress: "0x1234567890abcdef1234",
      network: "mainnet",
      timestamp: "2026-03-30T00:00:00Z",
      error: null
    });

    fixture.detectChanges();

    const walletSection = fixture.nativeElement.querySelector('[aria-label="Wallet and Hyperliquid connection"]') as HTMLElement;

    expect(walletSection.textContent).toContain("Connected");
  });
});
