using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>
    /// Model for the video search, location event, location history, doorbot history and app location history feeds.
    /// Nested event objects (device, properties, placement, geolocation, receipt, last location, siren, recording and
    /// computer vision) are keyed by <see cref="RingEventSources"/> and the id of their event row, not by a foreign key,
    /// because one table serves all four event feeds. Every other child is a real foreign key with cascade delete.
    /// </summary>
    public static class RingEventHistoryModel
    {
        public static void Configure(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            ConfigureRootRows(modelBuilder);
            ConfigureNestedEventRows(modelBuilder);
            ConfigureComputerVision(modelBuilder);
            ConfigureLocationHistory(modelBuilder);
            ConfigureAppLocationHistory(modelBuilder);
        }

        private static void ConfigureRootRows(ModelBuilder b)
        {
            _ = b.Entity<RingVideoSearchEvent>().HasIndex(e => new { e.RingAccountId, e.ProviderEventId }).IsUnique();
            _ = b.Entity<RingLocationEventPage>().HasIndex(p => p.ProviderLocationId).IsUnique();
            _ = b.Entity<RingLocationEvent>().HasIndex(e => new { e.ProviderLocationId, e.ProviderEventId }).IsUnique();
            _ = b.Entity<RingLocationHistoryPage>().HasIndex(p => p.ProviderLocationId).IsUnique();
            _ = b.Entity<RingLocationHistoryEvent>().HasIndex(e => new { e.ProviderLocationId, e.ProviderEventId }).IsUnique();
            _ = b.Entity<RingDoorbotHistoryEvent>().HasIndex(e => e.ProviderEventId).IsUnique();
        }

        private static void ConfigureNestedEventRows(ModelBuilder b)
        {
            _ = b.Entity<RingEventDoorbot>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventProperties>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventDevicePlacement>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventGeolocation>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventSubscriptionReceipt>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventLastLocation>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventSiren>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventRecording>().HasIndex(x => new { x.Source, x.SourceRowId });
            _ = b.Entity<RingEventCvProperties>().HasIndex(x => new { x.Source, x.SourceRowId });
        }

        private static void ConfigureComputerVision(ModelBuilder b)
        {
            _ = b.Entity<RingEventCvDetectionType>()
                .HasOne<RingEventCvProperties>().WithMany(c => c.DetectionTypes)
                .HasForeignKey(t => t.RingEventCvPropertiesId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvVerifiedTimestamp>()
                .HasOne<RingEventCvDetectionType>().WithMany(t => t.VerifiedTimestamps)
                .HasForeignKey(v => v.RingEventCvDetectionTypeId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvSecurityAlert>()
                .HasOne<RingEventCvProperties>().WithMany(c => c.SecurityAlerts)
                .HasForeignKey(a => a.RingEventCvPropertiesId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvSecurityAlertItem>()
                .HasOne<RingEventCvSecurityAlert>().WithMany(a => a.Alerts)
                .HasForeignKey(i => i.RingEventCvSecurityAlertId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvProfile>()
                .HasOne<RingEventCvProperties>().WithMany(c => c.Profiles)
                .HasForeignKey(p => p.RingEventCvPropertiesId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvDetectionDetail>()
                .HasOne<RingEventCvProperties>().WithMany(c => c.DetectionDetails)
                .HasForeignKey(d => d.RingEventCvPropertiesId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvDetectionZone>()
                .HasOne<RingEventCvDetectionDetail>().WithMany(d => d.Zones)
                .HasForeignKey(z => z.RingEventCvDetectionDetailId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingEventCvTag>()
                .HasOne<RingEventCvProperties>().WithMany(c => c.Tags)
                .HasForeignKey(t => t.RingEventCvPropertiesId).OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureLocationHistory(ModelBuilder b)
        {
            _ = b.Entity<RingLocationHistoryVisualization>()
                .HasOne<RingLocationHistoryEvent>().WithMany(e => e.Visualizations)
                .HasForeignKey(v => v.RingLocationHistoryEventId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingLocationHistoryMedia>()
                .HasOne<RingLocationHistoryVisualization>().WithMany(v => v.Media)
                .HasForeignKey(m => m.RingLocationHistoryVisualizationId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingLocationHistoryMedia>()
                .HasOne(m => m.Metadata).WithOne()
                .HasForeignKey<RingLocationHistoryMediaMetadata>(m => m.RingLocationHistoryMediaId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingLocationHistoryMediaTrack>()
                .HasOne<RingLocationHistoryMediaMetadata>().WithMany(m => m.Tracks)
                .HasForeignKey(t => t.RingLocationHistoryMediaMetadataId).OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureAppLocationHistory(ModelBuilder b)
        {
            _ = b.Entity<RingAppLocationHistoryMessage>().HasIndex(m => m.EventId).IsUnique();
            _ = b.Entity<RingAppLocationHistoryBody>()
                .HasOne<RingAppLocationHistoryMessage>().WithMany(m => m.Bodies)
                .HasForeignKey(x => x.RingAppLocationHistoryMessageId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingAppLocationHistoryBody>()
                .HasOne(x => x.DeviceState).WithOne()
                .HasForeignKey<RingAppLocationHistoryDeviceState>(s => s.RingAppLocationHistoryBodyId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingAppLocationHistoryDeviceMode>()
                .HasOne<RingAppLocationHistoryDeviceState>().WithMany(s => s.Modes)
                .HasForeignKey(m => m.RingAppLocationHistoryDeviceStateId).OnDelete(DeleteBehavior.Cascade);
            _ = b.Entity<RingAppLocationHistoryImpulse>()
                .HasOne<RingAppLocationHistoryBody>().WithMany(x => x.Impulses)
                .HasForeignKey(i => i.RingAppLocationHistoryBodyId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
