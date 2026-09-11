namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Represents an available Ring self-test endpoint.
    /// </summary>
    /// <param name="Key">Unique identifier for the endpoint.</param>
    /// <param name="DisplayName">User-friendly name for the endpoint.</param>
    /// <param name="Description">Brief description of what the endpoint tests.</param>
    /// <param name="SessionMethod">The session method required by the endpoint.</param>
    /// <param name="HttpMethod">The HTTP method used (GET, POST, etc.).</param>
    /// <param name="ApiPath">The API path for the endpoint.</param>
    /// <param name="Scope">The scope level: None, PerLocation, PerDoorbot, or PerChime.</param>
    /// <param name="Destructive">True if this endpoint modifies device state.</param>
    /// <param name="Physical">True if this endpoint has physical effects (siren, light, etc.).</param>
    public record SelfTestEndpointDto(
        string Key,
        string DisplayName,
        string Description,
        string SessionMethod,
        string HttpMethod,
        string ApiPath,
        string Scope,
        bool Destructive,
        bool Physical
    );

    /// <summary>
    /// Request DTO for starting a Ring self-test run.
    /// </summary>
    /// <param name="Endpoints">List of endpoint keys to test. Empty or containing "all" means all non-destructive endpoints.</param>
    /// <param name="LocationId">Optional location ID filter.</param>
    /// <param name="DoorbotId">Optional doorbot device ID filter.</param>
    /// <param name="ChimeId">Optional chime device ID filter.</param>
    /// <param name="HistoryLimit">Maximum number of events to retrieve per endpoint (default 5).</param>
    /// <param name="Destructive">Include destructive endpoints in the run.</param>
    /// <param name="NoPhysical">Exclude physical endpoints from the run.</param>
    /// <param name="SirenDurationSeconds">Siren duration in seconds for applicable endpoints.</param>
    /// <param name="VolumeLevel">Volume level (0-11) for speaker endpoints.</param>
    /// <param name="ChimeTypeValue">Chime type value for chime endpoints.</param>
    /// <param name="DndSeconds">Do Not Disturb duration in seconds.</param>
    /// <param name="LocationModeValue">Location mode value (e.g., "home", "away").</param>
    /// <param name="DingId">Ding/event ID for event-specific testing.</param>
    /// <param name="AssetUuid">Asset UUID for snapshot/media testing.</param>
    /// <param name="PushToken">Push notification token for testing push delivery.</param>
    public record SelfTestRunRequestDto(
        IReadOnlyList<string> Endpoints,
        Guid? LocationId = null,
        long? DoorbotId = null,
        long? ChimeId = null,
        int HistoryLimit = 5,
        bool Destructive = false,
        bool NoPhysical = false,
        int? SirenDurationSeconds = null,
        int? VolumeLevel = null,
        int? ChimeTypeValue = null,
        int? DndSeconds = null,
        string? LocationModeValue = null,
        string? DingId = null,
        string? AssetUuid = null,
        string? PushToken = null
    );

    /// <summary>
    /// Response DTO for starting a self-test run (fire-and-forget acknowledgement).
    /// </summary>
    /// <param name="Accepted">True if the run was accepted and queued; false if rejected (e.g., already running).</param>
    /// <param name="Error">Error message if Accepted is false, null otherwise.</param>
    public record SelfTestRunResponseDto(
        bool Accepted,
        string? Error = null
    );

    /// <summary>
    /// Enumeration of possible self-test run statuses.
    /// </summary>
    public enum SelfTestRunStatus
    {
        /// <summary>No run in progress.</summary>
        Idle = 0,
        /// <summary>A run is currently executing.</summary>
        Running = 1,
        /// <summary>A run has completed successfully.</summary>
        Completed = 2,
        /// <summary>A run failed.</summary>
        Failed = 3
    }

    /// <summary>
    /// Response DTO for polling the current self-test run status.
    /// </summary>
    /// <param name="Status">The current run status.</param>
    /// <param name="StartedAtUtc">Timestamp when the run started (null if idle).</param>
    /// <param name="CompletedAtUtc">Timestamp when the run completed (null if still running).</param>
    /// <param name="Error">Error message if Status is Failed, null otherwise.</param>
    public record SelfTestStatusDto(
        SelfTestRunStatus Status,
        DateTime? StartedAtUtc = null,
        DateTime? CompletedAtUtc = null,
        string? Error = null
    );

    /// <summary>
    /// Summary statistics for a completed self-test run.
    /// </summary>
    /// <param name="TotalCalls">Total number of endpoint calls made.</param>
    /// <param name="Succeeded">Number of calls that succeeded.</param>
    /// <param name="Failed">Number of calls that failed.</param>
    public record SelfTestSummaryDto(
        int TotalCalls,
        int Succeeded,
        int Failed
    );

    /// <summary>
    /// Details of a single endpoint call within a self-test run.
    /// </summary>
    /// <param name="Endpoint">The endpoint key that was called.</param>
    /// <param name="DisplayName">User-friendly name of the endpoint.</param>
    /// <param name="SessionMethod">The session method used.</param>
    /// <param name="Destructive">True if this endpoint modifies device state.</param>
    /// <param name="Physical">True if this endpoint has physical effects.</param>
    /// <param name="Target">The device or resource targeted (e.g., doorbot ID, location ID).</param>
    /// <param name="StartedAtUtc">Timestamp when the call started.</param>
    /// <param name="DurationMs">Duration of the call in milliseconds.</param>
    /// <param name="Success">True if the call succeeded.</param>
    /// <param name="Error">Error message if Success is false, null otherwise.</param>
    /// <param name="RestoreAttempted">True if a restore/cleanup action was attempted after a destructive call.</param>
    /// <param name="RestoreSuccess">True if the restore succeeded; false if it failed; null if not attempted.</param>
    /// <param name="RestoreError">Error message from the restore attempt, null if not attempted or successful.</param>
    /// <param name="RestoreSkippedReason">Reason why restore was skipped (e.g., "endpoint not destructive"), null if attempted.</param>
    /// <param name="SchemaIssues">List of schema validation issues detected during the call.</param>
    public record SelfTestCallDto(
        string Endpoint,
        string DisplayName,
        string SessionMethod,
        bool Destructive,
        bool Physical,
        string? Target,
        DateTime StartedAtUtc,
        long DurationMs,
        bool Success,
        string? Error,
        bool RestoreAttempted,
        bool? RestoreSuccess,
        string? RestoreError,
        string? RestoreSkippedReason,
        IReadOnlyList<string> SchemaIssues
    );

    /// <summary>
    /// Complete results from a finished self-test run.
    /// </summary>
    /// <param name="ToolVersion">Version of the self-test tool.</param>
    /// <param name="GeneratedAtUtc">Timestamp when the results were generated.</param>
    /// <param name="CredentialSource">Source of credentials used for the test (e.g., "Database").</param>
    /// <param name="Summary">Summary statistics for the run.</param>
    /// <param name="Calls">Detailed results for each endpoint call.</param>
    public record SelfTestResultDto(
        string ToolVersion,
        DateTime GeneratedAtUtc,
        string CredentialSource,
        SelfTestSummaryDto Summary,
        IReadOnlyList<SelfTestCallDto> Calls
    );

    /// <summary>
    /// Service for running Ring self-test smoke tests on devices and retrieving results.
    /// </summary>
    public interface IRingSelfTestService
    {
        /// <summary>
        /// Lists all available Ring self-test endpoints.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available endpoints for populating a UI checklist.</returns>
        Task<IReadOnlyList<SelfTestEndpointDto>> ListEndpointsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a self-test run (fire-and-forget on the server side).
        /// Returns immediately with whether the run was accepted. A run can be rejected if another
        /// run is already in progress (returning Accepted=false with an error message).
        /// </summary>
        /// <param name="request">Configuration for the run (endpoints, device filters, parameters).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response indicating whether the run was accepted and any error message.</returns>
        Task<SelfTestRunResponseDto> StartRunAsync(SelfTestRunRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls the current status of the running or most recent self-test run.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current run status (Idle, Running, Completed, or Failed).</returns>
        Task<SelfTestStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the completed result of the most recent self-test run.
        /// Returns null if no run has completed yet, or the status is not Completed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Completed run results, or null if unavailable.</returns>
        Task<SelfTestResultDto?> GetResultAsync(CancellationToken cancellationToken = default);
    }
}
