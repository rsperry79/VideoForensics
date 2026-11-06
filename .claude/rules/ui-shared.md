---
paths:
  - "src/client/ui/VideoForensics.Ui.Shared/**"
---

# Ui.Shared

## Localization (required)

- Every user-visible string (labels, buttons, headings, placeholders, tooltips, errors, aria labels, validation/alert text) comes from resources. No English literals in markup or `@code`.
- Inject `@inject IStringLocalizer<SharedResources> L`; use `@L["Key"]`. Page-specific text goes in the page's `.resx` (e.g. `DeviceSignIn.resx`). Add keys in the same change.
- Tests asserting text use the localizer or resource key.
- Exempt: log messages, internal exception text, route paths, CSS classes, test data.
- Unlocalized pages get localized when next touched.

## Layout

- Desktop-first. Resizable panels via Syncfusion `SfSplitter`.
- `Routes.razor` uses `ResponsiveLayout`, which swaps to `MobileLayout` below 600px.
- Mobile logic goes in `MobileLayout` or `ResponsiveLayout`, never `MainLayout`.

## Syncfusion gotchas

- `SfTextBox` binds on change: tests use `Change()`, not `Input()`.
- Qualify `Microsoft.AspNetCore.Components.ChangeEventArgs` in Razor files. `Syncfusion.Blazor.QueryBuilder` clashes (CS0104).
- A Syncfusion dropdown renders its own `role="alert"`. Narrow test selectors with a `data-testid`.

## Reusable controls

Before writing a new control, read `.claude/reference/ui-shared-controls.md` and reuse what exists.
