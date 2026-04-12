# Copilot Build Prompt Pack
## Authentication System for Angular + .NET App

This prompt pack is designed to help GitHub Copilot (or another coding assistant) implement the authentication system for the app in manageable phases.

## Scope
Included:
- Local email/password authentication
- Google sign-in
- Microsoft sign-in
- Email verification
- Password reset
- TOTP MFA
- Recovery codes
- Passkeys (WebAuthn / FIDO2)
- Refresh token rotation
- Session management
- Step-up authentication for sensitive actions
- Angular frontend integration
- .NET backend integration
- Audit logging

Excluded:
- Enterprise SSO
- Magic links
- Wallet sign-in

---

# How to Use This Pack

## Recommended workflow
Use these prompts in order:

1. Foundation and auth architecture
2. Database and domain model
3. Local auth flows
4. Social login flows
5. Token/session management
6. MFA
7. Passkeys
8. Step-up authentication
9. Angular UI and guards
10. Audit logging and hardening
11. Testing
12. Final refactor and documentation

## Working rules for Copilot
Apply these rules in every implementation step:

- Use **ASP.NET Core** for backend
- Use **Angular** for frontend
- Keep the auth layer modular and testable
- Do not store reversibly encrypted passwords
- Use **Argon2id** for password hashing where practical
- Use **JWT access tokens** with **rotating refresh tokens**
- Keep access tokens short-lived
- Store refresh token server-side as a **hash**
- Prefer **HttpOnly Secure SameSite cookies** for refresh token transport
- Keep authentication concerns separate from exchange API credentials and trading secrets
- Build with future extensibility in mind
- Write clear interfaces, service abstractions, and DTOs
- Include error handling, validation, logging, and unit-testable seams
- Avoid over-engineering with unnecessary generic abstractions
- Follow clean, pragmatic .NET design
- Avoid security anti-patterns such as exposing account existence through error messages

---

# Prompt 1 - Foundation and Authentication Architecture

## Prompt
You are building the authentication subsystem for a SaaS trading-related app using ASP.NET Core backend and Angular frontend.

Build the foundational architecture for a modular authentication system with these features planned:
- local email/password auth
- Google login
- Microsoft login
- TOTP MFA
- passkeys
- refresh token rotation
- session management
- step-up auth
- audit logging

Requirements:
- create a clean project structure for the auth subsystem
- define core service interfaces and responsibilities
- define domain entities and DTO folders
- define configuration classes/options objects
- define controller structure and route grouping
- define where token issuance, password hashing, MFA, passkeys, and audit logging belong
- separate application services from infrastructure concerns
- keep room for future providers without building enterprise SSO

Output:
- proposed folder structure
- class/interface list with responsibilities
- dependency graph
- brief explanation of boundaries between layers
- example startup/DI registration skeleton
- no placeholder buzzwords; give concrete code-oriented structure

Important constraints:
- passwords must be hashed, not encrypted
- auth must remain clearly separate from exchange credentials and trading worker secrets
- do not implement everything yet; focus only on the architectural foundation

---

# Prompt 2 - Database Schema and Domain Model

## Prompt
Using the authentication architecture already defined, design and implement the database schema and domain model for the auth system.

Include entities/tables for:
- Users
- ExternalIdentities
- RefreshSessions
- MfaTotp
- PasskeyCredentials
- EmailVerificationTokens
- PasswordResetTokens
- AuditEvents

Requirements:
- use GUID/UUID identifiers
- include created/updated timestamps where useful
- store password hash only
- store refresh token hashes, not raw refresh tokens
- model nullable password fields for social-only accounts
- include security stamp/version fields needed for invalidation
- include useful indexes and unique constraints
- include EF Core entity configurations
- generate migration-ready code structure
- define enums/value objects only where they add clarity

Output:
- entity classes
- EF Core configurations
- DbContext additions
- notes on indexes and constraints
- migration considerations
- explanation of how account linking and session revocation are supported by the schema

Do not implement controllers or services in this step.

---

# Prompt 3 - Local Registration, Email Verification, and Password Login

## Prompt
Implement local authentication flows for ASP.NET Core.

Required flows:
- register with email and password
- create hashed password using Argon2id where practical
- create email verification token
- login with email and password
- verify password safely
- resend email verification
- verify email by token

Requirements:
- use DTOs and validation models
- avoid leaking whether an email exists where appropriate
- write service-layer code and controller endpoints
- include domain/application errors with safe user-facing responses
- produce audit events for registration, login success/failure, and email verification
- email verification should be required before full access to sensitive features
- use a token generation approach suitable for signed, time-limited verification links
- store only token hashes if applicable

Output:
- controller code
- service implementations
- request/response DTOs
- validation rules
- token generation/verification approach
- sample email-verification integration points
- minimal unit-test examples for core services

Do not implement forgot/reset password yet unless needed for structure.

---

# Prompt 4 - Forgot Password and Reset Password

## Prompt
Implement forgot-password and reset-password flows in the ASP.NET Core auth subsystem.

Requirements:
- user submits email for password reset
- always return a neutral response
- generate signed or random reset token with expiry
- store only a secure token hash if using opaque tokens
- verify reset token safely
- invalidate old reset tokens once used
- update password hash using Argon2id
- revoke active refresh sessions after password reset
- update security stamp/version after password reset
- create audit events for reset requested and password reset completed

Output:
- endpoints
- service implementations
- token model
- email integration points
- password update logic
- revocation logic
- sample tests

Keep responses safe from account enumeration.

---

# Prompt 5 - JWT Access Tokens and Rotating Refresh Tokens

## Prompt
Implement token/session handling for the auth system.

Requirements:
- issue short-lived JWT access tokens
- issue random opaque refresh tokens
- store refresh token hashes in the database
- rotate refresh token on each refresh request
- revoke refresh tokens on logout
- support revoke-all-sessions
- record user agent and IP metadata where useful
- ensure refresh token reuse detection can be added cleanly
- access token should contain only minimal claims
- support a clean auth result model used by local and social login flows
- refresh token should be transported via HttpOnly Secure SameSite cookie unless there is a strong reason otherwise

Output:
- token service interfaces and implementations
- refresh session persistence logic
- controller endpoints for refresh/logout/revoke-all
- JWT claims model
- cookie-writing helpers or middleware integration notes
- session query model for later “active sessions” UI
- example tests for token rotation logic

Include notes on how security stamp changes should invalidate sessions.

---

# Prompt 6 - Google and Microsoft Sign-In

## Prompt
Implement Google and Microsoft sign-in for the ASP.NET Core backend and define the Angular integration pattern.

Requirements:
- use OpenID Connect / external auth handlers
- support Google and Microsoft
- define start and callback endpoints
- on callback:
  - find existing ExternalIdentity by provider and provider subject
  - else find matching internal verified email
  - else create a new user
- define safe account linking rules
- do not rely only on email as the external identity key
- issue internal access/refresh tokens after successful external auth
- write audit events for successful external sign-in and account linking
- produce Angular flow notes for initiating provider login and handling callback completion

Output:
- backend auth handler setup
- callback orchestration service
- external identity linking logic
- safe account creation/linking rules
- controller endpoints
- Angular flow notes
- tests for account linking edge cases

Do not implement enterprise SSO.

---

# Prompt 7 - TOTP MFA and Recovery Codes

## Prompt
Implement TOTP MFA for the auth system.

Requirements:
- user can start TOTP setup while authenticated
- backend generates TOTP secret and otpauth URI
- frontend can display QR code from returned setup data
- user confirms setup by submitting a valid TOTP code
- store TOTP secret encrypted at rest
- generate 8-10 one-time recovery codes
- store only hashes of recovery codes
- require MFA after primary authentication when enabled
- support MFA verification step during login
- allow recovery code fallback
- support disabling MFA with step-up protection
- generate audit events for MFA enabled, disabled, and recovery codes regenerated

Output:
- TOTP setup endpoints
- TOTP verification endpoints
- recovery code generation and verification logic
- pending-auth or challenge model for post-primary-login MFA step
- controller/service code
- test coverage for successful and failed MFA flows

Also explain how this integrates with social login and password login.

---

# Prompt 8 - Passkeys (WebAuthn / FIDO2)

## Prompt
Implement passkey support for the auth system using WebAuthn/FIDO2 concepts.

Requirements:
- allow authenticated users to register passkeys
- allow users to list and revoke passkeys
- support passkey authentication flow
- backend must validate challenge, origin, RP ID, credential ownership, signature, and sign counter handling
- store passkey credential metadata in a PasskeyCredentials table
- allow passkeys to be used later as a step-up method
- expose friendly device information where practical
- generate audit events for passkey registration and removal

Output:
- backend endpoints for register options / register complete
- backend endpoints for assert options / assert complete
- passkey service abstractions
- persistence model integration
- Angular flow notes for navigator.credentials APIs
- validation notes and security checks
- tests or test strategy notes

Keep implementation practical and production-oriented.

---

# Prompt 9 - Step-Up Authentication for Sensitive Actions

## Prompt
Implement a reusable step-up authentication mechanism for sensitive actions in the app.

Sensitive actions include:
- changing password
- changing email
- disabling MFA
- removing all passkeys
- viewing or modifying exchange credential settings
- enabling live trading
- revoking all sessions
- deleting account

Requirements:
- define a policy-based or service-based step-up model
- a recent strong-auth window should be tracked, e.g. 10-15 minutes
- acceptable step-up methods:
  - TOTP
  - passkey
  - fresh primary re-authentication
- integrate with both password and social-login accounts
- expose a clean backend API for initiating and confirming step-up
- provide a way for Angular to know whether the current session is step-up verified
- write audit events for step-up success/failure where useful

Output:
- design explanation
- middleware/filter/policy or service approach
- endpoints and DTOs
- sample protection on at least two sensitive actions
- Angular integration notes
- test strategy

Keep the design pragmatic rather than over-abstract.

---

# Prompt 10 - Angular Authentication Module and UX Flows

## Prompt
Build the Angular authentication module and security UI to integrate with the ASP.NET Core auth backend.

Required screens/components:
- Login
- Register
- Verify email pending
- Forgot password
- Reset password
- MFA challenge
- Security settings
- Sessions/devices
- Passkeys management

Requirements:
- create Angular services for auth state and backend calls
- keep access token in memory
- rely on refresh flow to restore session on app startup
- define route guards for:
  - authenticated user
  - verified email
  - MFA-complete auth state
  - step-up required routes
- include Google and Microsoft sign-in buttons
- include passkey login option where supported
- use safe error messaging
- build forms with validation and sensible UX states
- design the security settings page to manage password, MFA, sessions, and passkeys

Output:
- Angular folder/module structure
- service interfaces/classes
- route guard design
- component list
- auth state model
- example component code or scaffolding
- callback flow handling notes
- UX notes for security settings and session management

Use Angular best practices and keep it realistic.

---

# Prompt 11 - Audit Logging, Notifications, and Security Hardening

## Prompt
Add audit logging, security notifications, and hardening to the auth system.

Requirements:
- write audit events for major auth/security actions
- define a clean audit event publisher/service
- add rate limiting to login, reset, verification, MFA, and passkey endpoints
- add safe CSRF considerations where cookie-based flows apply
- ensure secure cookie usage
- define security notification triggers for:
  - password changed
  - MFA enabled/disabled
  - passkey added/removed
  - email changed
  - suspicious login or new device if feasible
- provide safe logging guidance so secrets and tokens are never logged
- document brute-force protection strategy
- document session invalidation triggers

Output:
- code-oriented hardening checklist
- audit event service or publisher pattern
- notification trigger design
- endpoint protection notes
- example configuration snippets
- test ideas for critical security cases

Keep the answer implementation-focused, not generic-policy heavy.

---

# Prompt 12 - Integration Tests and End-to-End Auth Test Plan

## Prompt
Create an integration and end-to-end test plan for the authentication system across backend and Angular frontend.

Cover:
- local registration
- email verification
- password login
- forgot/reset password
- refresh token rotation
- logout
- revoke-all sessions
- Google login orchestration
- Microsoft login orchestration
- MFA challenge flow
- recovery code use
- passkey register/login/revoke
- step-up protected actions

Requirements:
- define integration test scope for backend
- define mocking/stubbing strategy for email, provider callbacks, and WebAuthn dependencies
- define end-to-end scenarios for Angular + API
- call out security regression tests explicitly
- identify which tests should be unit, integration, or E2E

Output:
- test matrix
- recommended tooling
- key test cases
- example test skeletons
- risks and test gaps to watch for

---

# Prompt 13 - Final Refactor, Documentation, and Developer Handover

## Prompt
Review the full authentication implementation and prepare it for maintainable handover.

Requirements:
- identify areas to refactor for clarity
- remove duplication across password, social, MFA, and passkey flows
- ensure consistent DTO naming and error contracts
- ensure security-sensitive code paths are clearly documented
- generate a concise developer handover guide
- generate a configuration checklist for local/dev/test/prod environments
- generate a rollout checklist for enabling features in phases
- identify known future extension points without implementing enterprise SSO

Output:
- refactor recommendations
- developer handover notes
- environment/configuration checklist
- rollout checklist
- known limitations and extension points

---

# Optional Master Orchestrator Prompt

Use this when you want Copilot to work through the auth system in sequence without trying to do everything blindly in one shot.

## Prompt
You are implementing a production-oriented authentication system for an Angular + ASP.NET Core application.

Features in scope:
- local email/password auth
- Google sign-in
- Microsoft sign-in
- email verification
- forgot/reset password
- JWT access tokens
- rotating refresh tokens
- TOTP MFA
- recovery codes
- passkeys
- session management
- step-up authentication
- audit logging

Features out of scope:
- enterprise SSO
- magic links
- wallet sign-in

Instructions:
- work in incremental phases
- before writing code for each phase, restate the exact scope of that phase
- preserve clean architecture boundaries
- prefer pragmatic maintainable code over excessive abstraction
- use hashed passwords, never encrypted passwords
- keep refresh tokens hashed in persistence
- keep auth separate from trading credentials and worker secrets
- include DTOs, validation, tests, and migration considerations where relevant
- call out assumptions clearly
- where external providers or browser APIs are involved, provide realistic integration notes rather than fake certainty

Sequence:
1. foundation
2. domain/schema
3. local auth
4. password reset
5. token/session model
6. external login
7. MFA
8. passkeys
9. step-up auth
10. Angular integration
11. hardening
12. tests
13. refactor/handover

For each phase, output:
- what is being implemented
- code/files to add or change
- implementation details
- edge cases
- tests
- follow-up tasks for the next phase

---

# Suggested Copilot Usage Tips

## Tip 1
Feed one prompt at a time.
Do not paste the whole pack and expect a perfect first shot.

## Tip 2
After each prompt, ask Copilot to:
- show exact files to add
- show exact files to change
- explain assumptions
- identify risks

## Tip 3
When Copilot produces code, follow with:
"Now review your own output for security issues, missing validation, token leakage, race conditions, account enumeration risks, and session invalidation gaps."

## Tip 4
For difficult flows, use a second-pass prompt:
"Now generate tests for the code you just proposed, including failure cases and abuse cases."

## Tip 5
For frontend/backend coordination, ask:
"Now align the Angular DTOs, route guards, and auth-state assumptions with the backend contracts you just defined."

---

# Final Note

This prompt pack is designed to help you build the auth system in a sequence that is:
- realistic
- secure
- modular
- suited to an Angular + .NET application
- appropriate for a finance-adjacent trading product

The most important guardrails are:
- hash passwords, never encrypt them
- hash refresh tokens in storage
- use MFA and step-up auth for sensitive operations
- keep identity/auth separate from exchange and trading secrets
