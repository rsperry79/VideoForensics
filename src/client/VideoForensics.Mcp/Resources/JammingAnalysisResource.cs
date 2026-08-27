using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace VideoForensics.Mcp.Resources
{
    /// <summary>
    /// Exposes the jamming/interference analysis playbook as an on-demand MCP resource, rather than
    /// baking it into every JammingTools tool description (which would bloat context on every call
    /// whether or not the caller is actually doing jamming analysis).
    /// </summary>
    [McpServerResourceType]
    public static class JammingAnalysisResource
    {
        private const string ResourceName = "VideoForensics.Mcp.Resources.jamming-analysis.md";

        [McpServerResource(UriTemplate = "videoforensics://instructions/jamming-analysis", Name = "jamming-analysis-instructions", MimeType = "text/markdown")]
        [Description("Playbook for detecting and interpreting RF jamming/signal interference from Ring device RSSI data: what the signature looks like, the recommended JammingTools call sequence, how to interpret confidence levels, and caveats for presenting findings in a DV-safety context. Fetch this before running or summarizing jamming analysis.")]
        public static string GetJammingAnalysisInstructions()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
