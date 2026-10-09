# Mobile layout optimization

**Status on origin/dev (checked 2026-10-09):** ALREADY DONE (implementation shipped; only the CLAUDE.md "pending" note is stale)

**Source:** `plans/archive/per-user-login-password-passkey.md` line 365 ("Mobile layout optimization (pending per CLAUDE.md)"); `plans/archive/per-user-login-integration-test-results.md` line 214 ("pending after desktop optimization"). `plans/archive/maui-layout-theme-localization.md` was also checked and covers MAUI layout/theme/localization.

## Verdict
The mobile layout the deferred item asks for exists on origin/dev. `Routes.razor` uses `Layout.ResponsiveLayout` as the default layout, and `ResponsiveLayout.razor` swaps between `SimpleLayout`, `MobileLayout` and `MainLayout` based on `IViewportService.IsMobile` (width under 600px, JS interop via `wwwroot/js/viewport.js`). `MainLayout` was left untouched, as CLAUDE.md requires. The archive lines are stale, and so is the "Mobile optimization is pending" paragraph in CLAUDE.md (line 144). The only remaining work is small cleanup, not a feature build.

## Scope
In scope (cleanup only):
- Correct the stale CLAUDE.md paragraph so it describes the shipped `ResponsiveLayout` swap.
- Localize the hard-coded English `NotFound` text in `Routes.razor` ("Sorry, there's nothing at this address."), which is a visible string.
- Optional: confirm `MobileLayout` / `MobileFiltersSheet` / `MobileInspectorDrawer` strings all come from resources. They use `L[...]` keys, so this should be a quick audit.

Out of scope: new mobile screens, MAUI iOS/Android head project work, MainLayout changes.

## Design decisions
- **Platform detection (already chosen):** viewport width via JS (`vfViewport.getIsMobile`, 600px breakpoint, change events through `OnViewportChanged`), registered as scoped `IViewportService` in both `WebApp/Program.cs` and `MauiProgram.cs`. This is correct for a Blazor Hybrid app: the same Razor tree runs in the web browser and in MAUI, and a narrow window on desktop also gets the mobile layout. MAUI `DeviceInfo.Idiom` would miss narrow desktop and web browsers, so it is not needed. Keep viewport detection.
- **Swap point:** `ResponsiveLayout` as the router default layout. No change.
- **Which screens first:** already global, because the layout wraps every routed page. Per-page mobile polish is a separate, per-page concern, not part of this item.
- Static prerender defaults to desktop until the circuit connects, and the layout then flips. This is documented in the code. A brief desktop flash on phones is a known tradeoff.

## Code and file touchpoints
All paths verified on origin/dev.

| file | change | why |
|---|---|---|
| `CLAUDE.md` | Replace the "Mobile optimization is pending" paragraph with a description of `ResponsiveLayout` / `MobileLayout` / `IViewportService` | Stale guidance; the archive items cite it as the reason this was "pending" |
| `src/client/ui/VideoForensics.Ui.Shared/Routes.razor` | Inject `IStringLocalizer<SharedResources>` and use `L["NotFoundMessage"]` in `NotFound` | CLAUDE.md localization rule |
| `src/client/ui/VideoForensics.Ui.Shared/Resources/SharedResources*.resx` (exact filenames to be confirmed by the implementer) | Add `NotFoundMessage` key (all shipped cultures) | Localization |
| `src/client/ui/VideoForensics.Ui.Shared.Tests/MobileLayoutTests.cs` | Add a `NotFound` localization test if a Routes test fits there; otherwise a new test file | Coverage for the changed behavior |
| `src/client/ui/VideoForensics.Ui.Shared/Layout/ResponsiveLayout.razor` | No change | Already correct |

## Test plan
Test project: `src/client/ui/VideoForensics.Ui.Shared.Tests` (xUnit v3 + bUnit; `MobileLayoutTests.cs` and `DefaultViewportServiceTests.cs` already exist).
1. Write first: a bUnit test rendering `Routes` at an unknown path and asserting the not-found text equals the localizer value for `NotFoundMessage`. Expected failure: the key does not exist and the literal is hard-coded, so it fails on a missing resource or on text mismatch.
2. Implement the resource key and the `Routes.razor` change, then confirm it passes.
3. Optionally add a `ResponsiveLayout` swap test (mobile viewport renders `MobileLayout`, desktop renders `MainLayout`) if not already covered. Check the existing tests first.
4. Scoped run: `dotnet test --filter` on the new and touched test classes only.

## Risks and open questions
- Very low risk. The CLAUDE.md edit is a documentation change; the `Routes.razor` change is one string.
- Open: whether the 600px breakpoint is right for tablets (iPad portrait is 768px+ and gets the desktop layout). That is a product call and not a blocker.
- CLAUDE.md is a user-owned instructions file, so edit it only with the user's go-ahead.

## Priority recommendation
Low: the feature is already shipped, so product value and security exposure of the leftover cleanup are negligible, and it only removes stale guidance and one unlocalized string. Effort: S.

## Implementation dispatch
One Sonnet subagent for the whole item: write the failing `NotFound` localization test first, add the resx key and `Routes.razor` change, then edit the CLAUDE.md paragraph. Lite gate on `Ui.Shared` and `Ui.Shared.Tests` only.
