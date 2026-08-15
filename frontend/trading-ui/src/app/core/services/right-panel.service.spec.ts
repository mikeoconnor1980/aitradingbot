import { TestBed } from "@angular/core/testing";
import { RightPanelService } from "./right-panel.service";

describe("RightPanelService", () => {
  let service: RightPanelService;

  beforeEach(() => {
    service = TestBed.inject(RightPanelService);
  });

  it("keeps right-side utilities mutually exclusive", () => {
    service.open("analyst");
    service.open("help");
    service.open("notifications");

    expect(service.activePanel()).toBe("notifications");
  });

  it("closes only the requested active panel", () => {
    service.open("analyst");
    service.close("help");
    expect(service.activePanel()).toBe("analyst");

    service.close("analyst");
    expect(service.activePanel()).toBe("closed");
  });
});