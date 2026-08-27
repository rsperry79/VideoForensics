using System.ComponentModel;
using ModelContextProtocol.Server;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for browsing device inventory and configuration snapshots.</summary>
    [McpServerToolType]
    public static class DeviceTools
    {
        [McpServerTool, Description("Lists all known devices (cameras/doorbells) in the local evidence database.")]
        public static async Task<IReadOnlyList<Device>> ListDevices(
            IDeviceRepository deviceRepository,
            CancellationToken cancellationToken)
        {
            return await deviceRepository.ListAsync(cancellationToken);
        }

        [McpServerTool, Description("Returns the latest stored configuration snapshot (motion detection, sensitivity, recording mode) for a device.")]
        public static async Task<DeviceConfigSnapshot?> GetDeviceConfiguration(
            IDeviceConfigRepository deviceConfigRepository,
            [Description("Data-layer device Guid")] Guid deviceId,
            CancellationToken cancellationToken)
        {
            return await deviceConfigRepository.GetLatestAsync(deviceId, cancellationToken);
        }
    }
}
