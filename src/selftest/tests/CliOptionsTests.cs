using Xunit;
using VideoForensics.Providers.Ring.SelfTester;

namespace VideoForensics.Providers.Ring.SelfTester.Tests
{
    public class CliOptionsTests
    {
        [Fact]
        public void Parse_WithHelpFlag_SetsShowHelpTrue()
        {
            var args = new[] { "--help" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.ShowHelp);
        }

        [Fact]
        public void Parse_WithShortHelpFlag_SetsShowHelpTrue()
        {
            var args = new[] { "-h" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.ShowHelp);
        }

        [Fact]
        public void Parse_WithListFlag_SetsListEndpointsTrue()
        {
            var args = new[] { "--list" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.ListEndpoints);
        }

        [Fact]
        public void Parse_WithListEndpointsJsonFlag_SetsListEndpointsAndListEndpointsJsonTrue()
        {
            var args = new[] { "--list-endpoints-json" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.ListEndpoints);
            Assert.True(options.ListEndpointsJson);
        }

        [Fact]
        public void Parse_WithAuthFlag_SetsInteractiveAuthTrue()
        {
            var args = new[] { "--auth" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.InteractiveAuth);
        }

        [Fact]
        public void Parse_WithEndpointsCsv_ParsesEndpointsList()
        {
            var args = new[] { "--endpoints", "endpoint1,endpoint2,endpoint3" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(3, options.Endpoints.Count);
            Assert.Contains("endpoint1", options.Endpoints);
            Assert.Contains("endpoint2", options.Endpoints);
            Assert.Contains("endpoint3", options.Endpoints);
        }

        [Fact]
        public void Parse_WithAllFlag_AddsAllToEndpoints()
        {
            var args = new[] { "--all" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Single(options.Endpoints);
            Assert.Contains("all", options.Endpoints);
        }

        [Fact]
        public void Parse_WithNoEndpoints_DefaultsToAll()
        {
            var args = Array.Empty<string>();

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Single(options.Endpoints);
            Assert.Contains("all", options.Endpoints);
        }

        [Fact]
        public void Parse_WithOutputDir_SetsOutputDirValue()
        {
            var args = new[] { "--output-dir", "/tmp/output" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("/tmp/output", options.OutputDir);
        }

        [Fact]
        public void Parse_WithLocationId_ParsesGuid()
        {
            var guid = Guid.NewGuid().ToString();
            var args = new[] { "--location-id", guid };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(Guid.Parse(guid), options.LocationId);
        }

        [Fact]
        public void Parse_WithInvalidLocationId_ReturnsError()
        {
            var args = new[] { "--location-id", "not-a-guid" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("not a valid GUID", error);
        }

        [Fact]
        public void Parse_WithDoorbotId_ParsesLong()
        {
            var args = new[] { "--doorbot-id", "12345" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(12345L, options.DoorbotId);
        }

        [Fact]
        public void Parse_WithInvalidDoorbotId_ReturnsError()
        {
            var args = new[] { "--doorbot-id", "not-a-number" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("not a valid integer", error);
        }

        [Fact]
        public void Parse_WithChimeId_ParsesLong()
        {
            var args = new[] { "--chime-id", "67890" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(67890L, options.ChimeId);
        }

        [Fact]
        public void Parse_WithHistoryLimit_ParsesInt()
        {
            var args = new[] { "--history-limit", "10" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(10, options.HistoryLimit);
        }

        [Fact]
        public void Parse_WithHistoryLimitZero_ReturnsError()
        {
            var args = new[] { "--history-limit", "0" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("must be a positive integer", error);
        }

        [Fact]
        public void Parse_WithHistoryLimitNegative_ReturnsError()
        {
            var args = new[] { "--history-limit", "-5" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("must be a positive integer", error);
        }

        [Fact]
        public void Parse_WithDestructiveFlag_SetsDestructiveTrue()
        {
            var args = new[] { "--destructive" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.Destructive);
        }

        [Fact]
        public void Parse_WithNoPhysicalFlag_SetsNoPhysicalTrue()
        {
            var args = new[] { "--no-physical" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.NoPhysical);
        }

        [Fact]
        public void Parse_WithSirenDurationSeconds_ParsesInt()
        {
            var args = new[] { "--siren-duration-seconds", "5" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(5, options.SirenDurationSeconds);
        }

        [Fact]
        public void Parse_WithVolumeLevel_ParsesInt()
        {
            var args = new[] { "--volume-level", "8" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(8, options.VolumeLevel);
        }

        [Fact]
        public void Parse_WithVolumeLevelNegative_ReturnsError()
        {
            var args = new[] { "--volume-level", "-1" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("must be a non-negative integer", error);
        }

        [Fact]
        public void Parse_WithChimeTypeValue_ParsesInt()
        {
            var args = new[] { "--chime-type-value", "1" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(1, options.ChimeTypeValue);
        }

        [Fact]
        public void Parse_WithChimeTypeValueInvalidRange_ReturnsError()
        {
            var args = new[] { "--chime-type-value", "5" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("must be 0, 1 or 2", error);
        }

        [Fact]
        public void Parse_WithDndSeconds_ParsesInt()
        {
            var args = new[] { "--dnd-seconds", "120" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(120, options.DndSeconds);
        }

        [Fact]
        public void Parse_WithDndSecondsZero_ReturnsError()
        {
            var args = new[] { "--dnd-seconds", "0" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("must be a positive integer", error);
        }

        [Fact]
        public void Parse_WithLocationModeValueHome_SetsValue()
        {
            var args = new[] { "--location-mode-value", "home" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("home", options.LocationModeValue);
        }

        [Fact]
        public void Parse_WithLocationModeValueAway_SetsValue()
        {
            var args = new[] { "--location-mode-value", "away" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("away", options.LocationModeValue);
        }

        [Fact]
        public void Parse_WithLocationModeValueDisarmed_SetsValue()
        {
            var args = new[] { "--location-mode-value", "disarmed" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("disarmed", options.LocationModeValue);
        }

        [Fact]
        public void Parse_WithLocationModeValueInvalid_ReturnsError()
        {
            var args = new[] { "--location-mode-value", "invalid" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("must be one of: home, away, disarmed", error);
        }

        [Fact]
        public void Parse_WithDingId_SetsValue()
        {
            var args = new[] { "--ding-id", "ding123" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("ding123", options.DingId);
        }

        [Fact]
        public void Parse_WithAssetUuid_SetsValue()
        {
            var args = new[] { "--asset-uuid", "uuid-abc-123" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("uuid-abc-123", options.AssetUuid);
        }

        [Fact]
        public void Parse_WithPushToken_SetsValue()
        {
            var args = new[] { "--push-token", "token-xyz" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("token-xyz", options.PushToken);
        }

        [Fact]
        public void Parse_WithUsername_SetsValue()
        {
            var args = new[] { "--username", "testuser" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("testuser", options.UserName);
        }

        [Fact]
        public void Parse_WithPassword_SetsValue()
        {
            var args = new[] { "--password", "testpass" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("testpass", options.Password);
        }

        [Fact]
        public void Parse_WithRefreshToken_SetsValue()
        {
            var args = new[] { "--refresh-token", "refresh123" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("refresh123", options.RefreshToken);
        }

        [Fact]
        public void Parse_WithQuietFlag_SetsQuietTrue()
        {
            var args = new[] { "--quiet" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.Quiet);
        }

        [Fact]
        public void Parse_WithVerifyDbFlag_SetsVerifyDbTrue()
        {
            var args = new[] { "--verify-db" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.True(options.VerifyDb);
        }

        [Fact]
        public void Parse_WithDbPath_SetsValue()
        {
            var args = new[] { "--db-path", "/path/to/db.sqlite" };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal("/path/to/db.sqlite", options.DbPath);
        }

        [Fact]
        public void Parse_WithUnrecognizedArgument_ReturnsError()
        {
            var args = new[] { "--unknown-flag" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("Unrecognized argument", error);
        }

        [Fact]
        public void Parse_WithMissingValue_ReturnsError()
        {
            var args = new[] { "--endpoints" };

            var (options, error) = CliOptions.Parse(args);

            Assert.NotNull(error);
            Assert.Null(options);
            Assert.Contains("Missing value", error);
        }

        [Fact]
        public void Parse_WithMultipleOptions_ParsesAll()
        {
            var args = new[]
            {
                "--endpoints", "endpoint1,endpoint2",
                "--output-dir", "/tmp",
                "--history-limit", "7",
                "--quiet",
                "--destructive"
            };

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.Equal(2, options.Endpoints.Count);
            Assert.Equal("/tmp", options.OutputDir);
            Assert.Equal(7, options.HistoryLimit);
            Assert.True(options.Quiet);
            Assert.True(options.Destructive);
        }

        [Fact]
        public void Parse_DefaultValues_AreCorrect()
        {
            var args = Array.Empty<string>();

            var (options, error) = CliOptions.Parse(args);

            Assert.Null(error);
            Assert.NotNull(options);
            Assert.False(options.ShowHelp);
            Assert.False(options.ListEndpoints);
            Assert.False(options.ListEndpointsJson);
            Assert.False(options.InteractiveAuth);
            Assert.Null(options.OutputDir);
            Assert.Null(options.LocationId);
            Assert.Null(options.DoorbotId);
            Assert.Null(options.ChimeId);
            Assert.Equal(5, options.HistoryLimit);
            Assert.False(options.Destructive);
            Assert.False(options.NoPhysical);
            Assert.Equal(2, options.SirenDurationSeconds);
            Assert.Null(options.VolumeLevel);
            Assert.Null(options.ChimeTypeValue);
            Assert.Equal(60, options.DndSeconds);
            Assert.Null(options.LocationModeValue);
            Assert.Null(options.DingId);
            Assert.Null(options.AssetUuid);
            Assert.Null(options.PushToken);
            Assert.Null(options.UserName);
            Assert.Null(options.Password);
            Assert.Null(options.RefreshToken);
            Assert.False(options.Quiet);
            Assert.False(options.VerifyDb);
            Assert.Null(options.DbPath);
        }
    }
}
