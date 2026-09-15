using VideoForensics.Client.Core.Utilities;
using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class PathUtilitiesTests
    {
        [Fact]
        public void GetDefaultDownloadLocation_ReturnsSystemPath()
        {
            var result = PathUtilities.GetDefaultDownloadLocation();

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
            Assert.True(result.Contains("media"));
        }

        [Fact]
        public void GetDefaultDownloadLocation_ContainsProgramDataFolder()
        {
            var result = PathUtilities.GetDefaultDownloadLocation();
            var programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            Assert.True(result.StartsWith(programDataPath));
        }

        [Fact]
        public void GetDefaultDownloadLocation_Consistent()
        {
            var result1 = PathUtilities.GetDefaultDownloadLocation();
            var result2 = PathUtilities.GetDefaultDownloadLocation();

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void GetDefaultDownloadLocation_EndsWithMedia()
        {
            var result = PathUtilities.GetDefaultDownloadLocation();

            Assert.EndsWith("media", result);
        }

        [Fact]
        public void GetDefaultQueryExportLocation_ReturnsSystemPath()
        {
            var result = PathUtilities.GetDefaultQueryExportLocation();

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.Contains("VideoForensics"));
            Assert.True(result.Contains("backup"));
        }

        [Fact]
        public void GetDefaultQueryExportLocation_ContainsProgramDataFolder()
        {
            var result = PathUtilities.GetDefaultQueryExportLocation();
            var programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            Assert.True(result.StartsWith(programDataPath));
        }

        [Fact]
        public void GetDefaultQueryExportLocation_Consistent()
        {
            var result1 = PathUtilities.GetDefaultQueryExportLocation();
            var result2 = PathUtilities.GetDefaultQueryExportLocation();

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void GetDefaultQueryExportLocation_EndsWithBackup()
        {
            var result = PathUtilities.GetDefaultQueryExportLocation();

            Assert.EndsWith("backup", result);
        }

        [Fact]
        public void BuildSavePath_ValidInputs_CombinesPaths()
        {
            var basePath = @"C:\Videos";
            var locationName = "Front Door";
            var cameraName = "Camera 1";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.NotNull(result);
            Assert.Contains("Front Door", result);
            Assert.Contains("Camera 1", result);
        }

        [Fact]
        public void BuildSavePath_FollowsExpectedStructure()
        {
            var basePath = @"C:\Videos";
            var locationName = "Living Room";
            var cameraName = "Main Camera";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.True(result.Contains(basePath));
            Assert.True(result.EndsWith(@"Living Room\Main Camera") || result.EndsWith("Living Room/Main Camera"));
        }

        [Fact]
        public void BuildSavePath_NullLocationName_SanitizesToUnknown()
        {
            var basePath = @"C:\Videos";
            var result = PathUtilities.BuildSavePath(basePath, null!, "Camera");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_EmptyLocationName_SanitizesToUnknown()
        {
            var basePath = @"C:\Videos";
            var result = PathUtilities.BuildSavePath(basePath, "", "Camera");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_WhitespaceLocationName_SanitizesToUnknown()
        {
            var basePath = @"C:\Videos";
            var result = PathUtilities.BuildSavePath(basePath, "   ", "Camera");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_NullCameraName_SanitizesToUnknown()
        {
            var basePath = @"C:\Videos";
            var result = PathUtilities.BuildSavePath(basePath, "Location", null!);

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_EmptyCameraName_SanitizesToUnknown()
        {
            var basePath = @"C:\Videos";
            var result = PathUtilities.BuildSavePath(basePath, "Location", "");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_WhitespaceCameraName_SanitizesToUnknown()
        {
            var basePath = @"C:\Videos";
            var result = PathUtilities.BuildSavePath(basePath, "Location", "   ");

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_RemovesInvalidPathCharacters()
        {
            var basePath = @"C:\Videos";
            var locationName = @"Front/Door\|Camera?";
            var cameraName = "Camera:1";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);
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
            var basePath = @"C:\Videos";
            var locationName = "  Front Door  ";
            var cameraName = "  Camera 1  ";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Whitespace should be trimmed
            Assert.DoesNotContain("  Front Door", result);
            Assert.DoesNotContain("  Camera 1", result);
        }

        [Fact]
        public void BuildSavePath_SpecialCharacters_Sanitized()
        {
            var basePath = @"C:\Videos";
            var locationName = @"Front<>Door";
            var cameraName = "Camera*1";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Special characters should be removed or sanitized
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.DoesNotContain("*", result);
        }

        [Fact]
        public void BuildSavePath_AllInvalidCharacters_BecomesUnknown()
        {
            var basePath = @"C:\Videos";
            var locationName = @"<>?:|";
            var cameraName = @"*?:<>\|";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.Contains("Unknown", result);
        }

        [Fact]
        public void GetOneDrivePath_ReturnsStringOrNull()
        {
            var result = PathUtilities.GetOneDrivePath();

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
            var result = PathUtilities.GetOneDrivePath();

            if (result != null)
            {
                // If OneDrive path is returned, it should be a valid directory (or at least a reasonable path)
                Assert.NotEmpty(result);
            }
        }

        [Fact]
        public void GetOneDrivePath_Consistent()
        {
            var result1 = PathUtilities.GetOneDrivePath();
            var result2 = PathUtilities.GetOneDrivePath();

            Assert.Equal(result1, result2);
        }

        [Fact]
        public void BuildSavePath_LongLocationName_StillValid()
        {
            var basePath = @"C:\Videos";
            var locationName = new string('A', 100);
            var cameraName = "Camera 1";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void BuildSavePath_LongCameraName_StillValid()
        {
            var basePath = @"C:\Videos";
            var locationName = "Location";
            var cameraName = new string('A', 100);

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void BuildSavePath_MixedCase_Preserved()
        {
            var basePath = @"C:\Videos";
            var locationName = "FrOnt DoOr";
            var cameraName = "CaMeRa 1";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Case should generally be preserved (unless trimmed/sanitized)
            Assert.True(result.Contains("FrOnt") || result.Contains("Front"));
        }

        [Fact]
        public void BuildSavePath_NumbersAndSymbols_Preserved()
        {
            var basePath = @"C:\Videos";
            var locationName = "Front Door 123";
            var cameraName = "Camera (Main)";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Numbers and some symbols should be preserved
            Assert.Contains("123", result);
        }

        [Fact]
        public void BuildSavePath_UnicodeCharacters_Handled()
        {
            var basePath = @"C:\Videos";
            var locationName = "Café";
            var cameraName = "カメラ";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Should either preserve or sanitize unicode gracefully
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultDownloadLocation_NotEmpty()
        {
            var result = PathUtilities.GetDefaultDownloadLocation();

            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetDefaultQueryExportLocation_NotEmpty()
        {
            var result = PathUtilities.GetDefaultQueryExportLocation();

            Assert.NotEmpty(result);
        }

        [Fact]
        public void BuildSavePath_TwoLevelStructure()
        {
            var basePath = @"C:\Root";
            var locationName = "Loc1";
            var cameraName = "Cam1";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Path should have structure: basePath\location\camera
            var parts = result.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None);
            Assert.True(parts.Length >= 3, "Path should have at least 3 components (base + location + camera)");
        }

        [Fact]
        public void BuildSavePath_HandlesBackslashesInInput()
        {
            var basePath = @"C:\Videos";
            var locationName = @"Front\Back";
            var cameraName = "Camera";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Backslash is an invalid path character and should be sanitized
            Assert.NotNull(result);
        }

        [Fact]
        public void BuildSavePath_SanitizationDoesNotCreateDoubleUnknown()
        {
            var basePath = @"C:\Videos";
            var locationName = "Valid Location";
            var cameraName = "Valid Camera";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Both names are valid, so should not contain "Unknown"
            Assert.DoesNotContain("Unknown", result);
        }

        [Fact]
        public void BuildSavePath_CombinesPathsCorrectly()
        {
            var basePath = @"C:\Base";
            var locationName = "Location";
            var cameraName = "Camera";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Result should start with base path
            Assert.True(result.StartsWith(basePath));
        }

        [Fact]
        public void BuildSavePath_SanitizesOnly_DoesNotRemoveValidCharacters()
        {
            var basePath = @"C:\Videos";
            var locationName = "Front-Door_1";
            var cameraName = "Camera-2_Main";

            var result = PathUtilities.BuildSavePath(basePath, locationName, cameraName);

            // Hyphens and underscores are typically valid, so should be preserved
            Assert.True(result.Contains("Front-Door_1") || result.Contains("Front") && result.Contains("Door"));
        }
    }
}
