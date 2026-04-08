import { Component, inject, signal } from "@angular/core";
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { Router, RouterLink } from "@angular/router";
import { AuthService } from "../../core/services/auth.service";

@Component({
  selector: "app-login-page",
  standalone: true,
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, RouterLink],
  templateUrl: "./login-page.component.html",
  styleUrl: "./login-page.component.scss"
})
export class LoginPageComponent {
  private readonly _authService = inject(AuthService);
  private readonly _router = inject(Router);
  private readonly _fb = inject(FormBuilder);

  public readonly form: FormGroup = this._fb.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", [Validators.required]]
  });

  public readonly submitting = signal(false);
  public readonly errorMessage = signal<string | null>(null);
  public readonly hidePassword = signal(true);

  public onSubmit(): void {
    if (this.form.invalid) return;

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.form.value;
    this._authService.login({ email, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        this._router.navigate(["/dashboard"]);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.errorMessage ?? "Login failed. Please try again.");
      }
    });
  }
}
