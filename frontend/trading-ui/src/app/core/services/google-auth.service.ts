import { Injectable, NgZone, inject } from "@angular/core";
import { environment } from "../../../environments/environment";

interface GoogleIdConfiguration {
  client_id: string;
  callback: (response: GoogleCredentialResponse) => void;
  auto_select?: boolean;
  cancel_on_tap_outside?: boolean;
}

interface GoogleCredentialResponse {
  credential: string;
  select_by: string;
}

interface GoogleButtonConfiguration {
  type?: "standard" | "icon";
  theme?: "outline" | "filled_blue" | "filled_black";
  size?: "large" | "medium" | "small";
  text?: "signin_with" | "signup_with" | "continue_with" | "signin";
  shape?: "rectangular" | "pill" | "circle" | "square";
  logo_alignment?: "left" | "center";
  width?: number;
}

interface GoogleAccountsId {
  initialize: (config: GoogleIdConfiguration) => void;
  renderButton: (parent: HTMLElement, config: GoogleButtonConfiguration) => void;
}

declare const google: {
  accounts: {
    id: GoogleAccountsId;
  };
};

@Injectable({ providedIn: "root" })
export class GoogleAuthService {
  private readonly _ngZone = inject(NgZone);
  private _initialized = false;

  public initialize(onCredential: (idToken: string) => void): void {
    if (this._initialized) return;

    if (typeof google === "undefined" || !google?.accounts?.id) {
      console.warn("Google Identity Services SDK not loaded");
      return;
    }

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (response: GoogleCredentialResponse) => {
        this._ngZone.run(() => onCredential(response.credential));
      },
      cancel_on_tap_outside: true,
    });

    this._initialized = true;
  }

  public renderButton(element: HTMLElement, text: "signin_with" | "signup_with" = "signin_with"): void {
    if (typeof google === "undefined" || !google?.accounts?.id) {
      console.warn("Google Identity Services SDK not loaded");
      return;
    }

    google.accounts.id.renderButton(element, {
      type: "standard",
      theme: "filled_black",
      size: "large",
      text,
      shape: "rectangular",
      width: 350,
    });
  }
}
