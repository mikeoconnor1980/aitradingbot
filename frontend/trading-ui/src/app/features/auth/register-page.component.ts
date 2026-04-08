import { Component, inject, signal } from "@angular/core";
import { AbstractControl, ReactiveFormsModule, FormBuilder, FormGroup, ValidationErrors, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { Router, RouterLink } from "@angular/router";
import { AuthService } from "../../core/services/auth.service";

@Component({
  selector: "app-register-page",
  standalone: true,
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, RouterLink],
  templateUrl: "./register-page.component.html",
  styleUrl: "./register-page.component.scss"
})
export class RegisterPageComponent {
  private readonly _authService = inject(AuthService);
  private readonly _router = inject(Router);
  private readonly _fb = inject(FormBuilder);

  public readonly form: FormGroup = this._fb.group({
    email: ["", [Validators.required, Validators.email]],
    displayName: ["", [Validators.required, Validators.minLength(2)]],
    password: ["", [Validators.required, Validators.minLength(8), RegisterPageComponent.passwordComplexity]],
    confirmPassword: ["", [Validators.required]]
  }, { validators: RegisterPageComponent.passwordsMatch });

  public readonly submitting = signal(false);
  public readonly errorMessage = signal<string | null>(null);
  public readonly hidePassword = signal(true);

  public onSubmit(): void {
    if (this.form.invalid) return;

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { email, displayName, password } = this.form.value;
    this._authService.register({ email, displayName, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        this._router.navigate(["/dashboard"]);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.errorMessage ?? "Registration failed. Please try again.");
      }
    });
  }

  private static passwordComplexity(control: AbstractControl): ValidationErrors | null {
    const value: string = control.value || "";
    const errors: Record<string, boolean> = {};

    if (!/[A-Z]/.test(value)) errors["missingUpper"] = true;
    if (!/[0-9]/.test(value)) errors["missingDigit"] = true;
    if (!/[^a-zA-Z0-9]/.test(value)) errors["missingSpecial"] = true;

    return Object.keys(errors).length > 0 ? errors : null;
  }

  private static passwordsMatch(group: AbstractControl): ValidationErrors | null {
    const pw = group.get("password")?.value;
    const confirm = group.get("confirmPassword")?.value;
    return pw === confirm ? null : { passwordMismatch: true };
  }
}
