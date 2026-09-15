using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Providers.Ring;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class LocalRingSelfTestServiceTests
    {
        private readonly Mock<IRingSelfTestOrchestrator> _orchestratorMock;
        private readonly LocalRingSelfTestService _service;

        public LocalRingSelfTestServiceTests()
        {
            _orchestratorMock = new Mock<IRingSelfTestOrchestrator>();
            _service = new LocalRingSelfTestService(_orchestratorMock.Object);
        }

        [Fact]
        public async Task ListEndpointsAsync_ReturnsEndpointRegistry()
        {
            IReadOnlyList<SelfTestEndpointDto> result = await _service.ListEndpointsAsync();

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.All(result, endpoint =>
            {
                Assert.NotNull(endpoint.Key);
                Assert.NotNull(endpoint.DisplayName);
                Assert.NotNull(endpoint.Description);
                Assert.NotNull(endpoint.SessionMethod);
                Assert.NotNull(endpoint.HttpMethod);
                Assert.NotNull(endpoint.ApiPath);
                Assert.NotNull(endpoint.Scope);
            });
        }

        [Fact]
        public async Task ListEndpointsAsync_WithCancellationToken_ReturnsEndpoints()
        {
            CancellationToken cancellationToken = CancellationToken.None;

            IReadOnlyList<SelfTestEndpointDto> result = await _service.ListEndpointsAsync(cancellationToken);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task ListEndpointsAsync_EndpointDataMappedCorrectly()
        {
            IReadOnlyList<SelfTestEndpointDto> result = await _service.ListEndpointsAsync();

            SelfTestEndpointDto? devicesEndpoint = result.FirstOrDefault(e => e.Key == "devices");
            Assert.NotNull(devicesEndpoint);
            Assert.Equal("List devices", devicesEndpoint.DisplayName);
            Assert.NotNull(devicesEndpoint.Description);
            Assert.False(string.IsNullOrEmpty(devicesEndpoint.SessionMethod));
            Assert.False(string.IsNullOrEmpty(devicesEndpoint.HttpMethod));
            Assert.False(string.IsNullOrEmpty(devicesEndpoint.ApiPath));
        }

        [Fact]
        public async Task StartRunAsync_AcceptsRun_ReturnsSuccessResponse()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            SelfTestRunResponseDto result = await _service.StartRunAsync(request);

            Assert.NotNull(result);
            Assert.True(result.Accepted);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task StartRunAsync_RejectsSecondConcurrentRun_ReturnsErrorResponse()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(false);

            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            SelfTestRunResponseDto result = await _service.StartRunAsync(request);

            Assert.NotNull(result);
            Assert.False(result.Accepted);
            Assert.NotNull(result.Error);
            Assert.Contains("already in progress", result.Error);
        }

        [Fact]
        public async Task StartRunAsync_WithCancellationToken_ReturnsResponse()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            SelfTestRunResponseDto result = await _service.StartRunAsync(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.Accepted);
        }

        [Fact]
        public async Task StartRunAsync_SetsHistoryLimit()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 500,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            _ = await _service.StartRunAsync(request);

            Assert.Equal(500, EndpointRegistry.CurrentHistoryLimit);
        }

        [Fact]
        public async Task StartRunAsync_SetsSirenDuration_WhenProvided()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            int originalValue = EndpointRegistry.SirenDurationSeconds;
            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: 30,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            _ = await _service.StartRunAsync(request);

            Assert.Equal(30, EndpointRegistry.SirenDurationSeconds);
        }

        [Fact]
        public async Task StartRunAsync_SkipsSirenDuration_WhenNull()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            int originalValue = EndpointRegistry.SirenDurationSeconds;
            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            _ = await _service.StartRunAsync(request);

            // Should still have original value (not set)
            Assert.Equal(originalValue, EndpointRegistry.SirenDurationSeconds);
        }

        [Fact]
        public async Task StartRunAsync_SkipsOptionalStrings_WhenNullOrWhitespace()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: false,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            SelfTestRunResponseDto result = await _service.StartRunAsync(request);

            Assert.True(result.Accepted);
        }

        [Fact]
        public async Task StartRunAsync_PassesDestructiveFlag()
        {
            _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                .Returns(true);

            var request = new SelfTestRunRequestDto(
                Endpoints: new[] { "devices" },
                Destructive: true,
                NoPhysical: false,
                LocationId: null,
                DoorbotId: null,
                ChimeId: null,
                HistoryLimit: 1000,
                SirenDurationSeconds: null,
                VolumeLevel: null,
                ChimeTypeValue: null,
                DndSeconds: null,
                LocationModeValue: null,
                DingId: null,
                AssetUuid: null,
                PushToken: null);

            _ = await _service.StartRunAsync(request);

            _orchestratorMock.Verify(
                o => o.TryStartRun(
                    It.Is<RunOptions>(opts => opts.Destructive == true),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task StartRunAsync_CreatesOutputDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"videoforensics-test-{Guid.NewGuid():N}");
            try
            {
                _ = _orchestratorMock.Setup(o => o.TryStartRun(It.IsAny<RunOptions>(), It.IsAny<string>()))
                    .Callback<RunOptions, string>((_, outputDir) =>
                    {
                        // Verify that directory exists during the callback
                        Assert.True(Directory.Exists(outputDir));
                    })
                    .Returns(true);

                var request = new SelfTestRunRequestDto(
                    Endpoints: new[] { "devices" },
                    Destructive: false,
                    NoPhysical: false,
                    LocationId: null,
                    DoorbotId: null,
                    ChimeId: null,
                    HistoryLimit: 1000,
                    SirenDurationSeconds: null,
                    VolumeLevel: null,
                    ChimeTypeValue: null,
                    DndSeconds: null,
                    LocationModeValue: null,
                    DingId: null,
                    AssetUuid: null,
                    PushToken: null);

                SelfTestRunResponseDto result = await _service.StartRunAsync(request);

                Assert.True(result.Accepted);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        [Fact]
        public async Task GetStatusAsync_ReturnsOrchestratorStatus()
        {
            DateTime startedAt = DateTime.UtcNow.AddSeconds(-30);
            _ = _orchestratorMock.Setup(o => o.GetStatus())
                .Returns((SelfTestRunStatus.Running, startedAt, null, null));

            SelfTestStatusDto result = await _service.GetStatusAsync();

            Assert.NotNull(result);
            Assert.Equal(SelfTestRunStatus.Running, result.Status);
            Assert.Equal(startedAt, result.StartedAtUtc);
            Assert.Null(result.CompletedAtUtc);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task GetStatusAsync_WithCancellationToken_ReturnsStatus()
        {
            _ = _orchestratorMock.Setup(o => o.GetStatus())
                .Returns((SelfTestRunStatus.Idle, null, null, null));

            SelfTestStatusDto result = await _service.GetStatusAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(SelfTestRunStatus.Idle, result.Status);
        }

        [Fact]
        public async Task GetStatusAsync_IdleState_ReturnsNoTimestamps()
        {
            _ = _orchestratorMock.Setup(o => o.GetStatus())
                .Returns((SelfTestRunStatus.Idle, null, null, null));

            SelfTestStatusDto result = await _service.GetStatusAsync();

            Assert.Equal(SelfTestRunStatus.Idle, result.Status);
            Assert.Null(result.StartedAtUtc);
            Assert.Null(result.CompletedAtUtc);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task GetStatusAsync_ErrorState_ReturnsError()
        {
            string errorMsg = "Connection failed";
            _ = _orchestratorMock.Setup(o => o.GetStatus())
                .Returns((SelfTestRunStatus.Failed, null, null, errorMsg));

            SelfTestStatusDto result = await _service.GetStatusAsync();

            Assert.Equal(SelfTestRunStatus.Failed, result.Status);
            Assert.Equal(errorMsg, result.Error);
        }

        [Fact]
        public async Task GetResultAsync_NoResultYet_ReturnsNull()
        {
            _ = _orchestratorMock.Setup(o => o.GetResult())
                .Returns((IndexDocument?)null);

            SelfTestResultDto? result = await _service.GetResultAsync();

            Assert.Null(result);
        }

        [Fact]
        public async Task GetResultAsync_WithCancellationToken_ReturnsResult()
        {
            var indexDoc = new IndexDocument
            {
                ToolVersion = "1.0.0",
                GeneratedAtUtc = DateTime.UtcNow,
                CredentialSource = "OAuth",
                Summary = new SummaryRecord { TotalCalls = 0, Succeeded = 0, Failed = 0 },
                Calls = []
            };
            _ = _orchestratorMock.Setup(o => o.GetResult())
                .Returns(indexDoc);

            SelfTestResultDto? result = await _service.GetResultAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("1.0.0", result.ToolVersion);
        }

        [Fact]
        public async Task GetResultAsync_MapsIndexDocumentToDto()
        {
            DateTime startTime = DateTime.UtcNow.AddMinutes(-5);
            var callRecord = new CallRecord
            {
                Endpoint = "devices",
                DisplayName = "List devices",
                SessionMethod = "GetRingDevices",
                Destructive = false,
                Physical = false,
                Target = null,
                StartedAtUtc = startTime,
                DurationMs = 1500.5,
                Success = true,
                Error = null,
                RestoreAttempted = false,
                RestoreSuccess = false,
                RestoreError = null,
                RestoreSkippedReason = null,
                SchemaIssues = []
            };

            var indexDoc = new IndexDocument
            {
                ToolVersion = "1.0.0",
                GeneratedAtUtc = DateTime.UtcNow,
                CredentialSource = "OAuth",
                Summary = new SummaryRecord { TotalCalls = 1, Succeeded = 1, Failed = 0 },
                Calls = [callRecord]
            };
            _ = _orchestratorMock.Setup(o => o.GetResult())
                .Returns(indexDoc);

            SelfTestResultDto? result = await _service.GetResultAsync();

            Assert.NotNull(result);
            Assert.Equal("1.0.0", result.ToolVersion);
            Assert.Equal(1, result.Summary.TotalCalls);
            _ = Assert.Single(result.Calls);
            Assert.Equal("devices", result.Calls[0].Endpoint);
            Assert.True(result.Calls[0].Success);
            Assert.Equal(1500, result.Calls[0].DurationMs); // Truncated to long
        }

        [Fact]
        public async Task GetResultAsync_WithTarget_FormatsTarget()
        {
            var target = new TargetRecord
            {
                LocationId = Guid.NewGuid().ToString(),
                LocationName = "Front Door",
                DoorbotId = 12345,
                DoorbotName = "Front Camera",
                ChimeId = 67890,
                ChimeName = "Indoor Chime"
            };

            var callRecord = new CallRecord
            {
                Endpoint = "set-light",
                DisplayName = "Set light",
                SessionMethod = "SetLight",
                Destructive = true,
                Physical = true,
                Target = target,
                StartedAtUtc = DateTime.UtcNow,
                DurationMs = 500,
                Success = true,
                Error = null,
                RestoreAttempted = true,
                RestoreSuccess = true,
                RestoreError = null,
                RestoreSkippedReason = null,
                SchemaIssues = []
            };

            var indexDoc = new IndexDocument
            {
                ToolVersion = "1.0.0",
                GeneratedAtUtc = DateTime.UtcNow,
                CredentialSource = "OAuth",
                Summary = new SummaryRecord { TotalCalls = 1, Succeeded = 1, Failed = 0 },
                Calls = [callRecord]
            };
            _ = _orchestratorMock.Setup(o => o.GetResult())
                .Returns(indexDoc);

            SelfTestResultDto? result = await _service.GetResultAsync();

            Assert.NotNull(result);
            Assert.NotNull(result.Calls[0].Target);
            Assert.Contains("Location: Front Door", result.Calls[0].Target);
            Assert.Contains("Doorbot: Front Camera", result.Calls[0].Target);
            Assert.Contains("Chime: Indoor Chime", result.Calls[0].Target);
        }

        [Fact]
        public async Task GetResultAsync_WithSchemaIssues_IncludesIssuesInDto()
        {
            var schemaIssue = new SchemaIssueRecord
            {
                IssueType = "MissingField",
                Path = "$.data.devices[0]",
                Severity = "Warning"
            };

            var callRecord = new CallRecord
            {
                Endpoint = "devices",
                DisplayName = "List devices",
                SessionMethod = "GetRingDevices",
                Destructive = false,
                Physical = false,
                Target = null,
                StartedAtUtc = DateTime.UtcNow,
                DurationMs = 1000,
                Success = true,
                Error = null,
                RestoreAttempted = false,
                RestoreSuccess = false,
                RestoreError = null,
                RestoreSkippedReason = null,
                SchemaIssues = [schemaIssue]
            };

            var indexDoc = new IndexDocument
            {
                ToolVersion = "1.0.0",
                GeneratedAtUtc = DateTime.UtcNow,
                CredentialSource = "OAuth",
                Summary = new SummaryRecord { TotalCalls = 1, Succeeded = 1, Failed = 0 },
                Calls = [callRecord]
            };
            _ = _orchestratorMock.Setup(o => o.GetResult())
                .Returns(indexDoc);

            SelfTestResultDto? result = await _service.GetResultAsync();

            Assert.NotNull(result);
            _ = Assert.Single(result.Calls[0].SchemaIssues);
            Assert.Contains("MissingField", result.Calls[0].SchemaIssues[0]);
            Assert.Contains("Warning", result.Calls[0].SchemaIssues[0]);
        }
    }
}
