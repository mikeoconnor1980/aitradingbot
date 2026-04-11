import { Injectable, Signal, inject } from "@angular/core";
import { BreakpointObserver } from "@angular/cdk/layout";
import { toSignal } from "@angular/core/rxjs-interop";
import { map } from "rxjs";

@Injectable({ providedIn: "root" })
export class LayoutService {
  private readonly _breakpointObserver = inject(BreakpointObserver);

  public readonly isMobile: Signal<boolean> = toSignal(
    this._breakpointObserver
      .observe("(max-width: 768px)")
      .pipe(map((result) => result.matches)),
    { initialValue: false }
  );
}
