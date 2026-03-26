import { ComponentFixture, TestBed } from "@angular/core/testing";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { Position } from "../../../core/models/position.model";
import { PositionsTableComponent } from "./positions-table.component";

const mockPositions: Position[] = [
  {
    asset: "BTC",
    side: "Long",
    size: 0.001,
    entryPrice: 50000,
    markPrice: 51000,
    unrealisedPnl: 64.13,
    unrealisedPnlPercent: 12.8,
    liquidationPrice: 40000,
    leverage: 10,
    marginMode: "cross",
    marginUsed: 5.1,
    fundingRate: -0.0001
  },
  {
    asset: "ETH",
    side: "Short",
    size: 0.5,
    entryPrice: 3000,
    markPrice: 3050,
    unrealisedPnl: -22.19,
    unrealisedPnlPercent: -1.5,
    liquidationPrice: 3500,
    leverage: 5,
    marginMode: "cross",
    marginUsed: 305,
    fundingRate: 0.0002
  },
  {
    asset: "SUI",
    side: "Long",
    size: 100,
    entryPrice: 1.5,
    markPrice: 1.52,
    unrealisedPnl: 2.15,
    unrealisedPnlPercent: 1.4,
    liquidationPrice: 1.0,
    leverage: 3,
    marginMode: "cross",
    marginUsed: 50.67,
    fundingRate: 0.00005
  }
];

describe("PositionsTableComponent", () => {
  let component: PositionsTableComponent;
  let fixture: ComponentFixture<PositionsTableComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PositionsTableComponent, NoopAnimationsModule]
    }).compileComponents();

    fixture = TestBed.createComponent(PositionsTableComponent);
    component = fixture.componentInstance;
    component.positions = [...mockPositions];
    fixture.detectChanges();
  });

  describe("sorting", () => {
    it("should return positions in original order by default", () => {
      expect(component.sortedFilteredPositions.map((position) => position.asset)).toEqual(["BTC", "ETH", "SUI"]);
    });

    it("should sort by PnL descending on first click", () => {
      component.onSort("unrealisedPnl");

      expect(component.sortedFilteredPositions.map((position) => position.asset)).toEqual(["BTC", "SUI", "ETH"]);
    });

    it("should sort by PnL ascending on second click", () => {
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");

      expect(component.sortedFilteredPositions.map((position) => position.asset)).toEqual(["ETH", "SUI", "BTC"]);
    });

    it("should remove sort on third click", () => {
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");

      expect(component.sortColumn).toBeNull();
      expect(component.sortDirection).toBeNull();
      expect(component.sortedFilteredPositions.map((position) => position.asset)).toEqual(["BTC", "ETH", "SUI"]);
    });

    it("should reset to descending when switching columns", () => {
      component.onSort("unrealisedPnl");
      component.onSort("unrealisedPnl");
      component.onSort("asset");

      expect(component.sortDirection).toBe("desc");
      expect(component.sortedFilteredPositions.map((position) => position.asset)).toEqual(["SUI", "ETH", "BTC"]);
    });

    it("should sort asset alphabetically", () => {
      component.onSort("asset");
      component.onSort("asset");

      expect(component.sortedFilteredPositions.map((position) => position.asset)).toEqual(["BTC", "ETH", "SUI"]);
    });
  });

  describe("filtering", () => {
    it("should filter positions by asset name case-insensitively", () => {
      component.filterText = "btc";

      expect(component.sortedFilteredPositions.length).toBe(1);
      expect(component.sortedFilteredPositions[0].asset).toBe("BTC");
    });

    it("should show all positions when filter is empty", () => {
      component.filterText = "";

      expect(component.sortedFilteredPositions.length).toBe(3);
    });

    it("should return 0 results for non-matching filter", () => {
      component.filterText = "XYZ";

      expect(component.filteredCount).toBe(0);
    });

    it("should clear filter", () => {
      component.filterText = "BTC";

      component.clearFilter();

      expect(component.filterText).toBe("");
      expect(component.sortedFilteredPositions.length).toBe(3);
    });

    it("should apply filter and sort together", () => {
      component.filterText = "s";
      component.onSort("unrealisedPnl");

      expect(component.sortedFilteredPositions.length).toBe(1);
      expect(component.sortedFilteredPositions[0].asset).toBe("SUI");
    });

    it("should report isFiltered correctly", () => {
      expect(component.isFiltered).toBeFalse();

      component.filterText = "E";

      expect(component.isFiltered).toBeTrue();
    });
  });

  describe("close all", () => {
    it("should show Close All button when positions exist", () => {
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector(".positions-table__close-all-btn");
      expect(button).not.toBeNull();
    });

    it("should hide Close All button when no positions exist", () => {
      component.positions = [];
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector(".positions-table__close-all-btn");
      expect(button).toBeNull();
    });

    it("should disable Close All button during global loading", () => {
      component.setGlobalLoading(true);
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector(".positions-table__close-all-btn") as HTMLButtonElement;
      expect(button.disabled).toBeTrue();
    });

    it("should emit closeAllPositions when Close All button is clicked", () => {
      spyOn(component.closeAllPositions, "emit");
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector(".positions-table__close-all-btn") as HTMLButtonElement;
      button.click();

      expect(component.closeAllPositions.emit).toHaveBeenCalled();
    });
  });

  describe("details", () => {
    it("should toggle details for a position", () => {
      const position = mockPositions[0];

      expect(component.isDetailsExpanded(position)).toBeFalse();

      component.toggleDetails(position);
      expect(component.isDetailsExpanded(position)).toBeTrue();

      component.toggleDetails(position);
      expect(component.isDetailsExpanded(position)).toBeFalse();
    });

    it("should format position value and margin labels", () => {
      component.equity = 1000;

      expect(component.getNotionalLabel(mockPositions[0])).toBe("$51.00");
      expect(component.getMarginLabel(mockPositions[0])).toBe("$5.10");
      expect(component.getMarginPercentLabel(mockPositions[0])).toBe("0.5% of equity");
    });
  });
});
