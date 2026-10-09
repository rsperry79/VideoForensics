# MAUI local app-lock verification

**Status on origin/dev (checked 2026-10-09)**: CHANGED. The lock is built and shipped. What remains is a manual check on a real device. The "local unlock satisfies step-up" idea is not built and should be dropped.

**Source**: `plans/archive/maui-blazor-hybrid-conversion.md` line 19 (the `[~]` "MAUI local app-lock via `ILocalAuthGate`/Windows Hello" bullet, under M6). It says: "Not verified against a real Windows Hello prompt" and "Not wired: the plan's noted synergy where a fresh local unlock could satisfy step-up re-auth directly".

## Verdict
Part 1 is STILL OPEN as a manual check only; no code is needed. Part 2 (local unlock satisfies step-up) is not a wanted feature and should be closed as "won't do". Step-up exists to prove a fresh server-verified passkey or password assertion for one dangerous action. `StepUpAuthService` mints tokens bound to a paired device, and its doc comment says the escalation-flagged design must not be collapsed into weaker checks. A client-side Windows Hello result is an unverifiable boolean that the server cannot trust, so accepting it would weaken the guarantee.

## Scope
- Verified on origin/dev by grepping `ILocalAuthGate`, `StepUp` and `AppLock`:
  - `ILocalAuthGate` is in `src/client/common/VideoForensics.Client.Common/Contracts/`.
  - `FingerprintLocalAuthGate` wraps Plugin.Fingerprint.Maui and is registered in `MauiProgram.cs:206`.
  - `App.xaml.cs` and `AppLockPage` use the gate.
  - The `AppLock.razor` page and `IAppLockPreferencesStore` exist.
- Step-up on origin/dev:
  - `IStepUpAuthService`/`StepUpAuthService` (`src/client/host/VideoForensics.Hosting/StepUpAuthService.cs`) issues a 2-minute, device-bound token.
  - `StepUpEndpointFilter` enforces the token.
  - `WebAuthnClient.StepUpAsync` and `StepUpWithPasswordAsync` are the client side.
  - Step-up now also supports password, not just passkey. The archive text predates this.
- No test project exists for `VideoForensics.MauiApp` on origin/dev, so the gate has no automated coverage. The `AuthenticateAsync` contract is already written fail-closed ("true only on explicit success").
- Out of scope: a MAUI unit-test project. MAUI Windows targets are awkward to test headlessly. Add one only if the manual pass finds a bug.

## Manual verification steps (real Windows machine, packaged MAUI build)
1. Enroll Windows Hello (PIN at minimum). Launch the app cold: `AppLockPage` must appear before any Blazor content, and no evidence data may render behind it.
2. Cancel the Hello prompt: the app stays locked, with a retry path. Fail the PIN several times: it stays locked.
3. Succeed: the BlazorWebView appears and the session is intact (no re-pairing).
4. Set a short idle timeout on `/settings/app-lock`. Minimize or stop the window past the timeout, then restore: it re-locks. Alt-tab only (deactivate): it does NOT re-lock.
5. Remove all Hello enrollment (or test on a machine with none): confirm `IsAvailableAsync` behavior. Confirm the app does not silently allow entry when the gate is unavailable (check `App.xaml.cs` handling of `IsAvailableAsync() == false`; this is the one place a code fix may be needed).
6. Confirm the preference persists across restarts (MAUI `Preferences`).
7. Record the results in a dated note and update the archive bullet from `[~]` to `[x]`.

## Code and file touchpoints
| File | Change | Why |
|---|---|---|
| (none required for the verdict) | none | Verification is manual; step-up integration is dropped. |
| `src/client/maui/VideoForensics.MauiApp/App.xaml.cs` | Only if step 5 shows fail-open: make it fail-closed or give an explicit fallback | Security of the lock |
| `plans/archive/maui-blazor-hybrid-conversion.md` line 19 | Edit to mark verified and "won't do" for the step-up synergy | Close out the item |

## Test plan
No new automated tests unless step 5 finds a fail-open path. If it does (TDD):
- Extract the "should lock / unlock decision" logic into a testable class in `VideoForensics.Client.Common` (NEW, an interface in `Contracts/`).
- Write `AppLockDecision_GateUnavailable_StaysLocked()` and `AppLockDecision_AuthenticateReturnsFalse_StaysLocked()` first. They fail because the logic is not yet extracted or fail-closed.
- The test project is the sibling tests project of Client.Common, using xUnit v3 and Moq `ILocalAuthGate`.

## Risks and open questions
- Windows Hello may not be drivable by tools, so a human must do the pass.
- If the gate is unavailable and the app allows entry, that is a security gap, not just a polish item. Decide the policy (fail-closed with an explanation, or an explicit user opt-out stored locally).
- Product question to confirm with the owner: is closing the step-up synergy as "won't do" acceptable? The recommendation is yes.

## Priority recommendation
Low. Product value is small (the lock is built, so this is confirmation), security exposure is moderate only if step 5 reveals fail-open, and the effort is about an hour of hands-on testing. Effort: S.

## Implementation dispatch
No code work by default. If step 5 finds an issue, one Haiku or Sonnet subagent: tests first, then the fix in `App.xaml.cs` plus the extracted decision class.
