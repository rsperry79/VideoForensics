using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Diagnostics.Contracts
{
    /// <summary>Provides read-only database health check queries for detecting redundancy, orphaned records, and table statistics.</summary>
    public interface IDatabaseHealthChecker
    {
        /// <summary>Finds duplicate device entries sharing the same LocationId and ProviderDeviceId.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of duplicate groups, each containing record IDs for potential remediation.</returns>
        Task<IReadOnlyList<DuplicateGroup>> FindDuplicateDevicesAsync(CancellationToken ct);

        /// <summary>Finds duplicate event entries sharing the same DeviceId and ProviderEventId.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of duplicate groups, each containing record IDs for potential remediation.</returns>
        Task<IReadOnlyList<DuplicateGroup>> FindDuplicateEventsAsync(CancellationToken ct);

        /// <summary>Gets detection redundancy statistics across MediaItemDetection and EventDetection tables.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Redundancy summary with row counts and average detections per item/event.</returns>
        Task<DetectionRedundancySummary> GetDetectionRedundancySummaryAsync(CancellationToken ct);

        /// <summary>Gets device health table statistics and recent date activity.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Health summary with row count and up to 5 most recent dates with counts.</returns>
        Task<DeviceHealthSummary> GetDeviceHealthSummaryAsync(CancellationToken ct);

        /// <summary>Gets device feature and capabilities overlap statistics.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Overlap summary with device IDs for devices having both tables.</returns>
        Task<DeviceFeatureOverlapSummary> GetDeviceFeatureOverlapAsync(CancellationToken ct);

        /// <summary>Finds all orphaned records across Events, MediaItems, Detections, Devices, and DownloadEvents.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Orphaned record summary with counts and IDs for each orphaned entity type.</returns>
        Task<OrphanedRecordSummary> FindOrphanedRecordsAsync(CancellationToken ct);

        /// <summary>Gets row counts for major tables in the database.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of table size entries with row counts.</returns>
        Task<IReadOnlyList<TableSizeEntry>> GetTableSizesAsync(CancellationToken ct);
    }

    /// <summary>Represents a group of duplicate records sharing the same business key.</summary>
    /// <param name="LocationId">The location ID (for device duplicates) or null (for event duplicates).</param>
    /// <param name="DeviceId">The device ID (for event duplicates) or null (for device duplicates).</param>
    /// <param name="ProviderKey">The provider-specific key (ProviderDeviceId or ProviderEventId).</param>
    /// <param name="Count">The number of duplicate records in this group.</param>
    /// <param name="RecordIds">The IDs of all duplicate records in this group.</param>
    public record DuplicateGroup(
        Guid? LocationId,
        Guid? DeviceId,
        string ProviderKey,
        int Count,
        IReadOnlyList<Guid> RecordIds);

    /// <summary>Redundancy statistics for detection data across MediaItems and Events.</summary>
    /// <param name="MediaItemDetectionCount">Total rows in MediaItemDetection table.</param>
    /// <param name="EventDetectionCount">Total rows in EventDetection table.</param>
    /// <param name="AvgDetectionsPerMediaItem">Average detections per media item, or null if no media items exist.</param>
    /// <param name="AvgDetectionsPerEvent">Average detections per event, or null if no events exist.</param>
    public record DetectionRedundancySummary(
        int MediaItemDetectionCount,
        int EventDetectionCount,
        double? AvgDetectionsPerMediaItem,
        double? AvgDetectionsPerEvent);

    /// <summary>Device health table statistics and recent activity breakdown.</summary>
    /// <param name="DeviceHealthRowCount">Total rows in DeviceHealth table.</param>
    /// <param name="RecentDates">Up to 5 most recent dates and their record counts.</param>
    public record DeviceHealthSummary(
        int DeviceHealthRowCount,
        IReadOnlyList<DateHealthCount> RecentDates);

    /// <summary>Date and record count for device health activity.</summary>
    /// <param name="Date">The date (captured at UTC date component).</param>
    /// <param name="Count">The number of DeviceHealth records on this date.</param>
    public record DateHealthCount(DateTime Date, int Count);

    /// <summary>Device feature and capabilities overlap statistics.</summary>
    /// <param name="DeviceCapabilitiesCount">Total rows in DeviceCapabilities table.</param>
    /// <param name="DeviceFeaturesCount">Total rows in DeviceFeatures table.</param>
    /// <param name="DevicesWithBoth">Count of unique devices having both capabilities and features.</param>
    /// <param name="DeviceIdsWithBoth">IDs of devices having both capabilities and features.</param>
    /// <param name="CapabilitiesOnlyCount">Count of unique devices with only DeviceCapabilities.</param>
    /// <param name="FeaturesOnlyCount">Count of unique devices with only DeviceFeatures.</param>
    public record DeviceFeatureOverlapSummary(
        int DeviceCapabilitiesCount,
        int DeviceFeaturesCount,
        int DevicesWithBoth,
        IReadOnlyList<Guid> DeviceIdsWithBoth,
        int CapabilitiesOnlyCount,
        int FeaturesOnlyCount);

    /// <summary>Orphaned record summary across all major foreign key relationships.</summary>
    /// <param name="OrphanedEventCount">Events with deleted devices.</param>
    /// <param name="OrphanedEventIds">IDs of orphaned events.</param>
    /// <param name="OrphanedMediaItemCount">MediaItems with deleted devices.</param>
    /// <param name="OrphanedMediaItemIds">IDs of orphaned media items.</param>
    /// <param name="OrphanedMediaItemDetectionCount">MediaItemDetections with deleted media items.</param>
    /// <param name="OrphanedMediaItemDetectionIds">IDs of orphaned media item detections.</param>
    /// <param name="OrphanedEventDetectionCount">EventDetections with deleted events.</param>
    /// <param name="OrphanedEventDetectionIds">IDs of orphaned event detections.</param>
    /// <param name="OrphanedDeviceCount">Devices with deleted locations.</param>
    /// <param name="OrphanedDeviceIds">IDs of orphaned devices.</param>
    /// <param name="OrphanedDownloadEventCount">DownloadEvents with deleted devices.</param>
    /// <param name="OrphanedDownloadEventIds">IDs of orphaned download events.</param>
    public record OrphanedRecordSummary(
        int OrphanedEventCount,
        IReadOnlyList<Guid> OrphanedEventIds,
        int OrphanedMediaItemCount,
        IReadOnlyList<Guid> OrphanedMediaItemIds,
        int OrphanedMediaItemDetectionCount,
        IReadOnlyList<Guid> OrphanedMediaItemDetectionIds,
        int OrphanedEventDetectionCount,
        IReadOnlyList<Guid> OrphanedEventDetectionIds,
        int OrphanedDeviceCount,
        IReadOnlyList<Guid> OrphanedDeviceIds,
        int OrphanedDownloadEventCount,
        IReadOnlyList<Guid> OrphanedDownloadEventIds);

    /// <summary>Row count for a single database table.</summary>
    /// <param name="TableName">The table name.</param>
    /// <param name="RowCount">The number of rows in the table.</param>
    public record TableSizeEntry(string TableName, int RowCount);
}
