import { DatePipe } from "@angular/common";
import { HttpContext } from "@angular/common/http";
import { Component, OnInit, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { Router } from "@angular/router";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatDialog } from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { SKIP_ERROR_NOTIFICATION } from "../../core/interceptors/http-context-tokens";
import { AuthService } from "../../core/services/auth.service";
import { NotificationFacade } from "../../core/services/notification-facade.service";
import { ConfirmDialogComponent, ConfirmDialogData } from "../order-entry/confirm-dialog/confirm-dialog.component";
import { AdminUserDto } from "./models/admin-user.model";
import { AdminUsersApiService } from "./services/admin-users-api.service";

@Component({
  selector: "app-admin-users-page",
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule
  ],
  templateUrl: "./admin-users-page.component.html",
  styleUrl: "./admin-users-page.component.scss"
})
export class AdminUsersPageComponent implements OnInit {
  private readonly _adminUsersApi = inject(AdminUsersApiService);
  private readonly _authService = inject(AuthService);
  private readonly _dialog = inject(MatDialog);
  private readonly _fb = inject(FormBuilder);
  private readonly _notifications = inject(NotificationFacade);
  private readonly _router = inject(Router);
  private readonly _localErrorContext = new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

  public readonly displayedColumns = ["email", "account", "createdAtUtc", "actions"];
  public readonly form = this._fb.nonNullable.group({
    email: ["", [Validators.required, Validators.email, Validators.maxLength(256)]]
  });

  public admins: AdminUserDto[] = [];
  public isLoading = true;
  public isAdding = false;
  public removingAdminId: string | null = null;

  public ngOnInit(): void {
    this._loadAdmins();
  }

  public isCurrentUser(admin: AdminUserDto): boolean {
    return this._normalizeEmail(admin.email) === this._normalizeEmail(this._authService.currentUser?.email ?? "");
  }

  public onAddAdmin(): void {
    if (this.form.invalid || this.isAdding || this.removingAdminId !== null) {
      this.form.markAllAsTouched();
      return;
    }

    this.isAdding = true;
    const email = this.form.controls.email.value.trim();

    this._adminUsersApi.addAdminUser({ email }, this._localErrorContext).subscribe({
      next: () => {
        this.isAdding = false;
        this.form.reset({ email: "" });
        this._notifications.success(`Admin access granted to '${email}'.`);
        this._loadAdmins();
      },
      error: (error) => {
        this.isAdding = false;
        this._notifications.error(error.error?.errorMessage ?? "Failed to add admin user.");
      }
    });
  }

  public onRemoveAdmin(admin: AdminUserDto): void {
    if (this.removingAdminId !== null || this.isAdding) {
      return;
    }

    const dialogData: ConfirmDialogData = {
      title: "Remove Admin Access",
      message: `Remove admin access for '${admin.email}'?`,
      confirmText: "Remove",
      cancelText: "Cancel"
    };

    this._dialog.open(ConfirmDialogComponent, { data: dialogData, width: "420px" }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.removingAdminId = admin.id;
      const removedCurrentUser = this.isCurrentUser(admin);

      this._adminUsersApi.removeAdminUser(admin.id, this._localErrorContext).subscribe({
        next: () => {
          this.removingAdminId = null;

          if (removedCurrentUser) {
            this._notifications.success("Your admin access was removed.");
            this._authService.syncCurrentUser().subscribe({
              next: () => {
                void this._router.navigate(["/dashboard"]);
              },
              error: () => {
                void this._router.navigate(["/dashboard"]);
              }
            });

            return;
          }

          this._notifications.success(`Admin access removed for '${admin.email}'.`);
          this._loadAdmins();
        },
        error: (error) => {
          this.removingAdminId = null;
          this._notifications.error(error.error?.errorMessage ?? "Failed to remove admin user.");
        }
      });
    });
  }

  private _loadAdmins(): void {
    this.isLoading = true;

    this._adminUsersApi.getAdminUsers(this._localErrorContext).subscribe({
      next: (admins) => {
        this.admins = admins;
        this.isLoading = false;
      },
      error: (error) => {
        this.admins = [];
        this.isLoading = false;
        this._notifications.error(error.error?.errorMessage ?? "Failed to load admin users.");
      }
    });
  }

  private _normalizeEmail(value: string): string {
    return value.trim().toLowerCase();
  }
}