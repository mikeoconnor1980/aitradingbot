import { Component, OnInit, inject } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { ActivatedRoute, Router } from "@angular/router";
import { AnalystSessionService } from "../../core/services/analyst-session.service";
import { AnalystConversationComponent } from "./analyst-conversation.component";

@Component({
  selector: "app-analyst-page",
  standalone: true,
  imports: [MatButtonModule, MatIconModule, AnalystConversationComponent],
  templateUrl: "./analyst-page.component.html",
  styleUrl: "./analyst-page.component.scss"
})
export class AnalystPageComponent implements OnInit {
  private readonly _router = inject(Router);
  private readonly _route = inject(ActivatedRoute);
  public readonly session = inject(AnalystSessionService);

  public ngOnInit(): void {
    const context = this.session.routeContext(this._route.snapshot.queryParamMap);
    if (context && this.session.messages().length === 0) {
      this.session.start(context, this.session.questionFor(context));
    }
  }

  public returnToWorkspace(): void {
    if (this.session.previousRoute()) {
      void this._router.navigateByUrl(this.session.previousRoute()!);
    }
  }
}