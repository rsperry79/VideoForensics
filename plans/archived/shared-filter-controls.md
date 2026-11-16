# Shared filter controls

Status: done (merged to wip, 17910496)

## Context
Filters on several pages (Accounts, Manual Sync, Notices, Live View, Review) were copies: each page kept its own provider, account and camera state and its own storage key. A choice on one page did not reach another. Rule: a filter that appears on more than one page is one reusable control with one shared backing, and the control is listed in `.claude/reference/ui-shared-controls.md`.

## Decisions (from the user)
- Each sub-filter becomes a reusable control. All pages showing that control use the same control and the same backing.
- Controls are listed in the reusable-controls index.
- Old per-page saved values are abandoned. No migration code.

## Decisions (from review)
- Empty providers means all providers, on every page. Pages never treat empty as nothing.
- The account-clearing rule lives in the control, not the pages.
- Live View injects the scoped `ReviewPageState`, not a local instance.

## Units
1. **Provider and account state (done).** Scoped `ProviderAccountFilterState` (Providers: string[]; Account: string?; OnChange) with one storage key, loaded and saved through `RightPanelFilterStore`. Test first: load restores providers and account; set saves them; a second page reading the state sees the same choice.
2. **Provider and account control (done, f7f96682).** `Components/Filters/ProviderAccountFilter.razor`: provider multi-select and account dropdown over the shared state. Test: changing the provider clears an account that no longer matches. The compile-error red was recorded, not a behavioural red.
3. **Adopt on Accounts, Manual Sync, Notices (done, ca9b8c77).** Pages removed their own provider and account state. Notices is multi-select over the shared set; its global notices show only when no provider is selected.
4. **Camera control (done, ca9b8c77).** Live View uses the scoped `ReviewPageState`. Live View and Review share one camera choice. The stored `live-view` camera restore from wip is kept and routed through that same state.
5. **Index (done, 8aaa8c57).** Both controls are listed in `.claude/reference/ui-shared-controls.md`.

## Open items
- Live View keeps a per-device stored camera choice under the `live-view` key. Review does not persist its camera choice there. Decide whether that asymmetry is wanted.
- The camera list is not linked to the chosen provider or account, so a stale camera id for an excluded account is possible. Out of scope for this plan.

## Done when
- No page keeps its own provider, account or camera filter state. (Met.)
- Every page showing a filter uses the shared control and backing. (Met.)
- The index lists both controls. (Met.)
