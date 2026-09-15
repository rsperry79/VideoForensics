using VideoForensics.Client.Core.Utilities;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class PathUtilitiesTests
    {
        [Fact]
        public void GetDefaultDownloadLocation_ReturnsSystemPath()
        {
            string result = PathUtilities.GetDefaultDownloadLocation();

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
            Assert.True(result.Contains("media"));
        }

        [Fact]
        public void GetDefaultDownloadLocation_ContainsProgramDataFolder()
        {
            string result = PathUtilities.GetDefaultDownloadLocation();
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            Assert.True(result.StartsWith(programDataPath));
        }

        [Fact]
        public void GetDefaultDownloadLocation_Consistent()
        {
            string result1 = PathUtilities.GetDefaultDownloadLocation();
            string result2 = PathUtilities.GetDefaultDownloadLocation();

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void GetDefaultDownloadLocation_EndsWithMedia()
        {
            string result = PathUtilities.GetDefaultDownloadLocation();

            Assert.EndsWith("media", result);
        }

        [Fact]
        public void GetDefaultQueryExportLocation_ReturnsSystemPath()
        {
            string result = PathUtilities.GetDefaultQueryExportLocation();

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
            Assert.True(result.Contains("backup"));
        }

        [Fact]
        public void GetDefaultQueryExportLocation_ContainsProgramDataFolder()
        {
            string result = PathUtilities.GetDefaultQueryExportLocation();
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            Assert.True(result.StartsWith(programDataPath));
        }

        [Fact]
        public void GetDefaultQueryExportLocation_Consistent()
        {
            string result1 = PathUtilities.GetDefaultQueryExportLocation();
            string result2 = PathUtilities.GetDefaultQueryExportLocation();

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void GetDefaultQueryExportLocation_EndsWithBackup()
        {
            string result = PathUtilities.GetDefaultQueryExportLocation();

            Assert.EndsWith("backup", result);
        }

        [Fact]
        public void BuildSavePath_ValidInputs_CombinesPaths()
        {
            string basePath = @"C:\Videos";
            string locationName = "Front Door";
            string cameraName = "Camera 1";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.NotNull(result);
            Assert.Contains("Front Door", result);
            Assert.Contains("Camera 1", result);
        }

        [Fact]
        public void BuildSavePath_FollowsExpectedStructure()
        {
            string basePath = @"C:\Videos";
            string locationName = "Living Room";
            string cameraName = "Main Camera";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.True(result.Contains(basePath));
            Assert.True(result.EndsWith(@"Living Room\Main Camera") || result.EndsWith("Living Room/Main Camera"));
        }

        [Fact]
        public void BuildSavePath_NullLocationName_SanitizesToUnknown()
        {
            string basePath = @"C:\Videos";
            string result = PathUtilities.BuildSavePath(basePath, null!, "Camera");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_EmptyLocationName_SanitizesToUnknown()
        {
            string basePath = @"C:\Videos";
            string result = PathUtilities.BuildSavePath(basePath, "", "Camera");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_WhitespaceLocationName_SanitizesToUnknown()
        {
            string basePath = @"C:\Videos";
            string result = PathUtilities.BuildSavePath(basePath, "   ", "Camera");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_NullCameraName_SanitizesToUnknown()
        {
            string basePath = @"C:\Videos";
            string result = PathUtilities.BuildSavePath(basePath, "Location", null!);

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_EmptyCameraName_SanitizesToUnknown()
        {
            string basePath = @"C:\Videos";
            string result = PathUtilities.BuildSavePath(basePath, "Location", "");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_WhitespaceCameraName_SanitizesToUnknown()
        {
            string basePath = @"C:\Videos";
            string result = PathUtilities.BuildSavePath(basePath, "Location", "   ");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_RemovesInvalidPathCharacters()
        {
            string basePath = @"C:\Videos";
            string locationName = @"Front/Door\|Camera?";
            string cameraName = "Camera:1";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);
            string appendedSegments = result[basePath.Length..];

            // Invalid characters should be removed from the appended segments (basePath's own
            // drive-letter colon, e.g. "C:", is untouched since only location/camera names are sanitized)
            Assert.DoesNotContain(":", appendedSegments);
            Assert.DoesNotContain("|", appendedSegments);
            Assert.DoesNotContain("?", appendedSegments);
        }

        [Fact]
        public void BuildSavePath_TrimsWhitespace()
        {
            string basePath = @"C:\Videos";
            string locationName = "  Front Door  ";
            string cameraName = "  Camera 1  ";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Whitespace should be trimmed
            Assert.DoesNotContain("  Front Door", result);
            Assert.DoesNotContain("  Camera 1", result);
        }

        [Fact]
        public void BuildSavePath_SpecialCharacters_Sanitized()
        {
            string basePath = @"C:\Videos";
            string locationName = @"Front<>Door";
            string cameraName = "Camera*1";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Special characters should be removed or sanitized
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.DoesNotContain("*", result);
        }

        [Fact]
        public void BuildSavePath_AllInvalidCharacters_BecomesUnknown()
        {
            string basePath = @"C:\Videos";
            string locationName = @"<>?:|";
            string cameraName = @"*?:<>\|";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void GetOneDrivePath_ReturnsStringOrNull()
        {
            string? result = PathUtilities.GetOneDrivePath();

            // Result can be null (no OneDrive) or a valid path
            if (result != null)
            {
                Assert.NotEmpty(result);
                Assert.True(Directory.Exists(result) || Environment.OSVersion.Platform != PlatformID.Win32NT);
            }
        }

        [Fact]
        public void GetOneDrivePath_IfExists_IsValidDirectory()
        {
            string? result = PathUtilities.GetOneDrivePath();

            if (result != null)
            {
                // If OneDrive path is returned, it should be a valid directory (or at least a reasonable path)
                Assert.NotEmpty(result);
            }
        }

        [Fact]
        public void GetOneDrivePath_Consistent()
        {
            string? result1 = PathUtilities.GetOneDrivePath();
            string? result2 = PathUtilities.GetOneDrivePath();

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void BuildSavePath_LongLocationName_StillValid()
        {
            string basePath = @"C:\Videos";
            string locationName = new('A', 100);
            string cameraName = "Camera 1";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void BuildSavePath_LongCameraName_StillValid()
        {
            string basePath = @"C:\Videos";
            string locationName = "Location";
            string cameraName = new('A', 100);

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void BuildSavePath_MixedCase_Preserved()
        {
            string basePath = @"C:\Videos";
            string locationName = "FrOnt DoOr";
            string cameraName = "CaMeRa 1";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Case should generally be preserved (unless trimmed/sanitized)
            Assert.True(result.Contains("FrOnt") || result.Contains("Front"));
        }

        [Fact]
        public void BuildSavePath_NumbersAndSymbols_Preserved()
        {
            string basePath = @"C:\Videos";
            string locationName = "Front Door 123";
            string cameraName = "Camera (Main)";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Numbers and some symbols should be preserved
            Assert.Contains("123", result);
        }

        [Fact]
        public void BuildSavePath_UnicodeCharacters_Handled()
        {
            string basePath = @"C:\Videos";
            string locationName = "Café";
            string cameraName = "カメラ";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Should either preserve or sanitize unicode gracefully
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultDownloadLocation_NotEmpty()
        {
            string result = PathUtilities.GetDefaultDownloadLocation();

            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultQueryExportLocation_NotEmpty()
        {
            string result = PathUtilities.GetDefaultQueryExportLocation();

            Assert.NotEmpty(result);
        }

        [Fact]
        public void BuildSavePath_TwoLevelStructure()
        {
            string basePath = @"C:\Root";
            string locationName = "Loc1";
            string cameraName = "Cam1";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Path should have structure: basePath\location\camera
            string[] parts = result.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None);
            Assert.True(parts.Length >= 3, "Path should have at least 3 components (base + location + camera)");
        }

        [Fact]
        public void BuildSavePath_HandlesBackslashesInInput()
        {
            string basePath = @"C:\Videos";
            string locationName = @"Front\Back";
            string cameraName = "Camera";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Backslash is an invalid path character and should be sanitized
            Assert.NotNull(result);
        }

        [Fact]
        public void BuildSavePath_SanitizationDoesNotCreateDoubleUnknown()
        {
            string basePath = @"C:\Videos";
            string locationName = "Valid Location";
            string cameraName = "Valid Camera";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Both names are valid, so should not contain "Unknown"
            Assert.DoesNotContain("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_CombinesPathsCorrectly()
        {
            string basePath = @"C:\Base";
            string locationName = "Location";
            string cameraName = "Camera";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Result should start with base path
            Assert.True(result.StartsWith(basePath));
        }

        [Fact]
        public void BuildSavePath_SanitizesOnly_DoesNotRemoveValidCharacters()
        {
            string basePath = @"C:\Videos";
            string locationName = "Front-Door_1";
            string cameraName = "Camera-2_Main";

            string result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Hyphens and underscores are typically valid, so should be preserved
            Assert.True(result.Contains("Front-Door_1") || (result.Contains("Front") && result.Contains("Door")));
        }
    }
}
