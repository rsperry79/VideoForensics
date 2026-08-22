using System;
using System.Collections.Generic;

namespace VideoForensics.Providers.Ring.Forensics.Models
{
    public class TimeSyncValidation
    {
        public bool IsTimeSynchronized { get; set; }
        public TimeSpan? MaxClockDrift { get; set; }
        public DateTime? LastKnownCorrectTime { get; set; }
        public List<string> TimeSuspiciousEvents { get; set; } = new();
        public string? Recommendation { get; set; }
        public string? SyncProvider { get; set; }
        public DateTime? LastSyncedAt { get; set; }
    }
}
