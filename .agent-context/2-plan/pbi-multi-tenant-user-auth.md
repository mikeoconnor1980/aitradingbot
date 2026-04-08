# PBI Specification: Multi-Tenant User Authentication & Wallet Registration

**Date:** 2026-04-08
**Author:** Copilot / mdoconnor
**Status:** Draft

---

## Summary

Introduce real user authentication to the platform, replacing the hardcoded dev identity stub (ADR 13). Users can register, log in, log out, and provide their Hyperliquid wallet address. All tenant-scoped data is isolated by UserId. This is the foundational PBI that enables multi-tenant operation.

### User Story

> As a **subscriber**, I want to **register an account, log in, and link my Hyperliquid wallet address** so that **the platform can monitor my positions and execute trades on my behalf**.

### Business Value

Without multi-tenant authentication, only a single hardcoded dev user can use the platform. This PBI unblocks all subscriber-facing functionality: onboarding, per-user strategy management, and multi-user trading execution — which are required before any public launch or beta testing.

---

## Requirements

### Functional Requirements

**Registration**
- [ ] User can register with email and password via a registration form
- [ ] Email must be unique across all users
- [ ] Password must meet minimum complexity (8+ chars, 1 uppercase, 1 number, 1 special char)
- [ ] On successful registration, a `User` entity is created in the database
- [ ] After registration, the user is automatically logged in and redirected to the dashboard

**Login / Logout**
- [ ] User can log in with email and password
- [ ] Successful login returns a JWT access token (and optional refresh token)
- [ ] JWT token includes `UserId` and `Email` claims
- [ ] User can log out (clears client-side token)
- [ ] All existing API endpoints require authentication (except health, login, register)
- [ ] Unauthenticated requests return 401

**Wallet Address**
- [ ] Authenticated user can provide their Hyperliquid wallet address (0x... Ethereum address format)
- [ ] Wallet address is validated for format (42-char hex string, 0x prefix)
- [ ] Wallet address is stored in a `UserWalletAddress` entity linked to the user
- [ ] User can view their current wallet address
- [ ] User can update their wallet address
- [ ] User can remove their wallet address
- [ ] Only ONE wallet address per user in this version

**Identity Integration**
- [ ] Replace `IdentityService` singleton stub with a real identity resolution from JWT claims
- [ ] `AppIdentity` is populated from the authenticated user's JWT on each request
- [ ] All existing handlers continue to receive `AppIdentity` without code changes (ADR 13 contract honoured)
- [ ] `Strategy`, `BacktestRun`, `OptimizationRun`, and other tenant-scoped entities are filtered by the authenticated user's `UserId`

### Non-Functional Requirements

- [ ] Password hashing uses ASP.NET Core Identity's default (PBKDF2) — no custom crypto
- [ ] JWT signing key stored in configuration (appsettings / environment variable)
- [ ] Token expiry: access token 60 minutes, refresh token 7 days
- [ ] All auth endpoints return consistent error responses (no stack traces)
- [ ] Registration and login endpoints are rate-limited (10 requests/minute per IP)
- [ ] Existing `dev-user` data migrated or re-assignable to a real registered user

---

## User Flow

### Happy Path — Registration

1. User navigates to `/register`
2. User fills in: email, display name, password, confirm password
3. Client validates form (matching passwords, email format, password strength)
4. Client calls `POST /api/auth/register`
5. Server creates `User` entity, hashes password, stores in database
6. Server returns JWT token
7. Client stores token, redirects to dashboard

### Happy Path — Login

1. User navigates to `/login`
2. User enters email and password
3. Client calls `POST /api/auth/login`
4. Server validates credentials, returns JWT token
5. Client stores token, redirects to dashboard

### Happy Path — Wallet Address

1. Authenticated user navigates to Settings / Profile page
2. User enters their Hyperliquid wallet address (e.g. `0xAbC123...`)
3. Client calls `POST /api/wallet-address`
4. Server validates the address format and stores it
5. Dashboard displays connected wallet address

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Register with existing email | 409 Conflict — "An account with this email already exists" |
| Register with weak password | 400 Bad Request — password requirements listed |
| Login with wrong password | 401 Unauthorized — "Invalid email or password" (generic) |
| Login with non-existent email | 401 Unauthorized — "Invalid email or password" (same message, no user enumeration) |
| Invalid wallet address format | 400 Bad Request — "Invalid Ethereum address format" |
| Access protected endpoint without token | 401 Unauthorized |
| Access protected endpoint with expired token | 401 Unauthorized |
| Access another user's strategy | 404 Not Found (not 403, to prevent resource enumeration) |

---

## Technical Considerations

### Bounded Context

**Context:** Identity & User Management (new)

### Domain Entities

**User** (new entity — `src/TradingApp.Domain/Entities/User.cs`)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Primary key |
| Email | string | Unique, required |
| DisplayName | string | Required |
| PasswordHash | string | ASP.NET Core Identity hash |
| CreatedAtUtc | long | Unix milliseconds |
| IsActive | bool | Soft-delete flag |

**UserWalletAddress** (new entity — `src/TradingApp.Domain/Entities/UserWalletAddress.cs`)

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Primary key |
| UserId | Guid | FK → User |
| Exchange | string | Always "Hyperliquid" in v1 |
| WalletAddress | string | 0x-prefixed Ethereum address |
| CreatedAtUtc | long | Unix milliseconds |
| IsActive | bool | Soft-delete flag |

> **Note:** This entity intentionally does NOT store private keys. The wallet address is used for read-only position monitoring and for the Split Architecture execution model (Option C) where the subscriber's execution agent holds the private key separately.

### API Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/register` | Anonymous | Register new user |
| POST | `/api/auth/login` | Anonymous | Authenticate and return JWT |
| POST | `/api/auth/refresh` | Anonymous (with valid refresh token) | Refresh access token |
| GET | `/api/auth/me` | Authenticated | Get current user profile |
| GET | `/api/wallet-address` | Authenticated | Get current user's wallet address |
| POST | `/api/wallet-address` | Authenticated | Set/update wallet address |
| DELETE | `/api/wallet-address` | Authenticated | Remove wallet address |

**Request/Response Shapes:**

```
POST /api/auth/register
Request:  { email: string, displayName: string, password: string }
Response: { token: string, refreshToken: string, user: { id, email, displayName } }

POST /api/auth/login
Request:  { email: string, password: string }
Response: { token: string, refreshToken: string, user: { id, email, displayName } }

GET /api/auth/me
Response: { id, email, displayName, hasWalletAddress: bool }

POST /api/wallet-address
Request:  { walletAddress: string }
Response: { walletAddress: string, exchange: "Hyperliquid" }
```

### Backend Changes

1. **New: `User` entity** in Domain layer with static factory `User.Create(email, displayName, passwordHash)`
2. **New: `UserWalletAddress` entity** in Domain layer
3. **New: `AuthController`** — handles register, login, refresh, me
4. **New: `WalletAddressController`** — handles wallet address CRUD (replaces existing `WalletController` key-management if applicable)
5. **New: `JwtTokenService`** — generates and validates JWT tokens (Infrastructure layer)
6. **Modified: `IdentityService`** — resolves `AppIdentity` from `HttpContext.User` JWT claims instead of returning hardcoded stub
7. **Modified: `Program.cs`** — add `AddAuthentication().AddJwtBearer()`, `UseAuthentication()`, `UseAuthorization()`
8. **Modified: `TradingAppDbContext`** — add `DbSet<User>`, `DbSet<UserWalletAddress>`
9. **New: EF migration** — `Users` and `UserWalletAddresses` tables
10. **Modified: All controllers** — add `[Authorize]` attribute (via base `ApiController` or individually)

### Frontend Changes

1. **New: `AuthService`** — login, register, logout, token storage/refresh, auth state observable
2. **New: `AuthInterceptor`** — attaches `Authorization: Bearer {token}` header to all API requests
3. **New: `AuthGuard`** — protects all routes except `/login` and `/register`
4. **New: Login page** (`/login`) — email + password form
5. **New: Register page** (`/register`) — email, display name, password, confirm password form
6. **New: Wallet settings component** — within a settings/profile area
7. **Modified: `app.routes.ts`** — add login, register routes; apply auth guard to existing routes
8. **Modified: Navigation** — show user display name, logout button when authenticated; show login/register when not

### Data Migration

- Existing `dev-user` scoped data (strategies, backtests, etc.) should be re-assignable. A one-time migration script or seed can create a default user matching the current `dev-user` ID so existing data is not orphaned.

---

## Out of Scope

- **Sub-accounts** — users will have sub-accounts in the future, but not in this PBI
- **Multiple wallet addresses per user** — future enhancement; this PBI supports exactly one
- **Private key storage** — this PBI stores wallet addresses only, not private keys
- **External identity providers** (Azure AD B2C, Auth0) — deferred to cloud migration phase
- **Subscription/billing** — separate PBI; no plan/payment gating in this work
- **Email verification** — not required for POC; can be added later
- **Password reset / forgot password** — not required for POC; can be added later
- **Social login** (Google, GitHub) — deferred
- **Admin roles / RBAC** — single user role for now; admin features deferred
- **Refresh token rotation / revocation list** — basic refresh token only

---

## Open Questions

- [ ] Should existing `dev-user` data be automatically migrated to the first registered user, or should there be a manual mapping step?
- [ ] For the POC phase, is self-contained ASP.NET Core Identity (with local DB) acceptable, or does the team want to start directly with an external IdP (Auth0)?
- [ ] Should the wallet address be validated against the Hyperliquid API (e.g. check it's a real funded wallet) or is format-only validation sufficient?
- [ ] Should the registration form require agreeing to Terms of Service / Privacy Policy?

---

## Acceptance Criteria

- [ ] A new user can register via the `/register` page and is redirected to the dashboard
- [ ] A registered user can log in via the `/login` page and is redirected to the dashboard
- [ ] A logged-in user can log out and is redirected to the login page
- [ ] An unauthenticated user is redirected to `/login` when accessing any protected route
- [ ] A logged-in user can set their Hyperliquid wallet address in a settings area
- [ ] A logged-in user can view and update their wallet address
- [ ] Each user can only see their own strategies, backtests, and other tenant-scoped data
- [ ] Two registered users cannot see each other's data
- [ ] Registration with a duplicate email returns an appropriate error
- [ ] Invalid wallet address format is rejected with a clear error message
- [ ] All existing API endpoints return 401 for unauthenticated requests
- [ ] All existing functionality continues to work for authenticated users (no regressions)
- [ ] All unit tests pass with >80% code coverage for new code
