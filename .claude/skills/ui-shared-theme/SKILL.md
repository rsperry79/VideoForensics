---
name: ui-shared-theme
description: Ui.Shared theme colors and Syncfusion dropdown binding gotchas. Use when writing or changing CSS, text colors, or SfDropDownList bindings in src/client/ui/VideoForensics.Ui.Shared.
---

# Ui.Shared theme and dropdowns

## Theme colors

- Text color in Ui.Shared CSS uses only Bootstrap 5.3 text variables (e.g. `--bs-secondary-color`).
- Never use a background variable (e.g. `--bs-secondary`) as text.
- Never use an undefined variable such as `--bs-muted-color`.
- Bootstrap switches its text tokens only under `data-bs-theme="dark"`. The app sets it on `<html>` in `wwwroot/scripts/theme-detect.js` (`setStylesheet`) next to `data-theme`. Never drop that attribute. If text turns faint on a dark page, check that the attribute is set before touching component CSS.
- Never hard-code a light panel color (e.g. `#f8f9fa`) for a component. Use a Bootstrap background token so it follows the theme.
- Syncfusion `e-outline` buttons and Bootstrap `btn-outline-secondary` take a mid-gray text color (`#868e96`, about 5:1 on the dark body). `wwwroot/app.css` lifts them to `#dee2e6` under `html[data-theme="dark"]`. Add new button variants there, not in component CSS.

## SfDropDownList binding

- A `SfDropDownList` clears its value when its list does not contain it. Two-way binding then writes that empty value back.
- A shared selection bound to such a dropdown must ignore empty writes while its option list is empty. See `QueryApiFilterBar`.
