using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class MediaFileNamerTests
    {
        [Theory]
        [InlineData("Front Door", 2026, 8, 27, 14, 30, 22, "video", "mp4", "Front_Door_20260827_143022_video.mp4")]
        [InlineData("Back Porch", 2026, 8, 27, 9, 15, 45, "snapshot", "jpg", "Back_Porch_20260827_091545_snapshot.jpg")]
        [InlineData("Garage", 2026, 12, 31, 23, 59, 59, "metadata", "json", "Garage_20261231_235959_metadata.json")]
        public void FormatMediaFileName_GeneratesCorrectFormat(
            string cameraName, int year, int month, int day, int hour, int minute, int second,
            string mediaType, string extension, string expected)
        {
            var timestamp = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            string result = MediaFileNamer.FormatMediaFileName(cameraName, timestamp, mediaType, extension);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Camera<1>", "Camera1")]
        [InlineData("Front:Door", "FrontDoor")]
        [InlineData("Back|Porch", "BackPorch")]
        [InlineData("Garage/Door", "GarageDoor")]
        [InlineData("Side\\Yard", "SideYard")]
        [InlineData("Driveway\"Cam\"", "DrivewayCam")]
        [InlineData("Attic?Monitor", "AtticMonitor")]
        [InlineData("Basement*Cam", "BasementCam")]
        [InlineData("Living Room", "Living_Room")] // Spaces become underscores
        [InlineData("Kitchen   Cam", "Kitchen_Cam")] // Multiple spaces collapsed and replaced
        public void SanitizeForFilePath_RemovesInvalidCharacters(string input, string expected)
        {
            string result = MediaFileNamer.SanitizeForFilePath(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizeForFilePath_RemovesControlCharacters()
        {
            // \u escapes (fixed 4 hex digits) used rather than \x (variable-length, 1-4 hex
            // digits, which greedily consumes following hex-digit-looking letters) - \x00Door
            // would parse as \x00D (a 3-digit hex escape, 0x0D) followed by "oor", silently
            // swallowing the "D" from "Door".
            string input = "Front\u0000Door\u0001Monitor\u001F";
            string result = MediaFileNamer.SanitizeForFilePath(input);
            // Control characters are stripped entirely, not replaced with underscore - see
            // MediaFileNamer.SanitizeForFilePath's regex, which maps them to string.Empty.
            Assert.Equal("FrontDoorMonitor", result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData(null)]
        public void SanitizeForFilePath_ReturnsFallback_WhenInputIsEmpty(string input)
        {
            string result = MediaFileNamer.SanitizeForFilePath(input ?? "");
            Assert.Equal("device", result);
        }

        [Theory]
        [InlineData("<<>>")]
        [InlineData("::")]
        [InlineData("||||")]
        [InlineData("\u0000\u0001\u0002")]
        public void SanitizeForFilePath_ReturnsFallback_WhenAllCharactersAreInvalid(string input)
        {
            string result = MediaFileNamer.SanitizeForFilePath(input);
            Assert.Equal("device", result);
        }

        [Fact]
        public void SanitizeForFilePath_TrimsTrailingWhitespace()
        {
            string input = "  Front Door  ";
            string result = MediaFileNamer.SanitizeForFilePath(input);
            Assert.Equal("Front_Door", result);
        }

        [Fact]
        public void FormatMediaFileName_WithSpecialCharactersInName()
        {
            var timestamp = new DateTime(2026, 8, 27, 14, 30, 22, DateTimeKind.Utc);
            string result = MediaFileNamer.FormatMediaFileName("Front<>Door:Home", timestamp, "video", "mp4");
            Assert.Equal("FrontDoorHome_20260827_143022_video.mp4", result);
        }

        [Fact]
        public void FormatMediaFileName_WithoutDotInExtension()
        {
            var timestamp = new DateTime(2026, 8, 27, 14, 30, 22, DateTimeKind.Utc);
            string result = MediaFileNamer.FormatMediaFileName("Front Door", timestamp, "video", ".mp4");
            Assert.Equal("Front_Door_20260827_143022_video.mp4", result);
        }

        [Fact]
        public void FormatMediaFileName_PreservesLowercaseExtension()
        {
            var timestamp = new DateTime(2026, 8, 27, 14, 30, 22, DateTimeKind.Utc);
            string result = MediaFileNamer.FormatMediaFileName("Front Door", timestamp, "video", "mp4");
            Assert.Equal("Front_Door_20260827_143022_video.mp4", result);
        }

        [Theory]
        [InlineData("video")]
        [InlineData("snapshot")]
        [InlineData("metadata")]
        [InlineData("sidecar")]
        public void FormatMediaFileName_AcceptsMultipleMediaTypes(string mediaType)
        {
            var timestamp = new DateTime(2026, 8, 27, 14, 30, 22, DateTimeKind.Utc);
            string result = MediaFileNamer.FormatMediaFileName("Camera", timestamp, mediaType, "ext");
            Assert.Contains($"_{mediaType}.", result);
        }

        [Fact]
        public void FormatMediaFileName_IsOneDriveSafe()
        {
            // OneDrive doesn't allow: < > : " / \ | ? *
            var timestamp = new DateTime(2026, 8, 27, 14, 30, 22, DateTimeKind.Utc);
            string problematicName = "Front<Door>:Home/Garden|Cam?*Device";
            string result = MediaFileNamer.FormatMediaFileName(problematicName, timestamp, "video", "mp4");

            // Should not contain any forbidden characters
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.DoesNotContain(":", result);
            Assert.DoesNotContain("\"", result);
            Assert.DoesNotContain("/", result);
            Assert.DoesNotContain("\\", result);
            Assert.DoesNotContain("|", result);
            Assert.DoesNotContain("?", result);
            Assert.DoesNotContain("*", result);
        }

        [Fact]
        public void FormatMediaFileName_ConsistencyAcrossTypes()
        {
            string cameraName = "Front Door";
            var timestamp = new DateTime(2026, 8, 27, 14, 30, 22, DateTimeKind.Utc);

            string videoFile = MediaFileNamer.FormatMediaFileName(cameraName, timestamp, "video", "mp4");
            string metadataFile = MediaFileNamer.FormatMediaFileName(cameraName, timestamp, "metadata", "json");

            // Both should have same prefix (name_date_time_)
            string prefix = "Front_Door_20260827_143022_";
            Assert.StartsWith(prefix, videoFile);
            Assert.StartsWith(prefix, metadataFile);

            // Should differ only in type and extension
            Assert.Equal($"{prefix}video.mp4", videoFile);
            Assert.Equal($"{prefix}metadata.json", metadataFile);
        }
    }
}
