---
name: ui-shared-theme
description: Ui.Shared theme colors and Syncfusion dropdown binding gotchas. Use when writing or changing CSS, text colors, or SfDropDownList bindings in src/client/ui/VideoForensics.Ui.Shared.
---

# Ui.Shared theme and dropdowns

## Theme colors

- Text color in Ui.Shared CSS uses only Bootstrap 5.3 text variables (e.g. `--bs-secondary-color`).
- Never use a background variable (e.g. `--bs-secondary`) as text.
- Never use an undefined variable such as `--bs-muted-color`.

## SfDropDownList binding

- A `SfDropDownList` clears its value when its list does not contain it. Two-way binding then writes that empty value back.
- A shared selection bound to such a dropdown must ignore empty writes while its option list is empty. See `QueryApiFilterBar`.
