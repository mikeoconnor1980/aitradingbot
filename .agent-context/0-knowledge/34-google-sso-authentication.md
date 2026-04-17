# Google SSO Authentication

Google Single Sign-On (SSO) allows users to sign in or register using their Google account as an alternative to email/password. The implementation uses Google Identity Services (GIS) SDK on the frontend and server-side ID token validation on the backend.

## Architecture

```
Browser (Angular)
  ↓ Google Identity Services SDK
  ↓ User clicks "Sign in with Google" button
  ↓ Google popup → user authenticates → returns ID token (JWT)
  ↓ POST /api/auth/google { idToken }
API (ASP.NET Core)
  ↓ GoogleTokenValidator validates ID token against Google's public keys
  ↓ Extracts: subject (unique Google user ID), email, name
  ↓ Find or create user, link accounts by email if needed
  ↓ Issues app JWT tokens (same as email/password login)
Browser
  ↓ Stores tokens in localStorage (identical to standard login)
  ↓ Navigates to /dashboard
```

The Google auth flow reuses the existing JWT infrastructure entirely — once a Google user is authenticated, they receive the same access/refresh tokens as email/password users.

## Key Components

| Component | Path | Purpose |
|-----------|------|---------|
| `GoogleTokenValidator` | `src/TradePilot.Infrastructure/Services/GoogleTokenValidator.cs` | Validates Google ID tokens using `Google.Apis.Auth` |
| `IGoogleTokenValidator` | `src/TradePilot.Application/Abstractions/Auth/IGoogleTokenValidator.cs` | Interface + `GoogleUserInfo` record |
| `GoogleAuthOptions` | `src/TradePilot.Application/Abstractions/Auth/GoogleAuthOptions.cs` | Config binding for `Google:ClientId` |
| `AuthController.GoogleSignIn` | `src/TradePilot.Api/Controllers/AuthController.cs` | `POST /api/auth/google` endpoint |
| `GoogleAuthService` | `frontend/trading-ui/src/app/core/services/google-auth.service.ts` | Wraps GIS SDK `initialize()` and `renderButton()` |
| `AuthService.googleSignIn()` | `frontend/trading-ui/src/app/core/services/auth.service.ts` | POSTs Google ID token to backend |

## User Entity Changes

The `User` entity supports external authentication providers:

| Property | Type | Purpose |
|----------|------|---------|
| `PasswordHash` | `string?` (nullable) | Null for Google-only users |
| `AuthProvider` | `string?` | Provider name (e.g., `"Google"`) or null for local accounts |
| `ExternalProviderId` | `string?` | Provider's unique user ID (Google `sub` claim) |

Factory methods:
- `User.Create(email, displayName, passwordHash)` — local registration (unchanged)
- `User.CreateExternal(email, displayName, authProvider, externalProviderId)` — external provider registration
- `User.LinkExternalProvider(authProvider, externalProviderId)` — links existing local account to external provider

The repository surface also includes `IUserRepository.GetByExternalProviderAsync(provider, externalId)` so the backend can resolve returning Google users before falling back to email-based account linking.

## Authentication Flow

### Google Sign-In (new or returning user)

1. User clicks Google button on login/register page
2. GIS SDK opens Google popup → user authenticates
3. GIS callback returns a Google ID token (JWT signed by Google)
4. Frontend sends `POST /api/auth/google { idToken }` to backend
5. Backend validates token via `GoogleJsonWebSignature.ValidateAsync()` with audience check
6. Backend looks up user by `(AuthProvider, ExternalProviderId)` — if found, issues tokens
7. If not found, looks up by email — if found, **auto-links** the Google identity to the existing account
8. If no user exists at all, creates a new user via `User.CreateExternal()`
9. Returns standard `AuthResponse` (access token, refresh token, user info)

### Local Login Guard

If a user registered via Google (no password) and tries `POST /api/auth/login` with email/password, the endpoint returns `400 external_auth_only` with the message "This account uses Google sign-in."

## Configuration

### Backend — `appsettings.json`

`Program.cs` binds `GoogleAuthOptions` with:

```csharp
builder.Services.AddOptions<GoogleAuthOptions>()
    .Bind(builder.Configuration.GetSection(GoogleAuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

The checked-in option class binds the `Google` section and currently requires the Client ID. Any secret material should stay in environment configuration or a managed secret store, not in this document.

```json
{
  "Google": {
    "ClientId": "your-client-id.apps.googleusercontent.com"
  }
}
```

### Frontend — `environment.ts`

```typescript
export const environment = {
  // ... other config
  googleClientId: "your-client-id.apps.googleusercontent.com"
};
```

**Both must use the same Client ID.**

## Google Cloud Console Setup

### Step 1: Create a Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click **Select a project** → **New Project**
3. Name it (e.g., `TradePilot`) → **Create**

### Step 2: Configure the OAuth Consent Screen

1. Navigate to **APIs & Services** → **OAuth consent screen**
2. Select **External** user type → **Create**
3. Fill in required fields:
   - **App name**: `TradePilot`
   - **User support email**: your email
   - **Developer contact information**: your email
4. Click **Save and Continue** through Scopes (defaults are fine — `email`, `profile`, `openid`)
5. On the **Test users** page, add your Google email for testing
6. Click **Save and Continue** → **Back to Dashboard**

### Step 3: Create OAuth 2.0 Credentials

1. Navigate to **APIs & Services** → **Credentials**
2. Click **+ Create Credentials** → **OAuth client ID**
3. Application type: **Web application**
4. Name: `TradePilot Web` (or any name)
5. **Authorized JavaScript origins**:
   - `http://localhost:4200` (Angular dev server)
   - `https://gentle-river-027f7d003.6.azurestaticapps.net` (Azure Static Web Apps production)
6. **Authorized redirect URIs**: leave empty (GIS popup flow doesn't use redirects)
7. Click **Create**
8. Copy the **Client ID** (format: `xxxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com`)

### Step 4: Configure the Application

1. **Backend** — open `src/TradePilot.Api/appsettings.Development.json`:
   ```json
   {
     "Google": {
       "ClientId": "PASTE_YOUR_CLIENT_ID_HERE"
     }
   }
   ```

2. **Frontend** — open `frontend/trading-ui/src/environments/environment.ts`:
   ```typescript
   export const environment = {
     production: false,
     apiBaseUrl: "http://localhost:5062/api",
     hubBaseUrl: "http://localhost:5062/hubs/marketdata",
     appVersion: "0.1.0",
     googleClientId: "PASTE_YOUR_CLIENT_ID_HERE"
   };
   ```

3. Both values must be **identical** — the same Client ID.

### Step 5: Apply Database Migration

Run the EF Core migration to add the new columns to the Users table:

```bash
dotnet ef database update --project src/TradePilot.Persistence --startup-project src/TradePilot.Api
```

### Step 6: Verify

1. Start the API: `dotnet run --project src/TradePilot.Api`
2. Start the UI: `cd frontend/trading-ui && ng serve`
3. Navigate to `http://localhost:4200/login`
4. The Google "Sign in with Google" button should appear below the login form
5. Click it → Google popup opens → authenticate → redirected to dashboard
6. Verify the same button appears on the register page

### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Google button doesn't appear | GIS SDK not loaded | Check `index.html` has the `<script src="https://accounts.google.com/gsi/client">` tag |
| "Google Identity Services SDK not loaded" console warning | Script blocked by ad blocker or CSP | Disable ad blocker for localhost, or check Content Security Policy headers |
| 401 "Invalid Google token" from API | Client ID mismatch between frontend and backend | Ensure both `environment.ts` and `appsettings.json` have the same Client ID |
| "idpiframe_initialization_failed" in console | `http://localhost:4200` not in authorized JavaScript origins | Add it in Google Cloud Console → Credentials → Edit the OAuth client |
| Google popup opens but immediately closes | Consent screen not configured or app not in test mode | Add your Google email to test users in OAuth consent screen |

## Production Deployment

For production:
1. Add `https://gentle-river-027f7d003.6.azurestaticapps.net` to **Authorized JavaScript origins** in Google Cloud Console
2. Set `Google:ClientId` in production `appsettings.json` (or via environment variable / Key Vault)
3. Set `googleClientId` in `environment.prod.ts`
4. Publish the OAuth consent screen (move from Testing to Published status in Google Cloud Console)

## Extensibility

The `AuthProvider` + `ExternalProviderId` pattern supports additional providers (GitHub, Microsoft, etc.) without schema changes. To add a new provider:
1. Create a new token validator implementing validation for that provider
2. Add a new endpoint (e.g., `POST /api/auth/github`)
3. Add frontend button + SDK integration
4. No database migration needed — reuses the same columns


## Google User Info Shape

`GoogleUserInfo` currently carries four fields:

- `Subject`
- `Email`
- `Name`
- `Picture`

The `Picture` field is the Google profile-image URL returned by token validation.


### Credentials 
- Client Id: 894614860421-8o8t4h5oc7baj3he9adtl4p5jroomho5.apps.googleusercontent.com
- Client Secret: <stored in environment/secrets>

## Future Recommendations

- Move production Google configuration to managed secret storage alongside other deployment secrets.
- Persist and surface Google profile-picture data in the UI if avatar support becomes part of the user profile experience.
- Add provider-specific operational tests covering client-ID mismatch, external-auth-only login, and account-linking edge cases.
