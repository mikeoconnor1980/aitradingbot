import { ComponentFixture, TestBed } from "@angular/core/testing";
import { Subject } from "rxjs";
import { Candle } from "../../../core/models/candle.model";
import { FillEvent } from "../../../core/models/fill-event.model";
import { PriceUpdate } from "../../../core/models/price-update.model";
import { SignalRService } from "../../../core/services/signalr.service";
import { PriceChartComponent } from "./price-chart.component";

class ResizeObserverMock {
  public observe = jasmine.createSpy("observe");
  public disconnect = jasmine.createSpy("disconnect");

  public constructor(callback: ResizeObserverCallback) {
    void callback;
  }
}

describe("PriceChartComponent", () => {
  let component: PriceChartComponent;
  let fixture: ComponentFixture<PriceChartComponent>;
  let originalResizeObserver: typeof ResizeObserver | undefined;

  const candleSeed: Candle[] = [
    { timestamp: Date.parse("2026-03-30T09:00:00Z"), open: 64800, high: 64950, low: 64780, close: 64900, volume: 120 },
    { timestamp: Date.parse("2026-03-30T10:00:00Z"), open: 64900, high: 65120, low: 64860, close: 65000, volume: 132 },
    { timestamp: Date.parse("2026-03-30T11:00:00Z"), open: 65000, high: 66100, low: 64980, close: 66000, volume: 145 },
    { timestamp: Date.parse("2026-03-30T12:00:00Z"), open: 66000, high: 66600, low: 65950, close: 66500, volume: 154 }
  ];

  const fillSeed: FillEvent[] = [
    {
      timestamp: "2026-03-30T10:12:00Z",
      asset: "BTC",
      side: "Buy",
      direction: "Open Long",
      size: 0.1,
      price: 65000,
      fee: 0.01,
      closedPnl: 0,
      orderId: "order-1"
    },
    {
      timestamp: "2026-03-30T11:33:00Z",
      asset: "BTC",
      side: "Sell",
      direction: "Close Long",
      size: 0.1,
      price: 66000,
      fee: 0.01,
      closedPnl: 100,
      orderId: "order-2"
    }
  ];

  const splitFillSeed: FillEvent[] = [
    {
      timestamp: "2026-03-30T10:12:00Z",
      asset: "BTC",
      side: "Buy",
      direction: "Open Long",
      size: 0.1,
      price: 65000,
      fee: 0.01,
      closedPnl: 0,
      orderId: "order-1"
    },
    {
      timestamp: "2026-03-30T10:24:00Z",
      asset: "BTC",
      side: "Buy",
      direction: "Open Long",
      size: 0.2,
      price: 65100,
      fee: 0.02,
      closedPnl: 0,
      orderId: "order-1b"
    }
  ];

  beforeEach(async () => {
    originalResizeObserver = globalThis.ResizeObserver;
    (globalThis as typeof globalThis & { ResizeObserver: typeof ResizeObserver }).ResizeObserver = ResizeObserverMock as never;

    const signalRMock = {
      priceUpdate$: new Subject<PriceUpdate>()
    };

    await TestBed.configureTestingModule({
      imports: [PriceChartComponent],
      providers: [
        { provide: SignalRService, useValue: signalRMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PriceChartComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput("seedCandles", candleSeed);
    fixture.componentRef.setInput("selectedAsset", "BTC-PERP");
    fixture.componentRef.setInput("selectedTimeframe", "1h");
  });

  afterEach(() => {
    if (originalResizeObserver) {
      (globalThis as typeof globalThis & { ResizeObserver: typeof ResizeObserver }).ResizeObserver = originalResizeObserver;
      return;
    }

    delete (globalThis as Partial<typeof globalThis>).ResizeObserver;
  });

  it("should given chart initialization when created then initialize the markers api", () => {
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect((component as unknown as { _markersApi: unknown })._markersApi).toBeTruthy();
  });

  it("should given buy and sell fills when inputs change then create trade markers", () => {
    fixture.detectChanges();

    const markersApi = (component as unknown as {
      _markersApi: { setMarkers: (markers: unknown[]) => void };
    })._markersApi;
    const setMarkersSpy = spyOn(markersApi, "setMarkers").and.callThrough();

    fixture.componentRef.setInput("fills", fillSeed);
    fixture.componentRef.setInput("showTradeMarkers", true);
    fixture.detectChanges();

    const markers = setMarkersSpy.calls.mostRecent().args[0] as { shape: string; color: string }[];

    expect(markers.length).toBe(2);
    expect(markers.map((marker) => marker.shape)).toEqual(["arrowUp", "arrowDown"]);
    expect(markers.map((marker) => marker.color)).toEqual(["#3bc9a8", "#caa86a"]);
  });

  it("should given split fills in the same candle and side when inputs change then create one consolidated marker", () => {
    fixture.detectChanges();

    const markersApi = (component as unknown as {
      _markersApi: { setMarkers: (markers: unknown[]) => void };
    })._markersApi;
    const setMarkersSpy = spyOn(markersApi, "setMarkers").and.callThrough();

    fixture.componentRef.setInput("fills", splitFillSeed);
    fixture.componentRef.setInput("showTradeMarkers", true);
    fixture.detectChanges();

    const markers = setMarkersSpy.calls.mostRecent().args[0] as { text: string; shape: string }[];

    expect(markers.length).toBe(1);
    expect(markers[0].shape).toBe("arrowUp");
    expect(markers[0].text).toContain("Buy 0.3000");
    expect(markers[0].text).toContain("(2)");
  });

  it("should given visible markers when toggled off then clear the rendered markers", () => {
    fixture.componentRef.setInput("fills", fillSeed);
    fixture.componentRef.setInput("showTradeMarkers", true);
    fixture.detectChanges();

    const markersApi = (component as unknown as {
      _markersApi: { setMarkers: jasmine.Spy };
    })._markersApi;
    spyOn(markersApi, "setMarkers").and.callThrough();

    fixture.componentRef.setInput("showTradeMarkers", false);
    fixture.detectChanges();

    const markers = markersApi.setMarkers.calls.mostRecent().args[0] as unknown[];
    expect(markers).toEqual([]);
  });

  it("should given existing fills when addFill is called with a split fill then keep markers consolidated", () => {
    fixture.componentRef.setInput("fills", splitFillSeed);
    fixture.componentRef.setInput("showTradeMarkers", true);
    fixture.detectChanges();

    const markersApi = (component as unknown as {
      _markersApi: { setMarkers: jasmine.Spy };
      _currentFills: FillEvent[];
    })._markersApi;
    spyOn(markersApi, "setMarkers").and.callThrough();

    component.addFill({
      timestamp: "2026-03-30T10:42:00Z",
      asset: "BTC",
      side: "Buy",
      direction: "Open Long",
      size: 0.2,
      price: 65200,
      fee: 0.02,
      closedPnl: 0,
      orderId: "order-3"
    });

    const markers = markersApi.setMarkers.calls.mostRecent().args[0] as unknown[];
    const currentFills = (component as unknown as { _currentFills: FillEvent[] })._currentFills;

    expect(currentFills.length).toBe(3);
    expect(markers.length).toBe(1);
  });

  it("should given markers exist when fills are cleared then remove all markers", () => {
    fixture.componentRef.setInput("fills", fillSeed);
    fixture.componentRef.setInput("showTradeMarkers", true);
    fixture.detectChanges();

    const markersApi = (component as unknown as {
      _markersApi: { setMarkers: jasmine.Spy };
    })._markersApi;
    spyOn(markersApi, "setMarkers").and.callThrough();

    fixture.componentRef.setInput("fills", []);
    fixture.detectChanges();

    const markers = markersApi.setMarkers.calls.mostRecent().args[0] as unknown[];
    expect(markers).toEqual([]);
  });
});