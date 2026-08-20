using System;
using System.Collections.Generic;

namespace Ring.Api.Utils
{
    /// <summary>
    /// One raw HTTP request/response captured via ApiRawLogger while a single endpoint call ran.
    /// Usually one per call, but a call is free to make more than one HTTP request (e.g. paging) -
    /// every request it made is listed here so nothing is hidden from the index.
    /// </summary>
    public sealed class HttpCallRecord
    {
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public int StatusCode { get; set; }
        public int ResponseBodyBytes { get; set; }
        /// <summary>Path to this specific raw response body, relative to index.json's own directory.</summary>
        public string? BodyFile { get; set; }
        /// <summary>"test" for the mutating call itself, "restore" for the follow-up call that put the setting back.</summary>
        public string Phase { get; set; } = "test";
    }

    /// <summary>
    /// The location/doorbot (if any) a call was scoped to, and the friendly name the tool
    /// resolved it to (from the devices/locations call), so the index is readable without
    /// cross-referencing ids by hand.
    /// </summary>
    public sealed class TargetRecord
    {
        public string? LocationId { get; set; }
        public string? LocationName { get; set; }
        public long? DoorbotId { get; set; }
        public string? DoorbotName { get; set; }
        public long? ChimeId { get; set; }
        public string? ChimeName { get; set; }
    }

    /// <summary>
    /// One endpoint invocation: what was called, against what target, what came back, and where
    /// the raw response body was written. This is the "sub-element" unit of index.json's "calls"
    /// array.
    /// </summary>
    public sealed class CallRecord
    {
        public required string Endpoint { get; set; }
        public required string DisplayName { get; set; }
        public required string SessionMethod { get; set; }
        public bool Destructive { get; set; }
        public bool Physical { get; set; }
        public TargetRecord? Target { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public double DurationMs { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        /// <summary>Path to the raw response body file, relative to index.json's own directory.</summary>
        public string? ResultFile { get; set; }
        public List<HttpCallRecord> HttpCalls { get; set; } = new();
        /// <summary>Human-readable snapshot of the original value(s) captured before this destructive call ran, if any.</summary>
        public string? OriginalValue { get; set; }
        public bool RestoreAttempted { get; set; }
        public bool? RestoreSuccess { get; set; }
        public string? RestoreError { get; set; }
        /// <summary>Set when no original value could be captured, so restore was skipped by design rather than attempted and failed.</summary>
        public string? RestoreSkippedReason { get; set; }
        /// <summary>Schema validation issues found when comparing the actual API response against the declared entity schema.</summary>
        public List<SchemaIssueRecord> SchemaIssues { get; set; } = new();
    }

    public sealed class SchemaIssueRecord
    {
        public string Path { get; set; } = "";
        public string IssueType { get; set; } = ""; // "TypeMismatch", "MissingInSchema", "UnusedInSchema", "NullabilityMismatch"
        public string? Expected { get; set; }
        public string? Actual { get; set; }
        public string Severity { get; set; } = ""; // "Error", "Warning", "Info"
    }

    /// <summary>
    /// Root of index.json - everything an AI agent needs to walk the results of a run without
    /// re-deriving anything from the raw files: what ran, against what account, where each raw
    /// response landed, and a pass/fail summary to check first.
    /// </summary>
    public sealed class IndexDocument
    {
        public string ToolVersion { get; set; } = "1.0";
        public DateTime GeneratedAtUtc { get; set; }
        public string CredentialSource { get; set; } = "";
        public string OutputDirectory { get; set; } = "";
        public SummaryRecord Summary { get; set; } = new();
        public List<CallRecord> Calls { get; set; } = new();
    }

    public sealed class SummaryRecord
    {
        public int TotalCalls { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
    }
}
