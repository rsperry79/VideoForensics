using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// Characterization of the selftest command line. These pin what the hand-written parser does today, so the move to
    /// System.CommandLine keeps every option, default and error message. Each case is a behaviour the tool relies on:
    /// the exit codes are decided by Program from these results.
    /// </summary>
    public sealed class CliOptions_Tests
    {
        private static CliOptions Parsed(params string[] args)
        {
            (CliOptions? options, string? error) = CliOptions.Parse(args);
            Assert.Null(error);
            Assert.NotNull(options);
            return options!;
        }

        private static string ErrorOf(params string[] args)
        {
            (CliOptions? options, string? error) = CliOptions.Parse(args);
            Assert.Null(options);
            Assert.NotNull(error);
            return error!;
        }

        [Fact]
        public void NoArguments_UsesDefaults()
        {
            CliOptions options = Parsed();

            Assert.Equal(["all"], options.Endpoints);
            Assert.Equal(5, options.HistoryLimit);
            Assert.Equal(2, options.SirenDurationSeconds);
            Assert.Equal(60, options.DndSeconds);
            Assert.False(options.Destructive);
            Assert.False(options.NoPhysical);
            Assert.False(options.Quiet);
            Assert.False(options.ShowHelp);
            Assert.False(options.ListEndpoints);
            Assert.False(options.InteractiveAuth);
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        public void HelpFlags_SetShowHelp(string flag)
        {
            Assert.True(Parsed(flag).ShowHelp);
        }

        [Theory]
        [InlineData("--list")]
        [InlineData("--list-endpoints")]
        public void ListFlags_SetListEndpoints(string flag)
        {
            CliOptions options = Parsed(flag);

            Assert.True(options.ListEndpoints);
            Assert.False(options.ListEndpointsJson);
        }

        [Fact]
        public void ListEndpointsJson_SetsBothListAndJson()
        {
            CliOptions options = Parsed("--list-endpoints-json");

            Assert.True(options.ListEndpoints);
            Assert.True(options.ListEndpointsJson);
        }

        [Theory]
        [InlineData("--auth", nameof(CliOptions.InteractiveAuth))]
        [InlineData("--destructive", nameof(CliOptions.Destructive))]
        [InlineData("--no-physical", nameof(CliOptions.NoPhysical))]
        [InlineData("--quiet", nameof(CliOptions.Quiet))]
        [InlineData("--verify-db", nameof(CliOptions.VerifyDb))]
        [InlineData("--list-unknown", nameof(CliOptions.ListUnknown))]
        [InlineData("--strip-raw-payload", nameof(CliOptions.StripRawPayload))]
        [InlineData("--yes", nameof(CliOptions.Yes))]
        public void BooleanFlags_SetTheirOption(string flag, string property)
        {
            CliOptions options = Parsed(flag);

            Assert.True((bool)typeof(CliOptions).GetField(property)?.GetValue(options)! || (bool)typeof(CliOptions).GetProperty(property)?.GetValue(options)!);
        }

        [Fact]
        public void AllFlag_AddsAllEndpoint()
        {
            Assert.Equal(["all"], Parsed("--all").Endpoints);
        }

        [Fact]
        public void EndpointsCsv_SplitsTrimsAndDropsBlanks()
        {
            Assert.Equal(["alpha", "beta", "gamma"], Parsed("--endpoints", " alpha, beta,,gamma ").Endpoints);
        }

        [Fact]
        public void EndpointsRepeated_IsRejected_UseOneCommaList()
        {
            // Changed by the System.CommandLine migration (approved): a repeated value option is an error, and the
            // keys go in one comma-separated list instead.
            Assert.Equal("--endpoints may be given only once", ErrorOf("--endpoints", "alpha", "--endpoints", "beta"));
        }

        [Theory]
        [InlineData("--output-dir", "OutputDir", "C:\\out")]
        [InlineData("--db-path", "DbPath", "C:\\db\\x.db")]
        [InlineData("--check-dir", "CheckDir", "C:\\run")]
        [InlineData("--dump-file", "DumpFile", "C:\\dump.ndjson")]
        [InlineData("--ding-id", "DingId", "123")]
        [InlineData("--asset-uuid", "AssetUuid", "abc-uuid")]
        [InlineData("--push-token", "PushToken", "tok")]
        [InlineData("--username", "UserName", "alice")]
        [InlineData("--password", "Password", "pw")]
        [InlineData("--refresh-token", "RefreshToken", "rt")]
        public void StringOptions_StoreTheirValue(string flag, string property, string value)
        {
            CliOptions options = Parsed(flag, value);

            Assert.Equal(value, typeof(CliOptions).GetField(property)?.GetValue(options) as string
                ?? typeof(CliOptions).GetProperty(property)?.GetValue(options) as string);
        }

        [Fact]
        public void LocationId_ParsesGuid()
        {
            Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");

            Assert.Equal(id, Parsed("--location-id", id.ToString()).LocationId);
        }

        [Fact]
        public void LocationId_InvalidGuid_ReportsValue()
        {
            Assert.Equal("--location-id value 'nope' is not a valid GUID", ErrorOf("--location-id", "nope"));
        }

        [Fact]
        public void DoorbotAndChimeIds_ParseLong()
        {
            CliOptions options = Parsed("--doorbot-id", "123456789012", "--chime-id", "42");

            Assert.Equal(123456789012L, options.DoorbotId);
            Assert.Equal(42L, options.ChimeId);
        }

        [Fact]
        public void DoorbotId_NotAnInteger_ReportsValue()
        {
            Assert.Equal("--doorbot-id value 'x' is not a valid integer", ErrorOf("--doorbot-id", "x"));
        }

        [Fact]
        public void ChimeId_NotAnInteger_ReportsValue()
        {
            Assert.Equal("--chime-id value 'x' is not a valid integer", ErrorOf("--chime-id", "x"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-3")]
        [InlineData("many")]
        public void HistoryLimit_MustBePositive(string value)
        {
            Assert.Equal($"--history-limit value '{value}' must be a positive integer", ErrorOf("--history-limit", value));
        }

        [Fact]
        public void HistoryLimit_ValidValueIsStored()
        {
            Assert.Equal(7, Parsed("--history-limit", "7").HistoryLimit);
        }

        [Theory]
        [InlineData("--siren-duration-seconds", "SirenDurationSeconds", "0")]
        [InlineData("--dnd-seconds", "DndSeconds", "0")]
        public void PositiveSeconds_RejectZero(string flag, string property, string value)
        {
            Assert.Equal($"{flag} value '{value}' must be a positive integer", ErrorOf(flag, value));
        }

        [Fact]
        public void SirenAndDnd_ValidValuesAreStored()
        {
            CliOptions options = Parsed("--siren-duration-seconds", "9", "--dnd-seconds", "120");

            Assert.Equal(9, options.SirenDurationSeconds);
            Assert.Equal(120, options.DndSeconds);
        }

        [Fact]
        public void VolumeLevel_AllowsZero_RejectsNegative()
        {
            Assert.Equal(0, Parsed("--volume-level", "0").VolumeLevel);
            Assert.Equal("--volume-level value '-1' must be a non-negative integer", ErrorOf("--volume-level", "-1"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("2")]
        public void ChimeTypeValue_AcceptsZeroToTwo(string value)
        {
            Assert.Equal(int.Parse(value), Parsed("--chime-type-value", value).ChimeTypeValue);
        }

        [Theory]
        [InlineData("3")]
        [InlineData("-1")]
        public void ChimeTypeValue_RejectsOutOfRange(string value)
        {
            Assert.Equal($"--chime-type-value value '{value}' must be 0, 1 or 2", ErrorOf("--chime-type-value", value));
        }

        [Theory]
        [InlineData("home")]
        [InlineData("away")]
        [InlineData("disarmed")]
        public void LocationModeValue_AcceptsTheThreeModes(string mode)
        {
            Assert.Equal(mode, Parsed("--location-mode-value", mode).LocationModeValue);
        }

        [Fact]
        public void LocationModeValue_RejectsOtherValues()
        {
            Assert.Equal("--location-mode-value value 'night' must be one of: home, away, disarmed",
                ErrorOf("--location-mode-value", "night"));
        }

        [Theory]
        [InlineData("--output-dir")]
        [InlineData("--endpoints")]
        [InlineData("--db-path")]
        [InlineData("--history-limit")]
        [InlineData("--location-id")]
        [InlineData("--app-api-base-url")]
        public void ValueOptionAtEnd_ReportsMissingValue(string flag)
        {
            Assert.Equal($"Missing value for {flag}", ErrorOf(flag));
        }

        [Fact]
        public void HostOverrideUrl_ParsesAbsoluteUrl()
        {
            Assert.Equal(new Uri("https://example.test/api"), Parsed("--app-api-base-url", "https://example.test/api").AppApiBaseUrl);
        }

        [Fact]
        public void HostOverrideUrl_RejectsRelativeUrl()
        {
            Assert.Equal("--app-api-base-url value '/api' is not a valid absolute URL", ErrorOf("--app-api-base-url", "/api"));
        }

        [Fact]
        public void UnknownDashedArgument_IsRejected()
        {
            Assert.Equal("Unrecognized argument: --nope", ErrorOf("--nope"));
        }

        [Fact]
        public void UnknownSingleDashArgument_IsRejected()
        {
            Assert.Equal("Unrecognized argument: -x", ErrorOf("-x"));
        }

        [Fact]
        public void BareWords_AreIgnored()
        {
            CliOptions options = Parsed("stray", "words");

            Assert.Equal(["all"], options.Endpoints);
        }
    }
}
