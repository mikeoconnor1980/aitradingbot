export interface AdminUserDto {
  id: string;
  email: string;
  userId: string | null;
  displayName: string | null;
  hasRegisteredAccount: boolean;
  createdAtUtc: number;
}

export interface CreateAdminUserRequest {
  email: string;
}