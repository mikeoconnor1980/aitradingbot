# PRD: Authentication System

**Status:** Draft  
**Author:** PRD Agent  
**Date:** 2026-04-12  
**Version:** 0.1  
**Source Specification:** [copilot-auth-build-prompt-pack.md](../3-develop/backlog/draft/auth/copilot-auth-build-prompt-pack.md)

---

## 1. Background & Context

### Problem Statement

The AI Grid Trading System is a subscription-based, multi-tenant algorithmic trading platform where subscribers connect their own Hyperliquid wallet private keys and the platform trades on their behalf. This places authentication and session security at the highest tier of criticality — a compromised session could expose exchange credentials, enable unauthorised trading, or leak financial data.

The current authentication implementation was built during the Hyperliquid POC phase to unblock development. It is functional but lacks the security controls required for a production SaaS product that handles subscriber API keys and real funds.

### Current State

The following authentication functionality exists today:

| Component | Status | Implementation |
|-----------|--------|----------------|
| Local email/password registration | ✅ Implemented | `POST /api/auth/register` — password hashed via ASP.NET Identity `PasswordHasher<T>` (PBKDF2) |
| Local email/password login | ✅ Implemented | `POST /api/auth/login` — password verification, JWT issuance |
| Google SSO | ✅ Implemented | `POST /api/auth/google` — GIS popup flow, server-side ID token validation, auto-linking by email |
| JWT access tokens | ✅ Implemented | Symmetric HMAC-SHA256, configurable expiry |
| Refresh tokens | ⚠️ Partial | Refresh tokens are JWTs (not opaque tokens), stored in `localStorage` (not HttpOnly cookies), not hashed server-side, no rotation |
| Auth guard (Angular) | ✅ Implemented | Route guard checking `localStorage` for token presence |
| Auth interceptor (Angular) | ✅ Implemented | Attaches Bearer token, attempts refresh on 401 |
| `GET /api/auth/me` | ✅ Implemented | Returns current user info from JWT claims |

**Key files:**

| Layer | File | Purpose |
|-------|------|---------|
| Domain | `src/TradingApp.Domain/Entities/User.cs` | User entity with `AuthProvider`, `ExternalProviderId` (single-provider model) |
| Application | `src/TradingApp.Application/Abstractions/Auth/` | `IJwtTokenService`, `IPasswordHasher`, `IGoogleTokenValidator` interfaces |
| Infrastructure | `src/TradingApp.Infrastructure/Services/JwtTokenService.cs` | JWT generation and refresh validation |
| Infrastructure | `src/TradingApp.Infrastructure/Services/AspNetPasswordHasher.cs` | PBKDF2 password hashing |
| Infrastructure | `src/TradingApp.Infrastructure/Services/GoogleTokenValidator.cs` | Google ID token validation |
| API | `src/TradingApp.Api/Controllers/AuthController.cs` | All auth endpoints |
| Frontend | `frontend/trading-ui/src/app/core/services/auth.service.ts` | Angular auth state, localStorage token management |
| Frontend | `frontend/trading-ui/src/app/core/guards/auth.guard.ts` | Route guard |
| Frontend | `frontend/trading-ui/src/app/core/interceptors/auth.interceptor.ts` | Bearer token attachment + refresh retry |

### Security Gaps in Current Implementation

| # | Gap | Severity | Detail |
|---|-----|----------|--------|
| SG-1 | No server-side refresh token storage | **Critical** | Refresh tokens are JWTs validated by signature only. No server-side revocation capability. Compromised refresh token cannot be invalidated without rotating the signing key (which invalidates ALL tokens). |
| SG-2 | No refresh token rotation | **Critical** | Same refresh token is reused until expiry. If stolen, attacker has persistent access for the full expiry window. |
| SG-3 | Tokens in `localStorage` | **High** | Vulnerable to XSS. Any injected script can exfiltrate both access and refresh tokens. |
| SG-4 | No email verification | **High** | Users can register with any email and immediately access all features. No proof of email ownership. |
| SG-5 | Account enumeration on registration | **Medium** | `POST /api/auth/register` returns `409 duplicate_email`, revealing whether an email is registered. |
| SG-6 | No rate limiting on auth endpoints | **High** | Login, registration, and refresh endpoints are unprotected against brute-force attacks. |
| SG-7 | No MFA | **High** | No second factor available. Single password compromise = full account access including exchange credentials. |
| SG-8 | Single external provider model | **Low** | User entity supports only one `AuthProvider`/`ExternalProviderId`. Cannot link both Google and Microsoft to the same account. |
| SG-9 | No session management | **Medium** | Users cannot view active sessions or revoke access from other devices. |
| SG-10 | No step-up authentication | **High** | Sensitive actions (viewing/modifying exchange keys, enabling live trading) require no additional verification. |
| SG-11 | No audit trail | **Medium** | No logging of authentication events (login, failed attempts, password changes, MFA events). |

### Opportunity

Building a production-grade authentication system:

1. **Eliminates the highest-risk attack surface** before the platform handles real subscriber funds and exchange credentials.
2. **Enables subscription billing** — verified identity is a prerequisite for Stripe integration.
3. **Establishes trust with subscribers** — users will not connect exchange keys to a platform without MFA, session management, and security notifications.
4. **Meets the pre-launch checklist requirements** defined in [22-prelaunch-checklist.md](../0-knowledge/22-prelaunch-checklist.md).

### Relationship to Other Work

- **Prerequisite for:** Subscription billing (Stripe), admin dashboard access control, multi-tenant worker execution (user identity scoping).
- **Depends on:** Existing User entity and EF Core persistence layer.
- **Informed by:** [34-google-sso-authentication.md](../0-knowledge/34-google-sso-authentication.md), [10-architecture-decisions.md](../0-knowledge/10-architecture-decisions.md) (ADR 7, ADR 13), [22-prelaunch-checklist.md](../0-knowledge/22-prelaunch-checklist.md).

### Team & Working Model

- **1 developer, 1 PM** — no formal sprint cadence.
- Internal audience initially — security hardening must be production-grade regardless.

---

## 2. Goals & Objectives

### Business Goals

| ID | Goal | Measure of Success |
|----|------|--------------------|
| BG-1 | Secure subscriber accounts and exchange credentials to production standard | All security gaps SG-1 through SG-11 resolved; pre-launch checklist auth section passes |
| BG-2 | Enable safe multi-provider sign-in for maximum subscriber acquisition | Google and Microsoft SSO both functional; accounts linkable across providers |
| BG-3 | Establish identity verification foundation for subscription billing | Email verification enforced; verified identity available for Stripe customer creation |
| BG-4 | Protect sensitive operations with layered authentication | MFA and step-up auth required before accessing exchange credentials or enabling live trading |

### User Goals

| ID | Goal | Measure of Success |
|----|------|--------------------|
| UG-1 | Subscriber can register and verify their email securely | Registration → verification email → confirmed account flow completes end-to-end |
| UG-2 | Subscriber can sign in with email/password, Google, or Microsoft | All three methods issue valid sessions; accounts linkable across providers |
| UG-3 | Subscriber can protect their account with TOTP MFA | MFA setup, challenge, and recovery code flows work correctly |
| UG-4 | Subscriber can manage active sessions and revoke access | Security settings page shows active sessions with device info; revoke-single and revoke-all work |
| UG-5 | Subscriber is prompted for step-up verification before sensitive actions | Accessing exchange keys or enabling live trading requires recent strong authentication |
| UG-6 | Subscriber can recover account access if locked out | Password reset via email and MFA recovery codes allow account recovery |

### Success Metrics

| Metric | Target |
|--------|--------|
| Authentication-related security gaps | 0 critical or high severity gaps remaining |
| Pre-launch checklist — Authentication & Authorisation section | All items checked |
| Refresh token compromise window | Reduced from days (current JWT expiry) to single-use with rotation |
| Time to detect compromised session | Server-side revocation within seconds (vs. current: impossible without key rotation) |
| Auth endpoint brute-force protection | Rate limiting active on all auth endpoints |

### Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG-1 | Enterprise SSO (SAML, custom OIDC providers) | Not needed for initial subscriber base; architecture should allow future extension |
| NG-2 | Magic link authentication | Lower priority than password + social + MFA; can be added later |
| NG-3 | Wallet-based sign-in (Web3) | Hyperliquid wallet keys are exchange credentials, not identity credentials |
| NG-4 | Passkeys (WebAuthn / FIDO2) | Deferred to a future phase. Architecture should accommodate passkeys but implementation is not in scope for initial launch. The login and step-up flows should be extensible to add passkey support later |
| NG-5 | Admin role-based access control | Admin dashboard access control is a separate feature; this PRD covers subscriber authentication only |
| NG-6 | Subscription / billing integration | Stripe integration is a downstream consumer of verified identity, not part of auth |

---

## 3. Goals & Objectives — Review Notes

### Observations

1. **Passkeys moved to non-goal.** The build prompt pack includes passkeys (WebAuthn/FIDO2) as in-scope. Given the solo developer context and the fact that TOTP MFA + recovery codes provide strong second-factor protection, passkeys are deferred to reduce scope. The step-up auth interface should be designed to accept passkeys as a future method.

2. **Password hashing stays PBKDF2.** The build prompt pack recommends Argon2id. The current ASP.NET Identity `PasswordHasher<T>` uses PBKDF2-HMAC-SHA256 with 600,000 iterations (as of .NET 8+), which meets OWASP recommendations. Switching to Argon2id requires a third-party library and complicates the rehash path. PBKDF2 is retained.

3. **Account enumeration trade-off.** The registration endpoint currently returns `409 duplicate_email`. Fully eliminating enumeration on registration (by always returning 200) creates a poor UX. The recommended approach is to return a neutral message and send a "this email is already registered" notification to the existing email address, rather than silently succeeding.

### Assumptions

| # | Assumption | Impact if Wrong |
|---|-----------|----------------|
| A-1 | PBKDF2 with 600k+ iterations is sufficient for password hashing (no Argon2id needed) | If PBKDF2 is considered insufficient, migration to Argon2id requires a third-party NuGet package and rehash-on-login logic |
| A-2 | Single-provider external identity is insufficient; must support multi-provider linking | If only Google is ever needed, the current single-field model is adequate |
| A-3 | Email delivery can use a simple SMTP provider (e.g., SendGrid, Mailgun) for verification and reset emails | If no email provider is available, email verification and password reset cannot function |
| A-4 | `localStorage` for access tokens is acceptable if refresh tokens move to HttpOnly cookies | If XSS risk is deemed too high even for short-lived access tokens, in-memory-only storage is needed (increases complexity) |
| A-5 | Rate limiting can be implemented in-process (e.g., `System.Threading.RateLimiting`) without an external service | If the platform moves to multiple API instances, a distributed rate limiter (Redis-backed) will be needed |

---

## 4. Scope

### In Scope

| Area | Detail |
|------|--------|
| **Local auth** | Email/password registration with email verification, login, password reset |
| **Social login** | Microsoft sign-in (Google already implemented); multi-provider account linking |
| **Token security** | Server-side refresh token storage (hashed), rotation on each refresh, HttpOnly cookie transport |
| **Session management** | Active session list, per-session metadata (device/IP), revoke-single, revoke-all |
| **MFA** | TOTP setup/verification, recovery codes (hashed storage), MFA challenge during login |
| **Step-up authentication** | Time-windowed strong-auth verification for sensitive actions (exchange keys, live trading, password change, MFA disable) |
| **Audit logging** | Auth event publisher for login, registration, password change, MFA events, session events |
| **Rate limiting** | Endpoint-level rate limiting on login, register, refresh, reset, and MFA endpoints |
| **Security hardening** | CSRF protection for cookie-based flows, safe error messaging, brute-force protection |
| **Angular auth module** | Updated auth service (HttpOnly cookie handling), security settings page, MFA setup/challenge UI, session management UI |
| **Domain model changes** | `RefreshSession` entity, `ExternalIdentity` entity (replaces single-field model), `EmailVerificationToken`, `PasswordResetToken`, `MfaTotp`, `AuditEvent` |

### Out of Scope

| Area | Rationale |
|------|-----------|
| Enterprise SSO (SAML, custom OIDC) | Not needed for initial subscriber base |
| Magic link authentication | Lower priority; standard flows are sufficient |
| Wallet-based sign-in | Exchange keys are credentials, not identity |
| Passkeys (WebAuthn/FIDO2) | Deferred to future phase; TOTP MFA sufficient for launch |
| Admin RBAC | Separate feature |
| Subscription billing integration | Downstream consumer of identity |
| Email template design/branding | Functional plain-text emails are sufficient for now |

### Future Considerations

| Item | How This PRD Informs It |
|------|------------------------|
| **Passkeys** | Step-up auth interface designed to accept additional methods; `PasskeyCredentials` table can be added without schema migration to existing auth tables |
| **Admin RBAC** | Claims-based identity model established here supports role claims for admin access |
| **Subscription billing** | Email-verified identity provides the customer record for Stripe |
| **Enterprise SSO** | `ExternalIdentity` table and provider-based auth flow support arbitrary OIDC providers |
| **Distributed rate limiting** | In-process rate limiter can be swapped for Redis-backed when scaling to multiple API instances |

---

## 5. Technical Considerations

### Architecture

Authentication concerns are split across existing project layers following the established architecture:

```
Angular 19 (standalone)
  ↕ HTTP (REST) + HttpOnly cookie (refresh token)
ASP.NET Core Web API (TradingApp.Api)
  ↕ Application abstractions (TradingApp.Application)
  ↕ Infrastructure services (TradingApp.Infrastructure)
  ↕ EF Core persistence (TradingApp.Persistence)
SQLite (POC) / Azure SQL (production)
```

### Domain Model Changes

#### New Entities

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| `RefreshSession` | Server-side refresh token tracking | `Id`, `UserId`, `TokenHash`, `UserAgent`, `IpAddress`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedBySessionId` |
| `ExternalIdentity` | Multi-provider external auth linking | `Id`, `UserId`, `Provider`, `ProviderSubject`, `Email`, `CreatedAtUtc` |
| `EmailVerificationToken` | Email verification flow | `Id`, `UserId`, `TokenHash`, `ExpiresAtUtc`, `VerifiedAtUtc` |
| `PasswordResetToken` | Password reset flow | `Id`, `UserId`, `TokenHash`, `ExpiresAtUtc`, `UsedAtUtc` |
| `MfaTotp` | TOTP MFA configuration | `Id`, `UserId`, `EncryptedSecret`, `IsConfirmed`, `CreatedAtUtc` |
| `AuditEvent` | Auth event log | `Id`, `UserId`, `EventType`, `IpAddress`, `UserAgent`, `Detail`, `CreatedAtUtc` |

#### User Entity Changes

| Change | Detail |
|--------|--------|
| Add `EmailVerifiedAtUtc` | Nullable `long?`; null = unverified |
| Add `SecurityStamp` | `string`; rotated on password change, MFA change, revoke-all; used to invalidate all sessions |
| Add `MfaEnabled` | `bool`; flag for whether TOTP MFA is active |
| Deprecate `AuthProvider` / `ExternalProviderId` | Replaced by `ExternalIdentity` table; migration moves existing data |

### New Service Interfaces

| Interface | Layer | Responsibility |
|-----------|-------|----------------|
| `IRefreshSessionService` | Application | Create, rotate, revoke, revoke-all refresh sessions |
| `IEmailVerificationService` | Application | Generate verification token, verify email |
| `IPasswordResetService` | Application | Generate reset token, validate token, execute reset |
| `IMfaService` | Application | TOTP setup, verification, recovery code generation/validation |
| `IStepUpService` | Application | Check step-up window, validate step-up challenge |
| `IAuditEventPublisher` | Application | Publish auth audit events |
| `IEmailSender` | Application | Send verification emails, reset emails, security notifications |

### API Endpoint Changes

| Endpoint | Method | Auth | Purpose |
|----------|--------|------|---------|
| `POST /api/auth/register` | POST | Anonymous | Register (existing, updated to send verification email) |
| `POST /api/auth/login` | POST | Anonymous | Login (existing, updated for MFA challenge) |
| `POST /api/auth/google` | POST | Anonymous | Google SSO (existing) |
| `POST /api/auth/microsoft` | POST | Anonymous | Microsoft SSO (new) |
| `POST /api/auth/refresh` | POST | Anonymous | Refresh with rotation (reworked — reads cookie) |
| `POST /api/auth/logout` | POST | Authenticated | Revoke current refresh session |
| `POST /api/auth/verify-email` | POST | Anonymous | Verify email by token |
| `POST /api/auth/resend-verification` | POST | Authenticated | Resend verification email |
| `POST /api/auth/forgot-password` | POST | Anonymous | Request password reset email |
| `POST /api/auth/reset-password` | POST | Anonymous | Reset password by token |
| `POST /api/auth/mfa/setup` | POST | Authenticated | Begin TOTP setup (returns secret + QR URI) |
| `POST /api/auth/mfa/confirm` | POST | Authenticated | Confirm TOTP setup with code |
| `POST /api/auth/mfa/verify` | POST | Anonymous | Verify MFA code during login challenge |
| `POST /api/auth/mfa/recovery` | POST | Anonymous | Use recovery code during login challenge |
| `DELETE /api/auth/mfa` | DELETE | Authenticated + Step-up | Disable MFA |
| `GET /api/auth/sessions` | GET | Authenticated | List active sessions |
| `DELETE /api/auth/sessions/{id}` | DELETE | Authenticated | Revoke specific session |
| `DELETE /api/auth/sessions` | DELETE | Authenticated + Step-up | Revoke all sessions |
| `POST /api/auth/step-up` | POST | Authenticated | Initiate step-up challenge |
| `POST /api/auth/step-up/verify` | POST | Authenticated | Verify step-up (TOTP or password) |
| `PUT /api/auth/password` | PUT | Authenticated + Step-up | Change password |

### Refresh Token Security Model

| Concern | Approach |
|---------|----------|
| **Storage** | Opaque random token (not JWT). Server stores `SHA256(token)` in `RefreshSession` table. |
| **Transport** | HttpOnly, Secure, SameSite=Strict cookie. Not accessible to JavaScript. |
| **Rotation** | On each `POST /api/auth/refresh`, the old session is marked as replaced and a new session+token is issued. |
| **Reuse detection** | If a rotated-out token is presented, all sessions for that user are revoked (indicates token theft). |
| **Revocation** | Logout revokes the current session. Security stamp change revokes all sessions. |
| **Access token** | Remains a short-lived JWT (e.g. 15 minutes). Stored in memory in Angular (not localStorage). |

### MFA Flow

```
Login (email + password)
  ↓ credentials valid, MFA enabled
  ↓ return { requiresMfa: true, mfaChallengeToken: "..." }
  ↓
Frontend shows MFA challenge screen
  ↓ user enters TOTP code (or recovery code)
  ↓ POST /api/auth/mfa/verify { challengeToken, code }
  ↓
Backend validates code + challenge token
  ↓ success → issue access + refresh tokens
  ↓ failure → return error (with rate limiting)
```

The `mfaChallengeToken` is a short-lived, single-use JWT that proves primary authentication succeeded. It is not a session token.

### Step-Up Authentication Model

Sensitive actions require recent strong authentication (within configurable window, e.g. 10 minutes):

1. Frontend detects step-up is required (via 403 with `step_up_required` error code, or proactively before navigating to sensitive pages).
2. Frontend presents step-up dialog (TOTP code or password re-entry).
3. `POST /api/auth/step-up/verify` validates the challenge and issues a short-lived step-up claim.
4. Subsequent requests within the window include the elevated claim.

**Protected actions:**
- Viewing/modifying exchange credentials
- Enabling live trading
- Changing password
- Disabling MFA
- Revoking all sessions
- Deleting account

### Angular Changes

| Component | Change |
|-----------|--------|
| `AuthService` | Access token stored in memory (BehaviorSubject), not localStorage. Refresh via HttpOnly cookie (credentials: include). |
| `authInterceptor` | Updated to use in-memory token; refresh flow uses `withCredentials: true`. |
| `authGuard` | No change to API; token source changes from localStorage to AuthService getter. |
| New: MFA setup component | QR code display, code entry, recovery codes display |
| New: MFA challenge component | Code entry during login flow |
| New: Security settings page | Password change, MFA toggle, active sessions list, linked accounts |
| New: Step-up dialog | Modal for TOTP or password re-entry before sensitive actions |
| New: Email verification pending page | Shows after registration; "Resend" button |
| New: Forgot/reset password pages | Email entry, token-based reset form |

### Integration Points

| Integration | Protocol | Direction | Detail |
|-------------|----------|-----------|--------|
| Email provider (SendGrid/Mailgun) | HTTPS | Backend → Provider | Verification emails, password reset emails, security notifications |
| Microsoft Identity Platform | OIDC | Frontend → Microsoft → Backend | Microsoft sign-in flow (similar pattern to Google GIS) |
| Google Identity Services | OIDC | Frontend → Google → Backend | Existing; unchanged |

### Constraints

| Constraint | Impact |
|------------|--------|
| **SQLite (POC)** | No row-level locking; refresh token rotation race conditions must be handled in application code |
| **Single API instance (POC)** | In-process rate limiting is sufficient; must move to distributed rate limiter for multi-instance |
| **No email provider yet** | Email verification and password reset require configuring an SMTP/API provider |
| **TOTP secret encryption** | TOTP secrets must be encrypted at rest in the database; requires a data protection key |

---

## 6. Use Cases

### Personas

| Persona | Description |
|---------|-------------|
| **Subscriber** | A paying user who connects their Hyperliquid wallet and relies on the platform to trade on their behalf. Security-conscious; expects MFA, session management, and protection of exchange credentials. |
| **New visitor** | A potential subscriber evaluating the platform. Expects a smooth registration flow with social login options. |

### Features & User Stories

#### F1 — Local Registration & Email Verification

**As a new visitor, I want to register with my email and verify it so I can create a trusted account.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F1.1 | As a visitor, I want to register with email, display name, and password | Registration creates account; verification email sent; user redirected to "check your email" page |
| F1.2 | As a visitor, I want to verify my email by clicking a link | Clicking the verification link confirms the email; user can access all features |
| F1.3 | As a registered user, I want to resend the verification email if I didn't receive it | "Resend" button sends a new verification email; previous token invalidated |
| F1.4 | As a registered user, I want access to be limited until I verify my email | Unverified users can log in but cannot access exchange credentials or trading features |
| F1.5 | As a visitor, I want the registration response to not reveal whether an email is already registered | Neutral response returned; if email exists, a notification is sent to the existing email |

#### F2 — Local Login

**As a subscriber, I want to log in with my email and password securely.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F2.1 | As a subscriber, I want to log in with email and password | Valid credentials return access token (in response body) and refresh token (in HttpOnly cookie) |
| F2.2 | As a subscriber, I want the login response to not reveal whether MFA vs password was wrong | Generic "invalid credentials" message for all failure cases |
| F2.3 | As a subscriber with MFA enabled, I want to be prompted for my TOTP code after entering credentials | Login returns MFA challenge response; frontend shows TOTP entry |

#### F3 — Password Reset

**As a subscriber, I want to reset my password if I forget it.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F3.1 | As a subscriber, I want to request a password reset email | Neutral response always returned; email sent if account exists |
| F3.2 | As a subscriber, I want to set a new password using the reset link | Valid token allows password update; token is single-use |
| F3.3 | As a subscriber, I want all my sessions to be revoked after a password reset | All active refresh sessions are invalidated; security stamp rotated |

#### F4 — Microsoft Sign-In

**As a visitor, I want to sign in with my Microsoft account for convenience.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F4.1 | As a visitor, I want to sign in with Microsoft | Microsoft OIDC popup flow → ID token validated on backend → session created |
| F4.2 | As a subscriber, I want my Microsoft account linked to my existing account if emails match | Auto-linking by verified email; creates `ExternalIdentity` record |
| F4.3 | As a subscriber, I want to use both Google and Microsoft on the same account | Multiple `ExternalIdentity` records supported per user |

#### F5 — Refresh Token Rotation & Session Security

**As a subscriber, I want my sessions to be secure and manageable.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F5.1 | As a subscriber, I want my refresh token to rotate on each use | Each refresh request issues a new token and invalidates the previous one |
| F5.2 | As a subscriber, I want stolen token reuse to trigger automatic session revocation | Presenting a previously-rotated token revokes all sessions for the user |
| F5.3 | As a subscriber, I want to log out and have my session revoked | Logout endpoint revokes the current refresh session |
| F5.4 | As a subscriber, I want to view my active sessions | Security settings page lists sessions with device/browser, IP, and last-used time |
| F5.5 | As a subscriber, I want to revoke a specific session | "Revoke" button on individual session entries |
| F5.6 | As a subscriber, I want to revoke all other sessions | "Revoke All" button invalidates all sessions except current (requires step-up) |

#### F6 — TOTP MFA & Recovery Codes

**As a subscriber, I want to add a second factor to protect my account.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F6.1 | As a subscriber, I want to set up TOTP MFA from security settings | Setup returns QR code / manual entry key; user confirms with a valid code |
| F6.2 | As a subscriber, I want to receive recovery codes when I enable MFA | 8–10 recovery codes displayed once; user prompted to save them; only hashes stored |
| F6.3 | As a subscriber with MFA, I want to be challenged for TOTP during login | After valid password, MFA challenge screen appears |
| F6.4 | As a subscriber, I want to use a recovery code if I lose my authenticator | Recovery code accepted in place of TOTP; code is single-use |
| F6.5 | As a subscriber, I want to disable MFA (with step-up verification) | MFA removal requires step-up auth; TOTP secret deleted |
| F6.6 | As a subscriber, I want to regenerate recovery codes (with step-up verification) | Old codes invalidated; new codes displayed |

#### F7 — Step-Up Authentication

**As a subscriber, I want sensitive actions to require additional verification.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F7.1 | As a subscriber, I want to be prompted for re-authentication before sensitive actions | Accessing exchange credentials, enabling live trading, changing password, or disabling MFA triggers step-up |
| F7.2 | As a subscriber, I want to verify with TOTP or password re-entry | Step-up dialog accepts TOTP (if MFA enabled) or password |
| F7.3 | As a subscriber, I want the step-up window to last for a reasonable period | 10-minute configurable window before re-verification is required |
| F7.4 | As a subscriber, I want clear feedback when step-up is required | 403 with `step_up_required` code; frontend shows step-up dialog automatically |

#### F8 — Security Settings Page

**As a subscriber, I want a central place to manage my account security.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F8.1 | As a subscriber, I want to change my password | Password change requires step-up; updates hash; rotates security stamp |
| F8.2 | As a subscriber, I want to see linked social accounts | Security settings shows Google and/or Microsoft linked accounts |
| F8.3 | As a subscriber, I want to manage MFA from security settings | Enable/disable TOTP, view recovery code count, regenerate codes |
| F8.4 | As a subscriber, I want to view and manage active sessions | Session list with revoke controls |

#### F9 — Audit Logging & Rate Limiting

**As a platform operator, I want visibility into auth events and protection against abuse.**

| # | User Story | Acceptance Criteria |
|---|-----------|-------------------|
| F9.1 | As a platform operator, I want all auth events logged | Registration, login (success/failure), logout, password change, MFA events, session events, step-up events all produce audit records |
| F9.2 | As a platform operator, I want rate limiting on auth endpoints | Login: 5 attempts/minute/IP. Register: 3/minute/IP. Reset: 3/minute/IP. MFA verify: 5/minute/IP. |
| F9.3 | As a platform operator, I want security notifications sent to users | Email sent on: password changed, MFA enabled/disabled, new device login, all sessions revoked |

---

## 7. Implementation Order

| Phase | Features | Risk | Rationale |
|-------|----------|------|-----------|
| 1 | **F5 — Refresh token rotation & server-side sessions** | **High** | Fixes the most critical security gap (SG-1, SG-2, SG-3). Must be done first as all subsequent features depend on the session model. Includes moving to HttpOnly cookies and in-memory access tokens. |
| 2 | **F1 — Registration & email verification** | Medium | Fixes SG-4 and SG-5. Establishes email verification as a gate for sensitive features. Requires email sender integration. |
| 3 | **F3 — Password reset** | Low | Depends on email sender from Phase 2. Standard flow. |
| 4 | **F2 — Login hardening + F9 — Rate limiting & audit logging** | Medium | Adds brute-force protection (SG-6) and audit trail (SG-11). Rate limiting should be in place before MFA to protect MFA endpoints too. |
| 5 | **F6 — TOTP MFA & recovery codes** | Medium | Fixes SG-7. Requires the MFA challenge flow integration with login. |
| 6 | **F7 — Step-up authentication** | Medium | Fixes SG-10. Depends on MFA being available as a step-up method. |
| 7 | **F4 — Microsoft sign-in + multi-provider linking** | Medium | Fixes SG-8. `ExternalIdentity` table migration. Google flow updated to use new table. |
| 8 | **F8 — Security settings page (Angular)** | Low | Aggregation of all security management UI. Most backend APIs built in earlier phases. |

---

## 8. Open Questions

### Assumptions

| # | Assumption | Impact if Wrong |
|---|-----------|----------------|
| A-1 | PBKDF2 (ASP.NET Identity default, 600k+ iterations) is sufficient for password hashing | Need to add Argon2id NuGet package and implement rehash-on-login logic |
| A-2 | In-process rate limiting (`System.Threading.RateLimiting`) is sufficient for POC/single-instance | Multi-instance deployment requires distributed rate limiter (Redis) |
| A-3 | SMTP/API email provider will be available (SendGrid, Mailgun, or similar) | Email verification and password reset are blocked without email delivery |
| A-4 | TOTP secret encryption can use ASP.NET Data Protection APIs | If Data Protection key management is deemed insufficient, need Azure Key Vault integration earlier |
| A-5 | Microsoft sign-in can use the same popup/ID-token pattern as Google (MSAL.js) | If Microsoft requires a server-side OIDC redirect flow, the implementation pattern differs from Google |

### Unresolved

| # | Question | Context |
|---|----------|---------|
| OQ-1 | Which email provider should be used for verification and reset emails? | SendGrid, Mailgun, and AWS SES are all viable. Needs a decision before Phase 2. |
| OQ-2 | Should unverified users be able to log in at all, or should login be blocked until email is verified? | Current proposal: allow login but restrict access to sensitive features. Alternative: block login entirely. |
| OQ-3 | What is the refresh token expiry? | Prompt pack does not specify. Common values: 7 days (short), 30 days (standard), 90 days (long). Needs alignment with session management expectations. |
| OQ-4 | Should the TOTP secret be encrypted with a per-user key or a shared application key? | Per-user key is more secure but adds complexity. Shared key is simpler but means a key compromise exposes all TOTP secrets. |
| OQ-5 | Should rate limiting be per-IP only, or also per-account (email)? | Per-IP alone can be bypassed with distributed attacks. Per-account adds account lockout risk. Combined approach is recommended but adds complexity. |
| OQ-6 | What is the access token lifetime? | Current value needs to be confirmed. Shorter (5 min) is more secure but increases refresh traffic. Longer (15–30 min) is more practical for a trading dashboard that requires continuous connectivity. |
| OQ-7 | Should the `ExternalIdentity` migration happen in Phase 1 (with refresh token work) or Phase 7 (with Microsoft sign-in)? | Migrating the data model earlier reduces risk but adds scope to Phase 1. |
