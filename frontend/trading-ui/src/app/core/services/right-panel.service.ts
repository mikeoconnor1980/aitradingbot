import { Injectable, signal } from "@angular/core";

export type RightPanel = "closed" | "analyst" | "help" | "notifications";

@Injectable({ providedIn: "root" })
export class RightPanelService {
  public readonly activePanel = signal<RightPanel>("closed");

  public open(panel: Exclude<RightPanel, "closed">): void {
    this.activePanel.set(panel);
  }

  public close(panel?: Exclude<RightPanel, "closed">): void {
    if (!panel || this.activePanel() === panel) {
      this.activePanel.set("closed");
    }
  }

  public toggle(panel: Exclude<RightPanel, "closed">): void {
    this.activePanel.update((activePanel) => activePanel === panel ? "closed" : panel);
  }
}