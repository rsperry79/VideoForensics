using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Viewer;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Viewer
{
    /// <summary>
    /// bUnit tests for the review timeline, player, and camera grid. They check rendering and the wiring between components,
    /// not the timeline math, which is tested in Client.Common.
    /// </summary>
    public class ViewerComponentTests : BunitContext
    {
        private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        public ViewerComponentTests()
        {
            JSInterop.Mode = JSRuntimeMode.Loose;
            _ = Services.AddLocalization();
        }

        private static List<ClipWindow> TwoClips() =>
            new()
            {
                new("a", Start, 60),
                new("b", Start.AddSeconds(120), 60),
            };

        /// <summary>Verifies that the timeline draws one segment per clip, in the window.</summary>
        [Fact]
        public void TimelineStrip_Clips_RendersOneSegmentPerClip()
        {
            IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
                .Add(x => x.Clips, TwoClips())
                .Add(x => x.WindowStartUtc, Start)
                .Add(x => x.WindowEndUtc, Start.AddMinutes(10)));

            Assert.Equal(2, cut.FindAll("[data-testid='timeline-segment']").Count);
        }

        /// <summary>Verifies that a click reports the UTC time under it, using the strip's width.</summary>
        [Fact]
        public void TimelineStrip_Click_RaisesSeekWithTimeUnderClick()
        {
            DateTime? seeked = null;
            IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
                .Add(x => x.Clips, TwoClips())
                .Add(x => x.WindowStartUtc, Start)
                .Add(x => x.WindowEndUtc, Start.AddMinutes(10))
                .Add(x => x.WidthPx, 600)
                .Add(x => x.OnSeek, EventCallback.Factory.Create<DateTime>(this, t => seeked = t)));

            cut.Find("[data-testid='timeline-strip']").Click(new MouseEventArgs { OffsetX = 300 });

            Assert.Equal(Start.AddMinutes(5), seeked);
        }

        /// <summary>Verifies that a clip with a thumbnail draws its segment with the thumbnail as the background image.</summary>
        [Fact]
        public void TimelineStrip_ClipWithThumbnail_SegmentShowsBackgroundImage()
        {
            var clips = new List<ClipWindow> { new("a", Start, 60, "/media/a/thumb") };

            IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
                .Add(x => x.Clips, clips)
                .Add(x => x.WindowStartUtc, Start)
                .Add(x => x.WindowEndUtc, Start.AddMinutes(10)));

            string? style = cut.Find("[data-testid='timeline-segment']").GetAttribute("style");
            Assert.NotNull(style);
            Assert.Contains("background-image", style);
            Assert.Contains("/media/a/thumb", style);
        }

        /// <summary>Verifies that a clip without a thumbnail draws its segment with no background image.</summary>
        [Fact]
        public void TimelineStrip_ClipWithoutThumbnail_SegmentHasNoBackgroundImage()
        {
            var clips = new List<ClipWindow> { new("a", Start, 60) };

            IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
                .Add(x => x.Clips, clips)
                .Add(x => x.WindowStartUtc, Start)
                .Add(x => x.WindowEndUtc, Start.AddMinutes(10)));

            string? style = cut.Find("[data-testid='timeline-segment']").GetAttribute("style");
            Assert.NotNull(style);
            Assert.DoesNotContain("background-image", style);
        }

        /// <summary>Verifies that the cursor is drawn only when a playback time is set.</summary>
        [Fact]
        public void TimelineStrip_NoCursor_DrawsNoCursor()
        {
            IRenderedComponent<TimelineStrip> cut = Render<TimelineStrip>(p => p
                .Add(x => x.Clips, TwoClips())
                .Add(x => x.WindowStartUtc, Start)
                .Add(x => x.WindowEndUtc, Start.AddMinutes(10)));

            Assert.Empty(cut.FindAll("[data-testid='timeline-cursor']"));
        }

        /// <summary>Verifies that the player shows the clip covering the cursor and seeks it to the offset.</summary>
        [Fact]
        public void ReviewPlayer_CursorInClip_ShowsClipAndSeeksToOffset()
        {
            IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
                .Add(x => x.Clips, TwoClips())
                .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
                .Add(x => x.Cursor, Start.AddSeconds(130)));

            IElement video = cut.Find("[data-testid='review-player']");
            Assert.Equal("b", video.GetAttribute("data-clip-id"));
            Assert.Equal("/media/b/content", video.GetAttribute("src"));
            _ = JSInterop.VerifyInvoke("vfSetVideoTime");
        }

        /// <summary>Verifies that a cursor in a gap shows the gap message and no video.</summary>
        [Fact]
        public void ReviewPlayer_CursorInGap_ShowsGapMessage()
        {
            IRenderedComponent<ReviewPlayer> cut = Render<ReviewPlayer>(p => p
                .Add(x => x.Clips, TwoClips())
                .Add(x => x.ContentUrlFor, id => $"/media/{id}/content")
                .Add(x => x.Cursor, Start.AddSeconds(90)));

            Assert.Empty(cut.FindAll("[data-testid='review-player']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='review-player-gap']"));
        }

        /// <summary>Verifies that the grid renders one tile per camera when within the limit.</summary>
        [Fact]
        public void CameraGrid_WithinLimit_RendersOneTilePerCamera()
        {
            var tiles = new List<CameraTile>
            {
                new("Front", TwoClips(), id => $"/media/{id}/content"),
                new("Back", TwoClips(), id => $"/media/{id}/content"),
            };

            IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p
                .Add(x => x.Tiles, tiles)
                .Add(x => x.Cursor, Start.AddSeconds(10)));

            Assert.Equal(2, cut.FindAll("[data-testid='camera-tile']").Count);
            Assert.Empty(cut.FindAll("[data-testid='camera-grid-limit']"));
        }

        /// <summary>Verifies that the grid refuses more tiles than the maximum and shows the limit message instead.</summary>
        [Fact]
        public void CameraGrid_OverLimit_ShowsLimitMessageInsteadOfTiles()
        {
            var tiles = Enumerable.Range(0, CameraSync.MaxTiles + 1)
                .Select(i => new CameraTile($"Cam {i}", TwoClips(), id => $"/media/{id}/content"))
                .ToList();

            IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p.Add(x => x.Tiles, tiles));

            Assert.Empty(cut.FindAll("[data-testid='camera-tile']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='camera-grid-limit']"));
        }

        /// <summary>
        /// Queues playback reads for <c>vfGetVideoTime</c> in call order. Each setup matches one call and returns its value,
        /// so the master (tile 0) is read first and then each following tile.
        /// </summary>
        private void SetupPlaybackReads(params double[] seconds)
        {
            int reads = 0;
            foreach (double value in seconds)
            {
                double captured = value;
                int ordinal = reads++;
                _ = JSInterop.Setup<double>("vfGetVideoTime", _ => JSInterop.Invocations.Count(i => i.Identifier == "vfGetVideoTime") == ordinal + 1).SetResult(captured);
            }
        }

        private static List<CameraTile> TwoTiles() =>
            new()
            {
                new("Front", TwoClips(), id => $"/media/{id}/content"),
                new("Back", TwoClips(), id => $"/media/{id}/content"),
            };

        /// <summary>Verifies that a tile drifting past the tolerance is seeked to the master clock's absolute time.</summary>
        [Fact]
        public async Task CameraGrid_TileDrifted_ResyncsToMaster()
        {
            IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p
                .Add(x => x.Tiles, TwoTiles())
                .Add(x => x.Cursor, Start.AddSeconds(10)));

            int before = JSInterop.Invocations.Count(i => i.Identifier == "vfSetVideoTime");
            SetupPlaybackReads(10.0, 12.0);

            await cut.InvokeAsync(() => cut.Instance.SyncTilesAsync());

            Assert.Equal(1, JSInterop.Invocations.Count(i => i.Identifier == "vfSetVideoTime") - before);
        }

        /// <summary>Verifies that a tile within the tolerance is left alone.</summary>
        [Fact]
        public async Task CameraGrid_TilesWithinTolerance_DoNotResync()
        {
            IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p
                .Add(x => x.Tiles, TwoTiles())
                .Add(x => x.Cursor, Start.AddSeconds(10)));

            int before = JSInterop.Invocations.Count(i => i.Identifier == "vfSetVideoTime");
            SetupPlaybackReads(10.0, 10.2);

            await cut.InvokeAsync(() => cut.Instance.SyncTilesAsync());

            Assert.Equal(0, JSInterop.Invocations.Count(i => i.Identifier == "vfSetVideoTime") - before);
        }

        /// <summary>Verifies that a single-tile grid has nothing to sync and completes without seeking.</summary>
        [Fact]
        public async Task CameraGrid_SingleTile_SyncDoesNothing()
        {
            var tiles = new List<CameraTile> { new("Front", TwoClips(), id => $"/media/{id}/content") };
            IRenderedComponent<CameraGrid> cut = Render<CameraGrid>(p => p
                .Add(x => x.Tiles, tiles)
                .Add(x => x.Cursor, Start.AddSeconds(10)));

            int before = JSInterop.Invocations.Count(i => i.Identifier == "vfSetVideoTime");

            await cut.InvokeAsync(() => cut.Instance.SyncTilesAsync());

            Assert.Equal(0, JSInterop.Invocations.Count(i => i.Identifier == "vfSetVideoTime") - before);
        }
    }
}
