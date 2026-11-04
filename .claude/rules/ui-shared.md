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

## Reusable controls

Use these before writing a new control. Feed data in through parameters; do not copy a control into a page.

- `Components/Review/HistogramTimeline.razor`: scrub timeline (transport, speed, scrub bar, flag and time markers) above, any histogram below it via the `Histogram` render fragment. Use for any chart tied to playback.
- `Components/Review/BitrateSeriesChart.razor`: bar histogram of per-second bitrate with dip marks and a playhead. Seeks on click when given a start time.
- `Components/Viewer/MediaPlayer.razor`: the only video element. Playback, seek and rate go through it.
- `Components/Viewer/TimelineStrip.razor`: horizontal timeline strip over a time window.
- `Components/IntervalSecondsInput.razor`, `Components/LocalTimeText.razor`: shared input and time display; use them instead of local copies.
