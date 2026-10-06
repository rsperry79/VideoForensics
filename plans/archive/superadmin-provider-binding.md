# SuperAdmin Provider Binding & Smart Auth Redirects

## Context

VideoForensics operators currently sign in via password or passkey (WebAuthn). Provider accounts (Ring, Uniview) are managed separately in the `/accounts` section. SuperAdmin users need an integrated way to link provider accounts for authentication, and the auth flow should intelligently redirect based on context:

- **Fresh sign-in** → go to home page
- **Re-authentication for a protected page** → return to that page
- **New operator registration** → show available providers as sign-in options

This plan builds on the recently completed auth work (SuperAdmin-only passkeys, local user creation toggle).

## Schema Changes

### ProviderAccount Extension

**Key Decision**: Extend ProviderAccount to link directly to Operator (in addition to User)

**Update**: `src/data/common/data.common/Entities/ProviderAccount.cs`

Add property:
```csharp
public Guid? OperatorId { get; set; }  // NEW: Link to Operator for SuperAdmin binding
// Existing properties remain: UserId, ProviderName, LinkedUtc, IsActive, LastErrorMessage, etc.
```

**Create migration** to add `OperatorId` column with nullable constraint.

**Rationale**: Existing ProviderAccount links to `User` (provider's user model for device management). For SuperAdmin authentication, we need an `OperatorId` link to distinguish operator-level bindings from device-level bindings. A ProviderAccount can have both `UserId` (device account) and `OperatorId` (operator login account).

## Features to Implement

### 1. SuperAdmin Profile Page (`/settings/superadmin`)

**New file**: `src/client/ui/VideoForensics.Ui.Shared/Pages/SuperAdminProfile.razor`

- SuperAdmin-only page (role-gated via `PairedSessionState`)
- Display SuperAdmin info (username, email, etc.)
- **Provider Binding Section**:
  - List of enabled providers (Ring, Uniview, Wyze)
  - "Sign in with [Provider]" button for each (same style/pattern as existing account switching)
  - Shows currently linked provider account (if any)
  - Action to link a new provider account
  - Action to unlink a provider

**Add to NavGroups.cs**:
```csharp
new("SuperAdmin Profile", "/settings/superadmin", ctx => ctx.HasRole(OperatorRole.SuperAdmin))
```

### 2. Provider OAuth Sign-In Buttons

**Update**: `src/client/ui/VideoForensics.Ui.Shared/Components/AuthForm.razor`

- Current pattern: AuthForm component already supports provider selection via dropdown
- Enhance with **visual buttons** (similar to "Sign in with Google/Microsoft" patterns)
- Each enabled provider gets a styled button showing the provider name/logo
- On click: triggers provider auth via existing `IProviderAuthService` flow
- Supports 2FA callback for providers requiring it (e.g., Ring with 2FA enabled)

**New file**: `src/client/ui/VideoForensics.Ui.Shared/Components/ProviderSignInButtons.razor`
- Reusable component showing provider buttons
- Queries `/api/v1/auth/providers` to get enabled providers
- Dispatches provider login and handles 2FA flow

### 3. Smart Post-Auth Redirect System

**Key Decision**: Store redirect context in both session state + query parameter for reliability

**Enhance**: `src/client/ui/VideoForensics.Ui.Shared/Services/PairedSessionState.cs`

Add properties:
```csharp
public string? AuthReturnUrl { get; set; }  // Where to go after auth
public string? AuthContext { get; set; }    // "signin" or "challenge"
```

**Enhance**: `src/client/ui/VideoForensics.Ui.Shared/Extensions/NavigationManagerExtensions.cs`

Add helper that stores context in session:

```csharp
public static string SignInPathWithContext(this NavigationManager nav, 
    PairedSessionState session, bool wasSignedIn = false)
{
    string returnUrl = "/" + nav.ToBaseRelativePath(nav.Uri);
    string context = wasSignedIn ? "challenge" : "signin";
    session.AuthReturnUrl = returnUrl;
    session.AuthContext = context;
    // Also pass as query param as backup
    return $"/signin?returnUrl={Uri.EscapeDataString(returnUrl)}&context={context}";
}
```

**Update**: `src/client/ui/VideoForensics.Ui.Shared/Pages/SignIn.razor`

- Extract `context` query parameter (defaults to "signin")
- After successful auth:
  - If `context=challenge`: redirect to returnUrl (user was re-authenticating)
  - If `context=signin`: redirect to home (fresh login)
  - Fallback: home page

**Update**: Protected pages (Evidence.razor, etc.)

```csharp
@if (!_isAuthenticated)
{
    <p>@NavigationManager.SignInPathWithContext(wasSignedIn: false)</p>
}
```

### 4. Provider Selection on Operator Registration

**Update**: `src/client/ui/VideoForensics.Ui.Shared/Pages/Register.razor`

Add provider sign-in alternative:
- Show "Register with email/password" (existing)
- Show "Sign in with [Provider]" buttons (new)
- Allow users to link a provider account during registration
- Creates operator account + links ProviderAccount via existing API flow

## Implementation Order (Haiku Subagents)

TDD-first (test before code), lite gate after each step:

1. **SuperAdminProfile.razor** - New page with role-gating
2. **NavGroups.cs update** - Add SuperAdmin Profile nav entry
3. **SignIn.razor enhancement** - Add `context` parameter handling
4. **NavigationManagerExtensions.cs** - Add `SignInPathWithContext()` helper
5. **ProviderSignInButtons.razor** - New reusable component
6. **AuthForm.razor update** - Integrate provider buttons UI
7. **Register.razor update** - Add provider sign-in alternatives
8. **Protected pages update** - Use `SignInPathWithContext()` for redirects
9. **API integration test** - Test provider OAuth flow end-to-end

## Verification

- Navigate to `/settings/superadmin` as SuperAdmin → confirm page loads with provider binding section
- Click "Sign in with Ring" → confirm OAuth flow initiates
- Protected page → not signed in → click sign-in link → redirect to `/signin?returnUrl=...&context=signin`
- Protected page → already signed in → need re-auth → redirect to `/signin?returnUrl=...&context=challenge` → after auth → return to original page
- Registration page → show provider buttons alongside email/password form
- Full test suite: `dotnet test` (all 177+ tests pass)

## Critical Files (Read-Only Exploration)

- `src/client/ui/VideoForensics.Ui.Shared/Pages/SignIn.razor` - Current auth page
- `src/client/ui/VideoForensics.Ui.Shared/Pages/DeviceSignIn.razor` - Device sign-in pattern
- `src/client/ui/VideoForensics.Ui.Shared/Components/AuthForm.razor` - Existing auth component
- `src/client/ui/VideoForensics.Ui.Shared/Extensions/NavigationManagerExtensions.cs` - Redirect helpers
- `src/client/ui/VideoForensics.Ui.Shared/Layout/NavGroups.cs` - Navigation structure + role-gating
- `src/client/ui/VideoForensics.Ui.Shared/Services/PairedSessionState.cs` - Auth context
- `src/client/web/VideoForensics.WebApp/Api/AuthEndpoints.cs` - Provider auth endpoints
- `src/client/web/VideoForensics.WebApp/Api/OperatorAuthEndpoints.cs` - Operator auth endpoints

## Branch & Execution

- Branch name: `feature/superadmin-provider-binding-redirects`
- Off `dev` branch
- Dispatch each numbered task to Haiku subagents
- Lite gate (build + scoped tests) after each task
- Full gate before PR
