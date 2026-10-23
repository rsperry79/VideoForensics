namespace VideoForensics.Ui.Shared.Formatting;

/// <summary>
/// Formats chat tool invocation metadata for display in the UI.
/// </summary>
public static class ChatToolsFormatter
{
    /// <summary>
    /// Formats a list of invoked tool names into a human-readable transparency note.
    /// </summary>
    /// <param name="toolsInvoked">List of tool names that were invoked.</param>
    /// <returns>A formatted string like "Checked: Tool1, Tool2" or empty string if none.</returns>
    public static string FormatToolsInvoked(IReadOnlyList<string> toolsInvoked)
    {
        if (toolsInvoked == null || toolsInvoked.Count == 0)
        {
            return string.Empty;
        }

        var formatted = string.Join(", ", toolsInvoked.Select(t => FormatToolName(t)));
        return $"Checked: {formatted}";
    }

    /// <summary>
    /// Formats a single tool name to be more readable (e.g., converts CamelCase to Title Case).
    /// </summary>
    private static string FormatToolName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return toolName ?? string.Empty;
        }

        // Convert CamelCase to Title Case with spaces
        var result = System.Text.RegularExpressions.Regex.Replace(
            toolName,
            "([a-z])([A-Z])",
            "$1 $2");

        // Convert underscores to spaces
        result = result.Replace("_", " ");

        return result;
    }
}
