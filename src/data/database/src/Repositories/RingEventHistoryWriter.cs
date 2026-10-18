using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Replaces stored Ring event history rows. Each write runs in one transaction: the previous row for the key and
    /// all of its child rows are deleted, then the new graph is inserted. If the insert fails the transaction rolls
    /// back and the previous row is left in place. Nested event objects are removed by (Source, SourceRowId) before
    /// the event row is deleted, because they have no foreign key to the event row.
    /// </summary>
    public class RingEventHistoryWriter : IRingEventHistoryWriter
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        public RingEventHistoryWriter(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        public Task ReplaceVideoSearchEventAsync(Guid ringAccountId, RingVideoSearchEvent evt, RingEventDetails details, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(evt);
            ArgumentNullException.ThrowIfNull(details);

            evt.RingAccountId = ringAccountId;
            return ReplaceAsync(async (db, token) =>
            {
                List<Guid> existing = await db.RingVideoSearchEvents
                    .Where(e => e.RingAccountId == ringAccountId && e.ProviderEventId == evt.ProviderEventId)
                    .Select(e => e.Id)
                    .ToListAsync(token);
                await DeleteEventAsync(db, RingEventSources.VideoSearch, existing, token);
                _ = await db.RingVideoSearchEvents.Where(e => existing.Contains(e.Id)).ExecuteDeleteAsync(token);

                _ = db.RingVideoSearchEvents.Add(evt);
                AddDetails(db, RingEventSources.VideoSearch, evt.Id, details);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceLocationEventPageAsync(RingLocationEventPage page, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(page);

            return ReplaceAsync(async (db, token) =>
            {
                _ = await db.RingLocationEventPages.Where(p => p.ProviderLocationId == page.ProviderLocationId).ExecuteDeleteAsync(token);
                _ = db.RingLocationEventPages.Add(page);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceLocationEventAsync(RingLocationEvent evt, RingEventDetails details, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(evt);
            ArgumentNullException.ThrowIfNull(details);

            return ReplaceAsync(async (db, token) =>
            {
                List<Guid> existing = await db.RingLocationEvents
                    .Where(e => e.ProviderLocationId == evt.ProviderLocationId && e.ProviderEventId == evt.ProviderEventId)
                    .Select(e => e.Id)
                    .ToListAsync(token);
                await DeleteEventAsync(db, RingEventSources.LocationEvent, existing, token);
                _ = await db.RingLocationEvents.Where(e => existing.Contains(e.Id)).ExecuteDeleteAsync(token);

                _ = db.RingLocationEvents.Add(evt);
                AddDetails(db, RingEventSources.LocationEvent, evt.Id, details);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceLocationHistoryPageAsync(RingLocationHistoryPage page, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(page);

            return ReplaceAsync(async (db, token) =>
            {
                _ = await db.RingLocationHistoryPages.Where(p => p.ProviderLocationId == page.ProviderLocationId).ExecuteDeleteAsync(token);
                _ = db.RingLocationHistoryPages.Add(page);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceLocationHistoryEventAsync(RingLocationHistoryEvent evt, RingEventDetails details, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(evt);
            ArgumentNullException.ThrowIfNull(details);

            return ReplaceAsync(async (db, token) =>
            {
                List<Guid> existing = await db.RingLocationHistoryEvents
                    .Where(e => e.ProviderLocationId == evt.ProviderLocationId && e.ProviderEventId == evt.ProviderEventId)
                    .Select(e => e.Id)
                    .ToListAsync(token);
                await DeleteEventAsync(db, RingEventSources.LocationHistory, existing, token);
                // Visualizations, media, metadata and tracks are foreign-key children, removed by cascade.
                _ = await db.RingLocationHistoryEvents.Where(e => existing.Contains(e.Id)).ExecuteDeleteAsync(token);

                _ = db.RingLocationHistoryEvents.Add(evt);
                AddDetails(db, RingEventSources.LocationHistory, evt.Id, details);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceDoorbotHistoryEventAsync(RingDoorbotHistoryEvent evt, RingEventDetails details, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(evt);
            ArgumentNullException.ThrowIfNull(details);

            return ReplaceAsync(async (db, token) =>
            {
                List<Guid> existing = await db.RingDoorbotHistoryEvents
                    .Where(e => e.ProviderEventId == evt.ProviderEventId)
                    .Select(e => e.Id)
                    .ToListAsync(token);
                await DeleteEventAsync(db, RingEventSources.DoorbotHistory, existing, token);
                _ = await db.RingDoorbotHistoryEvents.Where(e => existing.Contains(e.Id)).ExecuteDeleteAsync(token);

                _ = db.RingDoorbotHistoryEvents.Add(evt);
                AddDetails(db, RingEventSources.DoorbotHistory, evt.Id, details);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceAppLocationHistoryMessageAsync(RingAppLocationHistoryMessage message, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(message);

            return ReplaceAsync(async (db, token) =>
            {
                // Bodies, device states, modes and impulses are foreign-key children, removed by cascade.
                _ = await db.RingAppLocationHistoryMessages.Where(m => m.EventId == message.EventId).ExecuteDeleteAsync(token);
                _ = db.RingAppLocationHistoryMessages.Add(message);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        private async Task ReplaceAsync(Func<VideoForensicsDbContext, CancellationToken, Task> replace, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
            await replace(db, ct);
            await transaction.CommitAsync(ct);
        }

        /// <summary>Deletes the nested object rows of the given event rows, for every nested table of the feed.</summary>
        private static async Task DeleteEventAsync(VideoForensicsDbContext db, string source, List<Guid> eventRowIds, CancellationToken ct)
        {
            if (eventRowIds.Count == 0)
            {
                return;
            }

            _ = await db.RingEventDoorbots.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventProperties.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventDevicePlacements.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventGeolocations.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventSubscriptionReceipts.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventLastLocations.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventSirens.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            _ = await db.RingEventRecordings.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
            // Computer vision children (detection types, alerts, profiles, details, zones, tags) cascade from the root row.
            _ = await db.RingEventCvProperties.Where(x => x.Source == source && eventRowIds.Contains(x.SourceRowId)).ExecuteDeleteAsync(ct);
        }

        /// <summary>Attaches the nested objects of an event row, stamping each with its feed and event row id.</summary>
        private static void AddDetails(VideoForensicsDbContext db, string source, Guid eventRowId, RingEventDetails details)
        {
            if (details.Doorbot is { } doorbot)
            {
                Stamp(doorbot, source, eventRowId);
                _ = db.RingEventDoorbots.Add(doorbot);
            }

            if (details.Properties is { } properties)
            {
                Stamp(properties, source, eventRowId);
                _ = db.RingEventProperties.Add(properties);
            }

            if (details.DevicePlacement is { } placement)
            {
                Stamp(placement, source, eventRowId);
                _ = db.RingEventDevicePlacements.Add(placement);
            }

            if (details.Geolocation is { } geolocation)
            {
                Stamp(geolocation, source, eventRowId);
                _ = db.RingEventGeolocations.Add(geolocation);
            }

            if (details.SubscriptionReceipt is { } receipt)
            {
                Stamp(receipt, source, eventRowId);
                _ = db.RingEventSubscriptionReceipts.Add(receipt);
            }

            if (details.LastLocation is { } lastLocation)
            {
                Stamp(lastLocation, source, eventRowId);
                _ = db.RingEventLastLocations.Add(lastLocation);
            }

            if (details.Siren is { } siren)
            {
                Stamp(siren, source, eventRowId);
                _ = db.RingEventSirens.Add(siren);
            }

            if (details.Recording is { } recording)
            {
                Stamp(recording, source, eventRowId);
                _ = db.RingEventRecordings.Add(recording);
            }

            if (details.CvProperties is { } cv)
            {
                Stamp(cv, source, eventRowId);
                _ = db.RingEventCvProperties.Add(cv);
            }
        }

        private static void Stamp(RingEventDoorbot value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventProperties value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventDevicePlacement value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventGeolocation value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventSubscriptionReceipt value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventLastLocation value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventSiren value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventRecording value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }

        private static void Stamp(RingEventCvProperties value, string source, Guid rowId) { value.Source = source; value.SourceRowId = rowId; }
    }
}
