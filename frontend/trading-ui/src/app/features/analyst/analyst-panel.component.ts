import { AfterViewInit, Component, DestroyRef, HostListener, ViewChild, inject, signal } from "@angular/core";
import { A11yModule } from "@angular/cdk/a11y";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { AnalystSessionService } from "../../core/services/analyst-session.service";
import { HelpService } from "../../core/services/help.service";
import { RightPanelService } from "../../core/services/right-panel.service";
import { AnalystConversationComponent } from "./analyst-conversation.component";

@Component({
  selector: "app-analyst-panel",
  standalone: true,
  imports: [A11yModule, MatButtonModule, MatIconModule, MatTooltipModule, AnalystConversationComponent],
  templateUrl: "./analyst-panel.component.html",
  styleUrl: "./analyst-panel.component.scss"
})
export class AnalystPanelComponent implements AfterViewInit {
  private readonly _rightPanels = inject(RightPanelService);
  private readonly _helpService = inject(HelpService);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private _previouslyFocused?: HTMLElement;
  private _resizing = false;

  @ViewChild(AnalystConversationComponent)
  public conversation?: AnalystConversationComponent;

  public readonly session = inject(AnalystSessionService);
  public readonly isModal = signal(false);
  public readonly panelWidth = signal(this._readWidth());

  public constructor() {
    this._helpService.close();
    this._setModalState();
    this._setDocumentWidth();
    this._destroyRef.onDestroy(() => this._setDocumentWidth(400));
  }

  public ngAfterViewInit(): void {
    this._previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
    queueMicrotask(() => this.conversation?.focusComposer());
  }

  @HostListener("window:resize")
  public onWindowResize(): void {
    this._setModalState();
  }

  @HostListener("document:keydown.escape")
  public onEscape(): void {
    if (this.isModal()) this.close();
  }

  @HostListener("document:mousemove", ["$event"])
  public onResize(event: MouseEvent): void {
    if (!this._resizing) return;
    this.setWidth(window.innerWidth - event.clientX);
  }

  @HostListener("document:mouseup")
  public stopResize(): void {
    this._resizing = false;
  }

  public close(): void {
    this._rightPanels.close("analyst");
    queueMicrotask(() => this._previouslyFocused?.focus());
  }

  public expand(): void {
    void this._router.navigate(["/analyst"]);
    this._rightPanels.close("analyst");
  }

  public newInvestigation(): void {
    this.session.clear();
    this.conversation?.focusComposer();
  }

  public startResize(event: MouseEvent): void {
    if (!this.isDocked()) return;
    event.preventDefault();
    this._resizing = true;
  }

  public adjustWidth(change: number): void {
    this.setWidth(this.panelWidth() + change);
  }

  public isDocked(): boolean {
    return window.innerWidth >= 1280;
  }

  private setWidth(width: number): void {
    const boundedWidth = Math.max(360, Math.min(520, Math.round(width)));
    this.panelWidth.set(boundedWidth);
    localStorage.setItem("tradepilot.analyst-panel-width", String(boundedWidth));
    this._setDocumentWidth();
  }

  private _setModalState(): void {
    this.isModal.set(window.innerWidth < 1280);
  }

  private _readWidth(): number {
    const savedWidth = Number(localStorage.getItem("tradepilot.analyst-panel-width"));
    return Number.isFinite(savedWidth) ? Math.max(360, Math.min(520, savedWidth)) : 400;
  }

  private _setDocumentWidth(width: number = this.panelWidth()): void {
    document.documentElement.style.setProperty("--analyst-panel-width", `${width}px`);
  }
}