namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Phases of server maintenance, as sent to clients.</summary>
    public static class MaintenancePhases
    {
        /// <summary>Normal operation. Background and user-started work may run.</summary>
        public const string Idle = "Idle";

        /// <summary>New work is blocked. In-flight work is finishing before maintenance starts.</summary>
        public const string Draining = "Draining";

        /// <summary>Maintenance is running. New work stays blocked until it ends and the paused work resumes.</summary>
        public const string Running = "Running";
    }

    /// <summary>Current server maintenance state, pushed to every client on change and on connect.</summary>
    /// <param name="Phase">One of <see cref="MaintenancePhases"/>.</param>
    /// <param name="Message">What the server is doing, shown to users in the banner.</param>
    /// <param name="StartedUtc">When the current maintenance began, or null when idle.</param>
    public record MaintenanceStatusDto(string Phase, string Message, DateTime? StartedUtc)
    {
        /// <summary>The state when no maintenance is running.</summary>
        public static MaintenanceStatusDto Normal { get; } = new(MaintenancePhases.Idle, string.Empty, null);
    }
}
