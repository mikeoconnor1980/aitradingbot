import { DatePipe, DecimalPipe } from "@angular/common";
import { AfterViewInit, Component, Input, OnChanges, SimpleChanges, ViewChild } from "@angular/core";
import { MatSort, MatSortModule } from "@angular/material/sort";
import { MatTableDataSource, MatTableModule } from "@angular/material/table";
import { BacktestTrade } from "../../../core/models/backtest.model";

@Component({
  selector: "app-trade-log-table",
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatTableModule, MatSortModule],
  templateUrl: "./trade-log-table.component.html",
  styleUrl: "./trade-log-table.component.scss"
})
export class TradeLogTableComponent implements OnChanges, AfterViewInit {
  @Input()
  public trades: BacktestTrade[] = [];

  @ViewChild(MatSort)
  public sort!: MatSort;

  public readonly displayedColumns: string[] = [
    "entryTime",
    "exitTime",
    "entryPrice",
    "exitPrice",
    "side",
    "size",
    "pnl",
    "fees"
  ];

  public readonly dataSource = new MatTableDataSource<BacktestTrade>([]);

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes["trades"]) {
      this.dataSource.data = [...this.trades];
    }
  }

  public ngAfterViewInit(): void {
    this.dataSource.sortingDataAccessor = (item: BacktestTrade, property: string): string | number => {
      switch (property) {
        case "entryTime":
          return new Date(item.entryTime).getTime();
        case "exitTime":
          return item.exitTime ? new Date(item.exitTime).getTime() : 0;
        case "entryPrice":
          return item.entryPrice;
        case "exitPrice":
          return item.exitPrice ?? 0;
        case "size":
          return item.size;
        case "pnl":
          return item.pnl ?? 0;
        case "fees":
          return item.fees;
        case "side":
          return item.side;
        default:
          return "";
      }
    };

    this.dataSource.sort = this.sort;
  }

  public getPnlClass(pnl: number | null): string {
    if (pnl == null) {
      return "";
    }

    return pnl >= 0 ? "trade-log__pnl--profit" : "trade-log__pnl--loss";
  }
}