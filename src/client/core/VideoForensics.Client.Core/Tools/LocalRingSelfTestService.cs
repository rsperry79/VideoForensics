using System.Collections.ObjectModel;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Providers.Ring;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// Local (in-process) implementation of IRingSelfTestService that orchestrates Ring self-tests
    /// directly without HTTP transport. Used by server-tier hosts (WebApp, console, MCP) that have
    /// direct access to the Ring provider.
    /// </summary>
    public class LocalRingSelfTestService : IRingSelfTestService
    {
        private readonly IRingSelfTestOrchestrator _orchestrator;

        public LocalRingSelfTestService(IRingSelfTestOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public Task<IReadOnlyList<SelfTestEndpointDto>> ListEndpointsAsync(CancellationToken cancellationToken = default)
        {
            ReadOnlyCollection<SelfTestEndpointDto> endpoints = EndpointRegistry.All
                .Select(descriptor => new SelfTestEndpointDto(
                    Key: descriptor.Key,
                    DisplayName: descriptor.DisplayName,
                    Description: descriptor.Description,
                    SessionMethod: descriptor.SessionMethod,
                    HttpMethod: descriptor.HttpMethod,
                    ApiPath: descriptor.ApiPath,
                    Scope: descriptor.Scope.ToString(),
                    Destructive: descriptor.Destructive,
                    Physical: descriptor.Physical
                ))
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyList<SelfTestEndpointDto>>(endpoints);
        }

        public Task<SelfTestRunResponseDto> StartRunAsync(SelfTestRunRequestDto request, CancellationToken cancellationToken = default)
        {
            // Set ambient EndpointRegistry properties from request
            EndpointRegistry.CurrentHistoryLimit = request.HistoryLimit;

            if (request.SirenDurationSeconds.HasValue)
            {
                EndpointRegistry.SirenDurationSeconds = request.SirenDurationSeconds.Value;
            }

            if (request.VolumeLevel.HasValue)
            {
                EndpointRegistry.VolumeLevel = request.VolumeLevel.Value;
            }

            if (request.ChimeTypeValue.HasValue)
            {
                EndpointRegistry.ChimeTypeValue = request.ChimeTypeValue.Value;
            }

            if (request.DndSeconds.HasValue)
            {
                EndpointRegistry.DndSeconds = request.DndSeconds.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.LocationModeValue))
            {
                EndpointRegistry.LocationModeValue = request.LocationModeValue;
            }

            if (!string.IsNullOrWhiteSpace(request.DingId))
            {
                EndpointRegistry.DingId = request.DingId;
            }

            if (!string.IsNullOrWhiteSpace(request.AssetUuid))
            {
                EndpointRegistry.AssetUuid = request.AssetUuid;
            }

            if (!string.IsNullOrWhiteSpace(request.PushToken))
            {
                EndpointRegistry.PushToken = request.PushToken;
            }

            // Build RunOptions
            var options = new RunOptions(
                RequestedKeys: request.Endpoints ?? [],
                Destructive: request.Destructive,
                NoPhysical: request.NoPhysical,
                LocationIdFilter: request.LocationId,
                DoorbotIdFilter: request.DoorbotId,
                ChimeIdFilter: request.ChimeId
            );

            // Pick output directory under ProgramData, matching other persistent state in this app
            string outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics",
                "SelfTesterResults",
                DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
            _ = Directory.CreateDirectory(outputDir);

            // Try to start the run
            bool accepted = _orchestrator.TryStartRun(options, outputDir);

            SelfTestRunResponseDto response = accepted
                ? new SelfTestRunResponseDto(Accepted: true, Error: null)
                : new SelfTestRunResponseDto(Accepted: false, Error: "A self-test run is already in progress.");

            return Task.FromResult(response);
        }

        public Task<SelfTestStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            (SelfTestRunStatus status, DateTime? startedAtUtc, DateTime? completedAtUtc, string? error) = _orchestrator.GetStatus();

            var statusDto = new SelfTestStatusDto(
                Status: status,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: completedAtUtc,
                Error: error
            );

            return Task.FromResult(statusDto);
        }

        public Task<SelfTestResultDto?> GetResultAsync(CancellationToken cancellationToken = default)
        {
            IndexDocument? indexDoc = _orchestrator.GetResult();
            if (indexDoc == null)
            {
                return Task.FromResult<SelfTestResultDto?>(null);
            }

            ReadOnlyCollection<SelfTestCallDto> calls = indexDoc.Calls
                .Select(callRecord => new SelfTestCallDto(
                    Endpoint: callRecord.Endpoint,
                    DisplayName: callRecord.DisplayName,
                    SessionMethod: callRecord.SessionMethod,
                    Destructive: callRecord.Destructive,
                    Physical: callRecord.Physical,
                    Target: callRecord.Target != null
                        ? FormatTarget(callRecord.Target)
                        : null,
                    StartedAtUtc: callRecord.StartedAtUtc,
                    DurationMs: (long)callRecord.DurationMs,
                    Success: callRecord.Success,
                    Error: callRecord.Error,
                    RestoreAttempted: callRecord.RestoreAttempted,
                    RestoreSuccess: callRecord.RestoreSuccess,
                    RestoreError: callRecord.RestoreError,
                    RestoreSkippedReason: callRecord.RestoreSkippedReason,
                    SchemaIssues: callRecord.SchemaIssues
                        .Select(issue => $"{issue.IssueType} at {issue.Path}: {issue.Severity}")
                        .ToList()
                        .AsReadOnly()
                ))
                .ToList()
                .AsReadOnly();

            var summary = new SelfTestSummaryDto(
                TotalCalls: indexDoc.Summary.TotalCalls,
                Succeeded: indexDoc.Summary.Succeeded,
                Failed: indexDoc.Summary.Failed
            );

            var resultDto = new SelfTestResultDto(
                ToolVersion: indexDoc.ToolVersion,
                GeneratedAtUtc: indexDoc.GeneratedAtUtc,
                CredentialSource: indexDoc.CredentialSource,
                Summary: summary,
                Calls: calls
            );

            return Task.FromResult<SelfTestResultDto?>(resultDto);
        }

        /// <summary>
        /// Formats a TargetRecord as a readable string for display in the DTO.
        /// </summary>
        private static string FormatTarget(TargetRecord target)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(target.LocationName))
            {
                parts.Add($"Location: {target.LocationName}");
            }
            else if (!string.IsNullOrWhiteSpace(target.LocationId))
            {
                parts.Add($"Location: {target.LocationId}");
            }

            if (!string.IsNullOrWhiteSpace(target.DoorbotName))
            {
                parts.Add($"Doorbot: {target.DoorbotName}");
            }
            else if (target.DoorbotId.HasValue)
            {
                parts.Add($"Doorbot: {target.DoorbotId}");
            }

            if (!string.IsNullOrWhiteSpace(target.ChimeName))
            {
                parts.Add($"Chime: {target.ChimeName}");
            }
            else if (target.ChimeId.HasValue)
            {
                parts.Add($"Chime: {target.ChimeId}");
            }

            return string.Join("; ", parts);
        }
    }
}
