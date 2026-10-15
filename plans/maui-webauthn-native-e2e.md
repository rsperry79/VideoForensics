# MAUI native WebAuthn end-to-end test

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN. The native code exists, has zero callers and zero tests, and has never run against a real Windows Hello prompt.

**Source**: `plans/archive/maui-blazor-hybrid-conversion.md` line 21 (unchecked item "MAUI native WebAuthn ... needs a hands-on pass on a real Windows machine").

## Verdict

STILL OPEN. Checked with `git grep` on origin/dev for `WebAuthnNative`, `WebAuthnCeremonyClient`, `MakeCredential(`, `GetAssertion(`, `WebAuthnCeremonyException` and `InternalsVisibleTo`. The only hits are the two files themselves under `src/client/maui/VideoForensics.MauiApp/Platforms/Windows/WebAuthn/`. Nothing in MauiProgram, Ui.Shared or any test project calls them, and there is no MAUI test project. Only the pure helper logic (client-data JSON, base64url, JSON shaping) can be automated. The real Windows Hello ceremony needs a human on a device.

## Scope

Changes since the archived plan:
- `WebAuthnCeremonyClient` is a static class with private helpers, so nothing is testable today. `WebAuthnNative` is `internal static` P/Invoke.
- The MAUI app uses the in-WebView browser path instead. `MauiProgram.cs` registers `Ui.Shared.Services.WebAuthnClient` (JS interop via `webauthn.js`). The native client is not wired into any flow and is effectively dead code.
- MAUI targets only `net10.0-windows10.0.19041.0` (or iOS via `BuildIOS`), so a test project cannot reference it as a normal library.
- Confirmed cheap drift: `MakeCredential` returns both `clientDataJson` and `clientDataJSON` and mixes base64url (`id`) with standard base64 (`rawId`, `attestationObject`). That is an unverified guess at what Fido2NetLib accepts, so the server-contract test below is the highest-value automated check.

Split:
- **Automatable (no device):**
  - Extract the pure logic: `BuildClientDataJson`, `Base64UrlEncode/Decode`, and the response-JSON shaping from the attestation/assertion byte arrays.
  - Put them behind an interface (`IWebAuthnCeremonyClient` plus an internal `IWebAuthnNativeApi` seam) with a fake native layer.
  - Add a struct layout test using `Marshal.SizeOf` and `OffsetOf` against the known webauthn.h v1 sizes.
  - Add a Fido2NetLib round-trip test that feeds the shaped JSON to the server verifier with a software authenticator.
- **Human on a Windows Hello device:** a manual checklist (below).

## Code and file touchpoints

| File | Change | Why |
|---|---|---|
| `src/client/maui/VideoForensics.MauiApp/Platforms/Windows/WebAuthn/WebAuthnCeremonyClient.cs` | Split pure helpers and response shaping into a testable class; make the public surface an interface-backed instance; keep P/Invoke calls behind a seam | Needs testability; static plus private blocks it |
| `src/client/maui/VideoForensics.MauiApp/Platforms/Windows/WebAuthn/WebAuthnNative.cs` | Add `IWebAuthnNativeApi` adapter; expose layout constants for tests | Allows a fake native layer |
| `src/client/common/VideoForensics.Client.Common/Contracts/IWebAuthnCeremonyClient.cs` (NEW; Client.Common/Contracts verified to exist) | Platform-agnostic interface, public API in Contracts per convention | Lets the shared code mock it |
| `src/client/maui/VideoForensics.MauiApp.Tests/VideoForensics.MauiApp.Tests.csproj` (NEW) | xUnit v3 test project, Windows TFM, `Microsoft.CodeAnalysis` reference per repo rule | No MAUI test project exists |
| `src/client/maui/VideoForensics.MauiApp.Tests/WebAuthnCeremonyClientTests.cs` (NEW) | Tests listed below | TDD |
| `src/client/maui/VideoForensics.MauiApp.Tests/WebAuthnNativeLayoutTests.cs` (NEW) | Struct size and offset assertions | Catches the layout mistake the file's own risk note names |
| `src/client/host/VideoForensics.Hosting.Tests/` (existing project) | Add `WebAuthnNativeResponseContractTests` using Fido2NetLib | Proves the shaped JSON is accepted by the server |
| `plans/maui-webauthn-native-e2e.md` | Manual checklist (below) | Human-only part |

Because the MAUI project is Windows-only and has an unusual TFM setup, the first step is deciding whether tests reference the app project or a small extracted `net10.0-windows` library (see open questions).

## Test plan

Project: `VideoForensics.MauiApp.Tests` (NEW), plus one class in `VideoForensics.Hosting.Tests`. Write tests first; they fail by compile error until the seam and interface exist, then by behavior.

- `WebAuthnCeremonyClientTests.MakeCredential_ValidOptions_ReturnsAttestationJsonWithIdAndResponse`
- `WebAuthnCeremonyClientTests.MakeCredential_NativeReturnsFailureHr_ThrowsCeremonyExceptionWithHresult`
- `WebAuthnCeremonyClientTests.MakeCredential_NullAttestationPointer_Throws`
- `WebAuthnCeremonyClientTests.MakeCredential_Always_FreesAttestationAndPinnedHandles`
- `WebAuthnCeremonyClientTests.GetAssertion_ValidOptions_ReturnsAssertionJson`
- `WebAuthnCeremonyClientTests.GetAssertion_UserCancels_ThrowsCeremonyException`
- `WebAuthnCeremonyClientTests.Base64Url_RoundTrip_PreservesBytes` (lengths 0 to 5, `-` and `_` characters)
- `WebAuthnCeremonyClientTests.BuildClientDataJson_Create_ContainsTypeChallengeOrigin`
- `WebAuthnNativeLayoutTests.Structs_SizeAndOffsets_MatchWebauthnHV1` (x64)
- `WebAuthnNativeResponseContractTests.ShapedAttestation_SoftwareAuthenticator_AcceptedByFido2Verifier` (and the assertion twin)

Expected initial failure: the tests do not compile (no interface, no seam). After the seam, `Structs_SizeAndOffsets...` is the one most likely to fail for a real reason. Run scoped with `dotnet test --filter "Class=WebAuthnCeremonyClientTests"`.

### Manual checklist (human, Windows Hello device)

1. Build a MAUI Windows build; run VideoForensics.WebApp locally.
2. Call `WebAuthNIsUserVerifyingPlatformAuthenticatorAvailable`; expect true.
3. Register: complete a pairing, confirm the Windows Hello prompt appears, and confirm the server accepts `register/complete`.
4. Authenticate: sign in with the new credential; confirm `assertion-complete` succeeds and the sign count increments.
5. Cancel the prompt and confirm a clean `WebAuthnCeremonyException`, no crash or handle leak.
6. Repeat on PIN-only and fingerprint/face Hello configurations, and on an ARM64 device if one is in scope.
7. Record the result (date, OS build, device) in this plan.

## Risks and open questions

- Is the native path still wanted? The WebView plus `webauthn.js` path already works in MAUI. If native is not going to be wired in, the better option is to delete the dead code and close the item. Decide this first.
- Test project design: referencing a Windows-only MAUI exe project from a test project is fragile; extracting the logic into a small class library is cleaner but is a bigger refactor.
- Struct layout in this wrapper is x64-only verified; ARM64 and x86 are unknown.
- The response JSON mixes encodings (see Scope); the Fido2 contract test may force changes.
- Security: any mismatch would at worst fail a ceremony; there is no server-side trust change. Exposure is low while the code is unwired.

## Priority recommendation

**Low.** The code is unused, the working WebView passkey path already covers the product need, and the security exposure is nil until it is wired; the only open work is either a modest refactor plus tests or deleting the code. Effort: **M** (S if the decision is to delete).

## Implementation dispatch

Dispatch in two steps: (1) a Sonnet subagent writes the failing tests and the NEW test project plus the interface, then confirms they fail; (2) a second Sonnet subagent refactors `WebAuthnCeremonyClient`/`WebAuthnNative` behind the seam until green. The manual checklist is run by the user.
