import { Injectable } from "@angular/core";
import { AnalystChartContext } from "../models/analyst.model";

@Injectable({ providedIn: "root" })
export class AnalystChartContextService {
  private _capture?: () => AnalystChartContext | null;

  public register(capture: () => AnalystChartContext | null): void {
    this._capture = capture;
  }

  public clear(): void {
    this._capture = undefined;
  }

  public captureCurrent(): AnalystChartContext | null {
    return this._capture?.() ?? null;
  }
}