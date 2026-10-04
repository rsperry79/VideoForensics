# Case Filters & Auto-Generated Case Numbers (with Jamming Auto-Detection)

## Context
Currently, filters (case selector, devices, time range, search) are embedded in the left sidebar via ScopeRail, making them visible across all pages. Case numbers are manually entered by users with no standardization. The goals are:

1. Move filters to a right-side panel, shown **only on case pages** (Cases.razor, CaseDetails.razor)
2. **Auto-generate cases with standardized naming** when jamming is detected, suspected, or created manually:
   - `detected-yyyy-mm-dd-hh-mm-index` — auto-generated when jamming is detected
   - `suspected-yyyy-mm-dd-hh-mm-index` — auto-generated when jamming is suspected
   - `manual-yyyy-mm-dd-hh-mm-index` — user-created cases (no manual input, server-generated)
3. **Auto-generate alerts** when jamming-triggered cases are created

## Implementation Plan

### Phase 1: Case Number Autogeneration & Factory

**Logic:** Centralize case number generation and case creation in `CaseRepository`.

**Files to modify:**
- `src/data/database/data.database/Repositories/CaseRepository.cs`
  - Add method `GenerateCaseNumber(string prefix)` that creates a number with pattern `{prefix}-yyyy-mm-dd-hh-mm-index`
    - Prefix options: "detected", "suspected", "manual"
    - Query existing cases created in the same minute
    - Increment INDEX (0, 1, 2, ...) to ensure uniqueness
  - Add method `CreateFromJammingDetectionAsync(JammingEvent, CancellationToken)` to auto-create a case with auto-generated alert
    - Generate case with "detected" or "suspected" prefix based on JammingEvent type
    - Auto-populate Title, Description, and Scope from the JammingEvent
    - Return the created case with its auto-generated CaseNumber
  - Modify `CreateAsync(CreateCaseCommand)` to use `GenerateCaseNumber("manual")` instead of user input

**Files to add/modify for jamming detection hook:**
- `src/data/database/data.database/Repositories/JammingEventRepository.cs` (or similar)
  - When a jamming event is detected/suspected, trigger case creation via `CaseRepository.CreateFromJammingDetectionAsync()`
  - OR: Create an event handler/background service that watches JammingEvent creation and auto-creates cases

### Phase 2: Case Creation UI - Remove Manual Input

**Files to modify:**
- `src/client/ui/VideoForensics.Ui.Shared/Pages/CaseNew.razor`
  - Remove `CaseNumber` input field entirely
  - Add a readonly display showing the auto-generated number preview (e.g., "Your case will be named: manual-2026-09-27-14-35-0")
  - Form submission calls server to create case; server generates the number
  - Update form handling to accept the server-generated CaseNumber in response

### Phase 3: Move Filters to Right-Side Panel on Case Pages

**Architecture:**
- Keep `ScopeRail.razor` as a component but **remove it from MainLayout.razor**
- Add `ScopeRail` to the right side of case pages:
  - `Cases.razor` (main case list page)
  - `CaseDetails.razor` (individual case page)
- Update `MainLayout.razor` to remove the left-sidebar ScopeRail (currently line 129)

**Files to modify:**
- `src/client/ui/VideoForensics.Ui.Shared/Layout/MainLayout.razor`
  - Remove ScopeRail from left sidebar (line 129)
  - Left sidebar will now only show navigation items (lines 119-123)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Cases.razor`
  - Wrap page content and ScopeRail in a layout (grid/flexbox)
  - Position filters on the right
- `src/client/ui/VideoForensics.Ui.Shared/Pages/CaseDetails.razor`
  - Wrap page content and ScopeRail in a layout
  - Position filters on the right

### Phase 4: Auto-Alert Creation (Jamming Cases)

**Logic:** When a case is auto-created from jamming detection/suspicion, also create a corresponding Alert.

**Files to modify:**
- `src/data/database/data.database/Repositories/CaseRepository.cs`
  - In `CreateFromJammingDetectionAsync()`, after creating the case, call AlertRepository to create an alert
  - Alert Title: e.g., "Jamming Detected" or "Jamming Suspected"
  - Alert Description: Link case number, show detection details from JammingEvent
  - Alert RelatedCaseId: Link to the newly-created case

**Optional:** If Alert creation is a separate responsibility, consider creating an `AlertRepository.CreateFromJammingAsync()` method and calling it from the case creation flow.

## Critical Files

**Backend (case generation & jamming integration):**
- `src/data/database/data.database/Repositories/CaseRepository.cs` (CreateAsync, CreateFromJammingDetectionAsync, GenerateCaseNumber)
- `src/data/database/data.database/Repositories/JammingEventRepository.cs` (hook/trigger for case creation)
- `src/data/database/data.database/Repositories/AlertRepository.cs` (create alerts)
- `src/data/common/data.common/Entities/ForensicCase.cs` (reference CaseNumber column)
- `src/data/common/data.common/Entities/JammingEvent.cs` (reference structure)

**Frontend (UI changes):**
- `src/client/ui/VideoForensics.Ui.Shared/Layout/MainLayout.razor` (remove ScopeRail from left sidebar)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/Cases.razor` (add right-side filters)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/CaseDetails.razor` (add right-side filters)
- `src/client/ui/VideoForensics.Ui.Shared/Pages/CaseNew.razor` (remove case number input, show auto-generated preview)

## Verification

1. **Case number autogeneration (all three prefixes):**
   - Create a manual case → Confirm it auto-generates with "manual-yyyy-mm-dd-hh-mm-0" pattern
   - Create multiple manual cases in the same minute → Confirm INDEX increments
   - Trigger jamming detection → Confirm auto-generated case with "detected-..." prefix
   - Trigger jamming suspicion → Confirm auto-generated case with "suspected-..." prefix

2. **Alert auto-generation:**
   - Create jamming-detection case → Confirm corresponding alert is created
   - Alert title/description reflect the jamming event details

3. **Filter visibility and positioning:**
   - Navigate to Cases.razor → Filters visible on the right side
   - Navigate to CaseDetails.razor → Filters visible on the right side
   - Navigate to other pages → Filters NOT visible
   - MainLayout pages show only nav items in left sidebar

4. **Functional filtering:**
   - Case selector, device filters, time range, search all work as before
   - Filtering behavior unchanged, just repositioned

## Testing Notes

- Lite gate: Build affected `.csproj` files and run scoped tests
- Unit tests for `GenerateCaseNumber()` with each prefix
- Unit tests for `CreateFromJammingDetectionAsync()` and alert creation
- UI changes are visual; test filter positioning on case pages
