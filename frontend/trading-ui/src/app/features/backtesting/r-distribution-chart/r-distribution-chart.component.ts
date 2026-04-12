import { DecimalPipe } from "@angular/common";
import { Component, Input, OnChanges, SimpleChanges } from "@angular/core";

interface RBucket {
  label: string;
  count: number;
  percent: number;
  isPositive: boolean;
}

@Component({
  selector: "app-r-distribution-chart",
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: "./r-distribution-chart.component.html",
  styleUrl: "./r-distribution-chart.component.scss"
})
export class RDistributionChartComponent implements OnChanges {
  @Input()
  public rDistribution: number[] | null = null;

  public buckets: RBucket[] = [];

  public ngOnChanges(changes: SimpleChanges): void {
    void changes;

    if (!this.rDistribution || this.rDistribution.length === 0) {
      this.buckets = [];
      return;
    }

    const ranges = [
      { label: "< -1R", test: (value: number) => value < -1, isPositive: false },
      { label: "-1R to 0", test: (value: number) => value >= -1 && value < 0, isPositive: false },
      { label: "0 to 1R", test: (value: number) => value >= 0 && value < 1, isPositive: true },
      { label: "1R to 2R", test: (value: number) => value >= 1 && value < 2, isPositive: true },
      { label: "2R to 3R", test: (value: number) => value >= 2 && value < 3, isPositive: true },
      { label: "> 3R", test: (value: number) => value >= 3, isPositive: true }
    ];

    const total = this.rDistribution.length;
    this.buckets = ranges.map((range) => {
      const count = this.rDistribution?.filter(range.test).length ?? 0;

      return {
        label: range.label,
        count,
        percent: total > 0 ? (count / total) * 100 : 0,
        isPositive: range.isPositive
      };
    });
  }
}