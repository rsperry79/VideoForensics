namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Configuration for server-side scheduled provider tasks (bound from the "ScheduledTasks" section).
    /// Only the server tier (VideoForensics.Client.WebApp) ever enables this; client hosts never talk to providers.
    /// </summary>
    public sealed class ScheduledTasksOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "ScheduledTasks";

        /// <summary>Provider names ("Ring", "Uniview", "Wyze") whose accounts are synced on a schedule.
        /// Empty (the default) disables scheduled sync entirely: nothing is registered.</summary>
        public string[] EnabledProviders { get; set; } = [];

        /// <summary>
        /// Whether an account's first sync runs, including the monthly backfill that stands in for it. Off by default: while it is
        /// off, accounts get only the normal incremental pulls, so the first sync does not add provider calls.
        /// </summary>
        public bool InitialSyncEnabled { get; set; } = false;

        /// <summary>Seconds between scheduler passes over the enabled sync schedules (default 60, floor 5).</summary>
        public int TickSeconds { get; set; } = 60;
    }
}