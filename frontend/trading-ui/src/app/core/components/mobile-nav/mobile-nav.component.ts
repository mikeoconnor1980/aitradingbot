import { Component } from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { RouterLink, RouterLinkActive } from "@angular/router";

@Component({
  selector: "app-mobile-nav",
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatIconModule],
  templateUrl: "./mobile-nav.component.html",
  styleUrl: "./mobile-nav.component.scss"
})
export class MobileNavComponent {}
