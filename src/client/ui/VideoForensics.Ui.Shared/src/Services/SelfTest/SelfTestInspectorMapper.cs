using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
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
        string title = $"{call.DisplayName} · {(call.Success ? "OK" : "FAILED")}";

        // Fields: call summary with metadata (without bodies)
        var fields = new
        {
            call.Endpoint,
            call.Target,
            call.DurationMs,
            call.Success,
            call.Error,
            call.SessionMethod,
            call.Destructive,
            call.Physical,
            RestoreInfo = new
            {
                call.RestoreAttempted,
                call.RestoreSuccess,
                call.RestoreError,
                call.RestoreSkippedReason
            },
            SchemaIssuesCount = call.SchemaIssues.Count,
            HttpCallsCount = call.HttpCalls.Count
        };

        // RawJson: body of first test-phase HTTP call
        string? rawJson = call.HttpCalls
            .FirstOrDefault(h => h.Phase == "test")
            ?.Body;

        // Provenance: one entry per HTTP call (Phase Method StatusCode + URL + truncation info)
        var provenance = new List<KeyValuePair<string, string>>();
        foreach (SelfTestHttpCallDto httpCall in call.HttpCalls)
        {
            string key = $"{httpCall.Phase} {httpCall.Method} {httpCall.StatusCode}";
            string value = httpCall.Url;
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
            Related: [],
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
            _ = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
