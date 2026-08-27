using System.ComponentModel;
using ModelContextProtocol.Server;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for browsing stored device events.</summary>
    [McpServerToolType]
    public static class EventTools
    {
        [McpServerTool, Description("Lists stored events (motion, ring, etc.) for a device within a date range.")]
        public static async Task<IReadOnlyList<Event>> BrowseEvents(
            IEventRepository eventRepository,
            [Description("Data-layer device Guid")] Guid deviceId,
            [Description("Start of the date range (UTC)")] DateTime fromUtc,
            [Description("End of the date range (UTC)")] DateTime toUtc,
            CancellationToken cancellationToken)
        {
            return await eventRepository.ListByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, cancellationToken);
        }
    }
}
