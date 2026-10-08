# Docked MAUI/Blazor Layout + Theme + Localization + Per-Operator Preferences

**STATUS: MOSTLY COMPLETE (2026-10-08)**

- DONE: docked layout / NavGroups, per-operator `OperatorPreferences` (theme, culture, UiMode), 46 resx files. 40 of the 54 real pages under `Ui.Shared/Pages` use `IStringLocalizer` (the two redirect pages EventsRedirect and WorkflowRedirect have no text).
- REMAINING - 14 pages without localization: AccessControl, AccountDetails, AccountSyncSchedule, AddAccountWizard, AwaitingSetup, CaseDetails, CaseNew, Cases, EvidenceDatesFilter, ForensicReports, JammingAnalysis, Notices, SignalAnomalies, Welcome.
- REMAINING - Simple Mode strings that are hard-coded English: `Layout/Simple/SimpleLayout.razor` "Standard view" button (line 20); `Layout/Simple/SimpleHome.razor` headings and messages ("What happened this week", "Your evidence", "We couldn't load your activity...") and `Formatting/SimpleHomeBuilder.cs` day headings (no localizer in either); user-menu toggle labels "Switch to Simple view" / "Switch to Standard view" in `Layout/NavGroups.cs` (lines 128, 132). Already resx: the operator-details "Display mode" section (`Pages/OperatorDetails.resx`).
- Decision unchanged: English-only for now.

## Context

The VideoForensics client (`src/client/ui/VideoForensics.Ui.Shared`, a Blazor Hybrid Razor Class Library shared by the MAUI app and the server-hosted `VideoForensics.WebApp`) currently uses a single flat top-menu bar (`MainLayout.razor`) with flyout submenus for all navigation, a single hardcoded Radzen "material" theme, and no localization. The user wants a docked-app shell: top tabs per menu group, a hideable vertical left nav for items within the active group, and a hideable right-side "dynamic settings" panel (account switcher up top, then page-specific context below). Alongside the layout redo, the user also asked for a Light/Dark/System theme setting (System default) and full localization of all pages — both stored **per operator** (not per-device, not global), which requires new schema since no per-user preference concept exists today.

This plan was produced after two rounds of codebase exploration (architecture, existing nav/account/theme code, and per-user storage precedent) plus a dedicated planning pass, and after confirming scope decisions with the user directly.

## Confirmed decisions

- **7 top-level tabs**: Dashboard/Workflow, Collect, Analyze, Events, Devices, Tools, **Settings** (Settings promoted from a header flyout to its own tab + vertical nav, same pattern as the other 6).
- **Left nav + right panel** both default to **expanded**; collapse state persists across visits **per device** (localStorage), not per-operator — the user's "per user config" ask was scoped to theme + language only.
- **Theme/language preference** stored **per operator**, keyed by `PairedSessionState.OperatorId`. No such schema exists (`Operator` entity has no settings columns; `IAppLockPreferencesStore` is deliberately per-device; `AppSetting` is global) — requires a new `OperatorPreferences` table + repository + EF migration.
- **No-operator (pre-sign-in) fallback**: System theme + OS/browser culture, in-memory only, no persistence attempt. On sign-in, load (or create-default) that operator's stored preference — a pre-sign-in in-session choice is discarded in favor of the operator's stored value.
- **Localization**: full pass — extract every hardcoded string across all 26 existing pages + the new layout chrome into `.resx` + `IStringLocalizer`, but **English content only** this pass (infra + selector ready for more cultures; no second language translated now).

## A. Layout restructure (`VideoForensics.Ui.Shared`)

Rewrite `Layout/MainLayout.razor` from the current single `RadzenHeader` + two `RadzenMenu` into a docked shell:
- Top tab bar bound to the 7 groups (routes to each group's default page).
- Left `RadzenSidebar`-style vertical tab rail scoped to the active group's items, collapsible.
- Center `RadzenBody` / `@Body`.
- Right collapsible panel: `AccountSwitcher` → `ThemeLanguagePicker` → page-injected content slot.

New supporting files:
- `Layout/NavGroups.cs` — extracts today's inlined `RadzenMenuItem` tree into a plain data model (`record NavGroup(Key, Text, Path, Items)`, `record NavItem(Text, Path, Func<bool>? Visible)`) covering all 7 groups (including Settings' 8 items), reusing the existing `HasRole`/`OperatorRole` gating logic as `Visible` predicates. Both the top tab bar and left vertical nav render from this one data source.
- `Layout/MainLayout.razor.cs` — code-behind: active-group resolution from current route, role gating (unchanged from today), wiring to the new services below.
- `Layout/MainLayout.razor.css` — grid layout for header row + 3-column body row (left rail | body | right panel), collapse/expand transitions.
- `Layout/RightPanel/RightPanelHost.razor`, `AccountSwitcher.razor`, `ThemeLanguagePicker.razor`, `PageRightPanelContent.razor` (see B, C, D).
- `Services/RightPanelContentService.cs` — scoped service (per-circuit, like `PairedSessionState`) holding a settable `RenderFragment?` + `OnChange` event, so any page can inject contextual content into the right panel via the `PageRightPanelContent` wrapper component without touching `@code`; pages clear on `Dispose` so content doesn't leak across navigation.
- `Services/LayoutPreferencesState.cs` — scoped, `IJSRuntime`-backed (mirrors `PairedSessionState`'s `EnsureLoadedAsync`/try-catch-`JSException` pattern), persists `leftNavCollapsed`/`rightPanelCollapsed` to `localStorage` via new `wwwroot/js/layout-prefs.js`. Both defaults `false` (expanded).
- Add the new JS file's `<script>` tag to **both** hosts (`src/client/maui/VideoForensics.MauiApp/wwwroot/index.html` and `src/client/web/VideoForensics.WebApp/Components/App.razor`) — don't repeat the existing gap where `webauthn.js` exists only in the WebApp host.

## B. Account dropdown (right panel, top)

New `Layout/RightPanel/AccountSwitcher.razor` — a compact `RadzenDropDown` (not the existing `Pages/Accounts.razor` grid) reusing the same services that page already uses: `IProviderAccountRepository`, `IForensicsConfiguration`/`IForensicsConfigurationService` (`ActiveProviderAccountId`), `IProviderAuthService.RestoreFromSavedCredentialsAsync`. Switching the dropdown mirrors `Accounts.razor`'s existing `SelectAccountAsync` logic. `Pages/Accounts.razor` stays as the full add/remove management page, reachable via the new Settings tab.

## C. Theme (Light/Dark/System), per operator

**Schema** (new, since none exists — see `IForensicsConfiguration.cs`, `IAppLockPreferencesStore.cs`, `AppSetting.cs` for precedent patterns copied below):
- `src/data/common/data.common/Entities/OperatorPreferences.cs` — `Id`, `OperatorId` (FK to `Operator.Id`, unique index, mirrors `PairedDevice.OperatorId`), `ThemeMode` (string: "Light"/"Dark"/"System", default "System"), `CultureName` (string?, null = unset), `UpdatedAtUtc`.
- `src/data/common/data.common/Contracts/IOperatorPreferencesRepository.cs` — `GetAsync(operatorId, ct)`, `UpsertAsync(preferences, ct)` (narrower than `IAppSettingRepository` — one row per operator, no list/delete needed).
- `src/data/database/data.database/Repositories/OperatorPreferencesRepository.cs` — copy `AppSettingRepository`'s shape (`IDbContextFactory<VideoForensicsDbContext>`, per-call `await using` context).
- `src/data/database/data.database/Configurations/OperatorPreferencesConfiguration.cs` — copy `PairedDeviceConfiguration`'s shape.
- Add `DbSet<OperatorPreferences>` to `VideoForensicsDbContext`; register `IOperatorPreferencesRepository` in `src/data/database/data.database/DependencyInjection/ServiceCollectionExtensions.cs`.
- New EF migration: `dotnet ef migrations add AddOperatorPreferences --project src/data/database/sqlite/data.database.sqlite --startup-project <a host project>`.

**Runtime**:
- `Services/ThemePreferenceService.cs` (scoped) — `InitializeAsync()` (called from `MainLayout.razor.cs`'s `OnAfterRenderAsync(firstRender)`, same slot `SessionState.EnsureLoadedAsync()` uses) loads the signed-in operator's `OperatorPreferences` (or uses in-memory System default pre-sign-in), computes effective Light/Dark theme (resolving "System" via `matchMedia`), exposes `SetModeAsync(mode)` for the picker (persists via the repository when signed in, always updates in-memory state immediately).
- System-preference detection: new `wwwroot/js/theme-detect.js` — `vfTheme.getPrefersDark()` + `vfTheme.watchPrefersDark(dotNetRef)` registering a `matchMedia('(prefers-color-scheme: dark)')` change listener that calls back into a `[JSInvokable] OnSystemThemeChanged(bool)` on the service, so a live OS theme flip re-renders without a page refresh. Add this script tag to both hosts' shells, same as A's `layout-prefs.js`.
- `MainLayout.razor`'s `<RadzenTheme Theme="material" />` becomes `<RadzenTheme Theme="@ThemeService.EffectiveRadzenThemeName" />` (`"material"` or `"material-dark"` — both ship with the already-referenced Radzen.Blazor 11.2.2 package, confirmed present in the NuGet cache). Subscribes to `ThemePreferenceService.OnChange` like `RightPanelContentService`.
- `Layout/RightPanel/ThemeLanguagePicker.razor` — Light/Dark/System selector (e.g. `RadzenSelectBar`) bound to `ThemePreferenceService`, placed second in `RightPanelHost.razor` (below `AccountSwitcher`, above the page-injected slot).

## D. Localization

- Add `Microsoft.Extensions.Localization` package reference to `VideoForensics.Ui.Shared.csproj`, version-matched to the repo's pinned ASP.NET Core components version (currently `10.0.11`).
- Convention: one `.resx` co-located per page/component (e.g. `Pages/Dashboard.razor` → `Pages/Dashboard.resx`) for automatic `IStringLocalizer<T>` resolution, plus `Resources/SharedResources.resx` + marker class `Resources/SharedResources.cs` for shared chrome strings (tab/menu labels, common buttons).
- `builder.Services.AddLocalization();` in both hosts' startup (`MauiProgram.cs`, `WebApp/Program.cs`).
- **Culture-switching gotcha to resolve during implementation**: MAUI has no request pipeline, so culture must be set explicitly (`CultureInfo.CurrentCulture`/`CurrentUICulture`) on app start and on change — safe to mutate ambient culture in MAUI's single-process app. The WebApp host is Blazor Server (multi-circuit, shared process) — must NOT mutate ambient `CultureInfo` process-wide; set it per-circuit in `MainLayout`'s init lifecycle instead (spike this early: confirm a per-circuit `CultureInfo.CurrentUICulture` set in a component lifecycle method actually sticks for that circuit's subsequent renders under Blazor Server's threading model). Implement via a small `Services/CultureSwitcher.cs` abstraction with host-specific behavior.
- Migration pattern per existing page (repeat for all 26 files in `Pages/*.razor` plus `MainLayout`/`NavGroups`): inject `IStringLocalizer<PageName>`, seed a `.resx` with every literal user-facing string (labels, button text, grid column titles, status messages), replace literals with `@L["Key"]` / `L["Key"]`. Leave routes, CSS classes, C# identifiers, and log messages untouched. Do the pattern first on `Dashboard.razor` (simple), `Accounts.razor` (dynamic status messages + nested component), and one grid-heavy page (e.g. `Events.razor`) to validate the approach before sweeping the rest.
- Language selector (`RadzenDropDown`, initially one entry: English) lives alongside the theme picker in `ThemeLanguagePicker.razor`/right panel, persists `CultureName` on `OperatorPreferences` the same way theme does.

## Files touched (representative, not exhaustive for the 26-page string sweep)

**New:** `Entities/OperatorPreferences.cs`, `Contracts/IOperatorPreferencesRepository.cs`, `Repositories/OperatorPreferencesRepository.cs`, `Configurations/OperatorPreferencesConfiguration.cs`, EF migration files, `Layout/NavGroups.cs`, `Layout/MainLayout.razor.cs`, `Layout/RightPanel/{RightPanelHost,AccountSwitcher,ThemeLanguagePicker,PageRightPanelContent}.razor`, `Services/{RightPanelContentService,LayoutPreferencesState,ThemePreferenceService,CultureSwitcher}.cs`, `wwwroot/js/{layout-prefs,theme-detect}.js`, `Resources/SharedResources.{resx,cs}`, per-page `.resx` files.

**Changed:** `Layout/MainLayout.razor` (+`.css`), `VideoForensics.Ui.Shared.csproj`, `VideoForensicsDbContext.cs`, `ServiceCollectionExtensions.cs` (data DI), `MauiProgram.cs`, `WebApp/Program.cs`, `index.html`, `App.razor`, all `Pages/*.razor` (string extraction).

## Verification

1. `dotnet build` after each phase (data layer first — entity/config/migration; then UI layer).
2. Apply migration against a real/test SQLite DB; confirm `OperatorPreferences` table via `sqlite3 ... ".schema OperatorPreferences"`.
3. Run `VideoForensics.WebApp` (`dotnet run`): verify 7 top tabs route correctly, left nav shows only active group's items and collapses/persists across reload, right panel collapses/persists, account dropdown switches `ActiveProviderAccountId` (cross-check against `/accounts`), theme picker changes visibly + System mode live-follows a Windows dark-mode toggle without refresh, language dropdown renders without error, pre-sign-in state is System/no-persist and doesn't error.
4. Run the MAUI app (BlazorWebView/WebView2): repeat the same checks, specifically confirming the two new JS files actually load (no silently-swallowed `JSException`) and `matchMedia` works inside WebView2.
5. Sign in as two different operators (or one + signed-out) and confirm theme/language preferences are isolated per `OperatorId`, persist across app restart, and don't bleed across operators.
6. Spot-check localization on the 3 representative pages plus `MainLayout` chrome — grep for stray un-keyed literal strings.
