# Migrate UI framework: Radzen.Blazor → Syncfusion Blazor

**STATUS: NEARLY COMPLETE (2026-10-08)**

- 62 of 90 razor files under `VideoForensics.Ui.Shared` use Sf* components; `AddSyncfusionBlazor()` is registered in WebApp `Program.cs` (line 112) and `MauiProgram.cs` (line 51); `AddRadzenComponents` is gone.
- REMAINING: `src/client/ui/VideoForensics.Ui.Shared/Dialogs/FolderBrowserDialog.razor` still uses RadzenText/RadzenStack/RadzenButton (lines 10-26, plus `@using Radzen` at lines 4-5); `<PackageReference Include="Radzen.Blazor" Version="12.0.7" />` is still in `VideoForensics.Ui.Shared.csproj` (line 25); `@using Radzen` / `@using Radzen.Blazor` remain in `_Imports.razor` (lines 9-10).
- Stale comments mentioning `<RadzenComponents>`: `src/client/web/VideoForensics.WebApp/Program.cs` line 369 and `src/client/maui/VideoForensics.MauiApp/MauiProgram.cs` line 176; also `Services/IBlazorRenderModeProvider.cs` line 8, `Services/ThemePreferenceService.cs` line 16 ("Radzen theme") and `Layout/NavGroups.cs` line 34 in Ui.Shared.

## Context

The app's entire Blazor UI (shared by the MAUI desktop client and the Web/Blazor-Server host) is built on Radzen.Blazor 11.2.2. The user wants to move to Syncfusion Blazor instead, as a full framework swap, done in one big-bang pass rather than an incremental page-by-page migration. No Syncfusion license is in place yet — **that is a hard blocking prerequisite**, since Syncfusion Blazor components refuse to render (they show a licensing/trial banner or throw) without a registered license key at runtime.

Scope confirmed by inventory: Radzen is referenced in **34 `.razor` files** under `src\client\ui\VideoForensics.Ui.Shared`, one `Radzen.Blazor` NuGet reference (`src\client\ui\VideoForensics.Ui.Shared\VideoForensics.Ui.Shared.csproj:26`), and two DI registration points (`AddRadzenComponents()` in `src\client\web\VideoForensics.WebApp\Program.cs:54` and `src\client\maui\VideoForensics.MauiApp\MauiProgram.cs:30`). No other project carries its own `Radzen.Blazor` package reference — both hosts pick it up transitively through their project reference to `VideoForensics.Ui.Shared`, so the package swap happens in exactly one `.csproj`.

Component usage inventory (occurrence counts across all 34 files):

| Radzen component | Count | Syncfusion equivalent |
|---|---|---|
| `RadzenText` | 250 | plain HTML (`<span>`/`<p>`/`<h*>`) — Syncfusion has no direct "styled text" primitive; typography is CSS-driven |
| `RadzenDataGridColumn` | 123 | `GridColumn` (child of `SfGrid`) |
| `RadzenDataFilterProperty` | 98 | `SfGrid`'s built-in `GridFilterSettings`/toolbar filtering, or `SfQueryBuilder` if a standalone AND/OR condition-builder UI (like today's separate filter panel) must be preserved |
| `RadzenButton` | 72 | `SfButton` |
| `RadzenAlert` | 60 | no 1:1 widget — typically hand-rolled with `SfMessage`/plain markup + Syncfusion's alert CSS classes |
| `RadzenStack` | 57 | plain flex/grid CSS (Syncfusion has no layout-stack primitive) |
| `RadzenLink` | 36 | `<a>`/`NavLink`, or `SfButton` styled as a link |
| `RadzenCard` | 25 | `SfCard` |
| `RadzenProgressBarCircular` | 24 | `SfSpinner` |
| `RadzenDataGrid` | 24 | `SfGrid` |
| `RadzenDataFilter` | 21 | `SfQueryBuilder` (closest match to a standalone AND/OR builder) |
| `RadzenTextBox` | 20 | `SfTextBox` |
| `RadzenDropDown` | 18 | `SfDropDownList` |
| `RadzenCheckBox` | 12 | `SfCheckBox` |
| `RadzenDatePicker` | 10 | `SfDatePicker` |
| `RadzenNumeric` | 5 | `SfNumericTextBox` |
| `RadzenSelectBar`/`RadzenSelectBarItem` | 1/3 | `SfRadioButton` group or `SfButtonGroup` |
| `RadzenIcon` | 2 | Syncfusion icon font classes (`e-icons e-*`) |
| `RadzenTheme`, `RadzenComponents`, `RadzenLayout`, `RadzenHeader`, `RadzenBody` | 1 each | removed — Syncfusion has no theme/layout-host components; theming is CSS-only, layout stays custom HTML/CSS (already true today for the docked layout in `MainLayout.razor`) |
| `RadzenTextArea`, `RadzenBadge` | 1 each | `SfTextBox` (multiline), `SfBadge` |
| `DialogService` (Radzen) | 4 files (`FolderBrowserDialog.razor`, `EventDetailsDialog.razor`, + 2 more) | `SfDialogService`/`SfDialog` |

## Prerequisite (blocking)

Before any code changes: obtain a Syncfusion Blazor license key (Community License if the org/team qualifies under Syncfusion's revenue/team-size thresholds, otherwise a paid Essential Studio subscription) and get it into this app's existing secrets pattern — **no secrets in code** (per this repo's own CLAUDE.md rule), so the key goes through config/env vars/credential store, mirroring how other secrets are already handled in `VideoForensicsHostingExtensions`. This must be resolved before step 1 below, since Syncfusion components render a licensing watermark/error with no valid key registered.

## Implementation steps

1. **Package swap** — in `VideoForensics.Ui.Shared.csproj`, remove `Radzen.Blazor` and add the Syncfusion Blazor NuGet packages actually needed by the component list above (at minimum: `Syncfusion.Blazor.Grids`, `Syncfusion.Blazor.Buttons`, `Syncfusion.Blazor.DropDowns`, `Syncfusion.Blazor.Inputs`, `Syncfusion.Blazor.Calendars`, `Syncfusion.Blazor.Popups`, `Syncfusion.Blazor.Notifications`, `Syncfusion.Blazor.Cards`, `Syncfusion.Blazor.ProgressBar`, `Syncfusion.Blazor.QueryBuilder`, `Syncfusion.Blazor.Themes`). Pin exact versions matching this repo's convention of pinning every dependency (see `Microsoft.CodeAnalysis` pinning already enforced repo-wide).

2. **License registration + DI swap** — in both `src\client\web\VideoForensics.WebApp\Program.cs:54` and `src\client\maui\VideoForensics.MauiApp\MauiProgram.cs:30`, replace `builder.Services.AddRadzenComponents();` with `builder.Services.AddSyncfusionBlazor();`, and register the license key at startup via `Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(...)` reading from configuration/env var (added once, near the top of each `Program.cs`/`MauiProgram.cs`, before `builder.Build()`).

3. **Theme CSS** — remove `<RadzenTheme Theme="@ThemeService.EffectiveRadzenThemeName" />` and `<RadzenComponents ... />` from `MainLayout.razor`; add Syncfusion's theme stylesheet reference (static asset or CDN, matching this repo's existing `_content/VideoForensics.Ui.Shared/...` static-asset convention) to both hosts' root HTML pages (`App.razor`, `index.html`) — likely two stylesheet links (light/dark, or one Bootstrap5/Fluent theme with dark-mode CSS variables), swapped by `ThemePreferenceService` the same way it currently swaps Radzen's theme name, so the existing Light/Dark/System picker (`ThemeLanguagePicker.razor`, now living on the Settings page) keeps working.

4. **Component-by-component replacement** — this is mechanical but touches all 34 files; the pattern is identical everywhere so it's described once rather than file-by-file:
   - `<RadzenDataGrid TItem="X" Data="@Y">...<Columns><RadzenDataGridColumn .../></Columns></RadzenDataGrid>` → `<SfGrid TValue="X" DataSource="@Y"><GridColumns><GridColumn .../></GridColumns></SfGrid>`
   - `<RadzenDataFilter @ref="_f" TItem="X" Data="@Y" Auto="true"><Properties><RadzenDataFilterProperty .../></Properties></RadzenDataFilter>` → `<SfQueryBuilder TValue="X" DataSource="@Y">...</SfQueryBuilder>` wired to produce a filtered `View`-equivalent the grid binds to (Syncfusion's query builder emits a predicate/rule set rather than exposing a `.View` property directly — this is the one genuinely non-mechanical piece and needs a small adapter method per page, or a single shared helper in `Ui.Shared/Services` that converts a `SfQueryBuilder` rule set into a LINQ `Where` clause, reused across all ~15 pages using this pattern established for the sidebar-filter rollout done earlier this session).
   - `RadzenButton`/`RadzenTextBox`/`RadzenDropDown`/`RadzenCheckBox`/`RadzenDatePicker`/`RadzenNumeric` → their `Sf*` equivalents, mostly a tag-name + parameter-name swap (`Text=`→`Content=` or child text, `@bind-Value` stays the same binding pattern, `Data`/`TextProperty`/`ValueProperty` on dropdowns map directly).
   - `RadzenCard`/`RadzenProgressBarCircular`/`RadzenBadge` → `SfCard`/`SfSpinner`/`SfBadge`.
   - `RadzenText`/`RadzenStack`/`RadzenAlert`/`RadzenLink` → plain HTML with CSS classes (no direct Syncfusion widget), reusing whatever utility CSS classes this app already has (`mb-4`, `mt-2`, etc. already appear throughout, per the files touched this session).
   - `DialogService.OpenAsync<T>(...)` (Radzen) → `SfDialogService` injection + `SfDialog`/confirm equivalents in the 4 files using it today (`FolderBrowserDialog.razor`, `EventDetailsDialog.razor`, and the 2 others found by inventory).

5. **`MainLayout.razor` layout shell** — `RadzenLayout`/`RadzenHeader`/`RadzenBody` are just layout containers with no Syncfusion equivalent (and this app's docked layout is already hand-built CSS underneath them, per the top-bar/hamburger work done earlier this session) — replace with plain `<div>` wrappers carrying the same CSS classes (`app-bar`, `docked-body`, etc.) that already exist in `MainLayout.razor.css`, so no visual layout logic changes, only the outer component tags.

## Verification

- `dotnet build` on `VideoForensics.Ui.Shared.csproj`, then both hosts (`VideoForensics.WebApp.csproj`, `VideoForensics.MauiApp.csproj`) — must be 0 errors/0 warnings (per this repo's CLAUDE.md gate) before moving on, since a big-bang swap means the whole UI project won't compile until every file is converted.
- Launch the Web host via the existing `.claude/launch.json` `webapp` preview config, walk every nav tab (Dashboard, Collect, Analyze incl. all 5 report pages, Events, Devices, Tools incl. Import/Export, Settings) confirming grids render, filters work, dialogs open (FolderBrowserDialog, EventDetailsDialog), and the Light/Dark/System theme toggle still switches the Syncfusion theme stylesheet correctly.
- Confirm the MAUI app's Import/Export page still opens (this session's earlier fix for that page's DI resolution is Syncfusion-agnostic, but worth a smoke test given how much markup changes).
- Run the full test suite (`dotnet test`) per this repo's mandatory pre-commit gate — existing xUnit/Moq tests shouldn't reference Radzen types directly (they test services, not markup), so this is mainly a regression check that nothing else broke.
