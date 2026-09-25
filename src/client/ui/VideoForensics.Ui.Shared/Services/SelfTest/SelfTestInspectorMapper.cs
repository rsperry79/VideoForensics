using System.Text.Json;
using VideoForensics.Api.Contracts;
using VideoForensics.Ui.Shared.Services.Inspector;

namespace VideoForensics.Ui.Shared.Services.SelfTest;

/// <summary>
/// Maps SelfTestCallDto to InspectorModel for display in the inspector panel.
/// </summary>
public static class SelfTestInspectorMapper
{
    /// <summary>
    /// Maps a self-test call result to an inspector model for display.
    /// </summary>
    public static InspectorModel ToInspector(SelfTestCallDto call)
    {
        // Title: endpoint name and pass/fail status
        var title = $"{call.DisplayName} · {(call.Success ? "OK" : "FAILED")}";

        // Fields: call summary with metadata (without bodies)
        var fields = new
        {
            call.Endpoint,
            call.Target,
            DurationMs = call.DurationMs,
            Success = call.Success,
            Error = call.Error,
            SessionMethod = call.SessionMethod,
            Destructive = call.Destructive,
            Physical = call.Physical,
            RestoreInfo = new
            {
                RestoreAttempted = call.RestoreAttempted,
                RestoreSuccess = call.RestoreSuccess,
                RestoreError = call.RestoreError,
                RestoreSkippedReason = call.RestoreSkippedReason
            },
            SchemaIssuesCount = call.SchemaIssues.Count,
            HttpCallsCount = call.HttpCalls.Count
        };

        // RawJson: body of first test-phase HTTP call
        var rawJson = call.HttpCalls
            .FirstOrDefault(h => h.Phase == "test")
            ?.Body;

        // Provenance: one entry per HTTP call (Phase Method StatusCode + URL + truncation info)
        var provenance = new List<KeyValuePair<string, string>>();
        foreach (var httpCall in call.HttpCalls)
        {
            var key = $"{httpCall.Phase} {httpCall.Method} {httpCall.StatusCode}";
            var value = httpCall.Url;
            if (httpCall.BodyTruncated)
            {
                value += $" (truncated, {httpCall.ResponseBodyBytes} bytes)";
            }
            provenance.Add(new KeyValuePair<string, string>(key, value));
        }

        return new InspectorModel(
            Title: title,
            Fields: fields,
            RawJson: rawJson,
            Related: new List<InspectorLink>(),
            Provenance: provenance,
            Pin: null);
    }

    /// <summary>
    /// Detects whether a string is valid JSON.
    /// </summary>
    public static bool IsValidJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
