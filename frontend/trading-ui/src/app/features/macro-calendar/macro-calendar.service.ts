import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { MacroEventListItem, MacroSyncResult } from "./models/macro-event.model";

@Injectable({ providedIn: "root" })
export class MacroCalendarService {
  private readonly _http = inject(HttpClient);
  private readonly _basePath = `${environment.apiBaseUrl}/macro-calendar`;

  public getUpcomingEvents(fromUtcMs: number, toUtcMs: number, currency?: string): Observable<MacroEventListItem[]> {
    let params = new HttpParams()
      .set("fromUtc", fromUtcMs.toString())
      .set("toUtc", toUtcMs.toString());

    if (currency) {
      params = params.set("currency", currency);
    }

    return this._http.get<MacroEventListItem[]>(`${this._basePath}/events`, { params });
  }

  public getActiveBlocks(): Observable<MacroEventListItem[]> {
    return this._http.get<MacroEventListItem[]>(`${this._basePath}/active-blocks`);
  }

  public triggerSync(): Observable<MacroSyncResult> {
    return this._http.post<MacroSyncResult>(`${this._basePath}/sync`, {});
  }
}
