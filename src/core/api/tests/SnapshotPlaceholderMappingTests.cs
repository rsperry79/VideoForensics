using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    /// <summary>The placeholder flag on media and the skip reason on snapshot attempts survive the DTO mapping in both directions.</summary>
    public class SnapshotPlaceholderMappingTests
    {
        private static readonly DateTimeOffset CapturedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void MediaItemDto_IsPlaceholderTrue_RoundTripsThroughDto()
        {
            MediaItem item = BuildItem();
            item.IsPlaceholder = true;

            MediaItem roundTripped = item.ToDto().ToDomain();

            Assert.True(roundTripped.IsPlaceholder);
        }

        [Fact]
        public void MediaItemDto_IsPlaceholderDefault_IsFalse()
        {
            Assert.False(BuildItem().ToDto().IsPlaceholder);
            Assert.False(BuildItem().ToDto().ToDomain().IsPlaceholder);
        }

        [Fact]
        public void SnapshotCaptureDto_SkipReasonOffline_RoundTripsThroughDto()
        {
            SnapshotCapture skipped = SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 200, null, null, SnapshotSkipReason.Offline);

            SnapshotCaptureDto dto = skipped.ToDto();
            SnapshotCapture roundTripped = dto.ToDomain();

            Assert.Equal(SnapshotSkipReasonDto.Offline, dto.SkipReason);
            Assert.Equal(SnapshotSkipReason.Offline, roundTripped.SkipReason);
            Assert.Equal(200, roundTripped.HttpStatus);
            Assert.Null(roundTripped.MediaFilePath);
        }

        [Fact]
        public void SnapshotCaptureDto_SkipReasonIdenticalFrame_RoundTripsThroughDto()
        {
            SnapshotCapture skipped = SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "latest", 200, null, null, SnapshotSkipReason.IdenticalFrame);

            SnapshotCapture roundTripped = skipped.ToDto().ToDomain();

            Assert.Equal(SnapshotSkipReason.IdenticalFrame, roundTripped.SkipReason);
        }

        [Fact]
        public void SnapshotCaptureDto_NormalAttempt_SkipReasonStaysNull()
        {
            SnapshotCapture normal = SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 200, "Devices/x/frame.jpg", null);

            SnapshotCaptureDto dto = normal.ToDto();

            Assert.Null(dto.SkipReason);
            Assert.Null(dto.ToDomain().SkipReason);
        }

        private static MediaItem BuildItem()
        {
            return new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "frame.jpg",
                FilePath = @"D:\media\frame.jpg",
                MediaFormat = "jpeg",
                FileSizeBytes = 5442,
                RecordedAtUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
                DownloadedAtUtc = new DateTime(2026, 10, 20, 12, 5, 0, DateTimeKind.Utc),
                Sha256Hash = "2653d118053d",
            };
        }
    }
}
