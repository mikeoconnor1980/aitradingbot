import { ComponentFixture, TestBed } from "@angular/core/testing";
import { MAT_DIALOG_DATA, MatDialogRef } from "@angular/material/dialog";
import { NoopAnimationsModule } from "@angular/platform-browser/animations";
import { Position } from "../../../../core/models/position.model";
import { CloseAllDialogComponent } from "./close-all-dialog.component";

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
  }
];

describe("CloseAllDialogComponent", () => {
  let component: CloseAllDialogComponent;
  let fixture: ComponentFixture<CloseAllDialogComponent>;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<CloseAllDialogComponent>>;

  beforeEach(async () => {
    dialogRefSpy = jasmine.createSpyObj("MatDialogRef", ["close"]);

    await TestBed.configureTestingModule({
      imports: [CloseAllDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: { positions: mockPositions } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CloseAllDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it("should render position list", () => {
    const rows = fixture.nativeElement.querySelectorAll(".close-all-dialog__position-row");
    expect(rows.length).toBe(2);
  });

  it("should close with confirmed false on cancel", () => {
    component.onCancel();

    expect(dialogRefSpy.close).toHaveBeenCalledWith(
      jasmine.objectContaining({ confirmed: false, total: 2 })
    );
  });

  it("should close with confirmed true on confirm", () => {
    component.onConfirm();

    expect(dialogRefSpy.close).toHaveBeenCalledWith(
      jasmine.objectContaining({ confirmed: true, total: 2 })
    );
  });
});
