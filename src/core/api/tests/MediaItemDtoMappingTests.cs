using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class MediaItemDtoMappingTests
    {
        [Fact]
        public void MediaItemDto_DurationAndPreviewPath_RoundTripThroughDto()
        {
            MediaItem item = BuildItem();
            item.DurationSeconds = 61.5;
            item.PreviewPath = @"D:\media\ring\home\front\clip.preview.mp4";

            MediaItem roundTripped = item.ToDto().ToDomain();

            Assert.Equal(61.5, roundTripped.DurationSeconds);
            Assert.Equal(@"D:\media\ring\home\front\clip.preview.mp4", roundTripped.PreviewPath);
        }

        [Fact]
        public void MediaItemDto_NullDurationAndPreview_StayNull()
        {
            MediaItem roundTripped = BuildItem().ToDto().ToDomain();

            Assert.Null(roundTripped.DurationSeconds);
            Assert.Null(roundTripped.PreviewPath);
        }

        [Fact]
        public void MediaItemDto_OlderPayloadWithoutNewFields_DeserializesWithNulls()
        {
            // A server that predates these fields still talks to a newer client.
            string json = JsonSerializer.Serialize(BuildItem().ToDto());
            using JsonDocument document = JsonDocument.Parse(json);
            var trimmed = document.RootElement.EnumerateObject()
                .Where(p => p.Name is not ("DurationSeconds" or "PreviewPath"))
                .ToDictionary(p => p.Name, p => p.Value.Clone());

            MediaItemDto? dto = JsonSerializer.Deserialize<MediaItemDto>(JsonSerializer.Serialize(trimmed));

            Assert.NotNull(dto);
            Assert.Null(dto.DurationSeconds);
            Assert.Null(dto.PreviewPath);
        }

        private static MediaItem BuildItem()
        {
            return new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "clip.mp4",
                FilePath = @"D:\media\clip.mp4",
                MediaFormat = "mp4",
                FileSizeBytes = 1024,
                RecordedAtUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
                DownloadedAtUtc = new DateTime(2026, 10, 20, 12, 5, 0, DateTimeKind.Utc),
                Sha256Hash = "abc",
            };
        }
    }
}
