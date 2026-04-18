import { HttpContext } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiRestClient } from "../../../core/services/api-rest-client.service";
import { AdminUserDto, CreateAdminUserRequest } from "../models/admin-user.model";

@Injectable({ providedIn: "root" })
export class AdminUsersApiService {
  private readonly _apiClient = inject(ApiRestClient);

  public getAdminUsers(context?: HttpContext): Observable<AdminUserDto[]> {
    return this._apiClient.get<AdminUserDto[]>("admin/users", context);
  }

  public addAdminUser(request: CreateAdminUserRequest, context?: HttpContext): Observable<{ id: string }> {
    return this._apiClient.post<{ id: string }>("admin/users", request, context);
  }

  public removeAdminUser(grantId: string, context?: HttpContext): Observable<void> {
    return this._apiClient.delete<void>(`admin/users/${encodeURIComponent(grantId)}`, context);
  }
}