# Ui.Shared reusable controls

Paths below are relative to `src/client/ui/VideoForensics.Ui.Shared/src/`.

Feed data in through parameters. Do not copy a control into a page.

- `Components/Review/HistogramTimeline.razor`: scrub timeline (transport, speed, scrub bar, flag and time markers) above, any histogram below it via the `Histogram` render fragment. Use for any chart tied to playback.
- `Components/Review/BitrateSeriesChart.razor`: bar histogram of per-second bitrate with dip marks and a playhead. Seeks on click when given a start time.
- `Components/Viewer/MediaPlayer.razor`: the only video element. Playback, seek and rate go through it.
- `Components/Viewer/TimelineStrip.razor`: horizontal timeline strip over a time window.
- `Components/IntervalSecondsInput.razor`, `Components/LocalTimeText.razor`: shared input and time display; use them instead of local copies.
