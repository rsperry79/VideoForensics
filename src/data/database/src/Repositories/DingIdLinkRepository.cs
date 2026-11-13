using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Device-scoped ding id candidates and the doorbot-to-Events link. Candidates are read only from the string-id feeds
    /// (location events and video search), because those feeds store the string ding id that a doorbot history id must equal.
    /// </summary>
    public class DingIdLinkRepository : IDingIdLinkRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Initializes a new instance of the DingIdLinkRepository.</summary>
        public DingIdLinkRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public Task<IReadOnlyList<string>> GetCandidateDingIdsAsync(long doorbotId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync<IReadOnlyList<string>>(context =>
            {
                VideoForensicsDbContext db = context.Db();
                return Task.FromResult<IReadOnlyList<string>>(DingIdCandidates.ForDoorbot(db, doorbotId));
            }, ct);
        }

        public async Task LinkDoorbotHistoryEventAsync(string doorbotHistoryProviderEventId, Guid eventId, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                // Tracked entity: the unit of work saves the change when the transaction commits.
                RingDoorbotHistoryEvent? row = await db.RingDoorbotHistoryEvents.FirstOrDefaultAsync(e => e.ProviderEventId == doorbotHistoryProviderEventId, ct);
                if (row is not null)
                {
                    row.LinkedEventId = eventId;
                }

                return null;
            }, ct);
        }

        public Task<VideoForensics.Data.Common.Entities.Event?> GetLinkedEventAsync(string doorbotHistoryProviderEventId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync<VideoForensics.Data.Common.Entities.Event?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Guid? linkedId = await db.RingDoorbotHistoryEvents
                    .Where(e => e.ProviderEventId == doorbotHistoryProviderEventId)
                    .Select(e => e.LinkedEventId)
                    .FirstOrDefaultAsync(ct);
                return linkedId is { } id
                    ? await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct)
                    : null;
            }, ct);
        }

        public async Task RekeyLinkedEventAsync(Guid eventId, string newProviderEventId, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                // Tracked entity: the unit of work saves the change when the transaction commits. The unique (DeviceId, ProviderEventId) index
                // forbids two rows with one id, so when another row of the device already holds the ding id, the placeholder's doorbot rows
                // move to that row instead and the placeholder is removed. Only a placeholder that was never matched and has no media is removed.
                VideoForensics.Data.Common.Entities.Event? evt = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
                if (evt is null)
                {
                    return null;
                }

                VideoForensics.Data.Common.Entities.Event? existing = await db.Events.FirstOrDefaultAsync(e =>
                    e.DeviceId == evt.DeviceId && e.ProviderEventId == newProviderEventId && e.Id != eventId, ct);
                if (existing is null)
                {
                    evt.ProviderEventId = newProviderEventId;
                    evt.AwaitingDingId = false;
                    return null;
                }

                foreach (RingDoorbotHistoryEvent doorbotRow in await db.RingDoorbotHistoryEvents.Where(r => r.LinkedEventId == evt.Id).ToListAsync(ct))
                {
                    doorbotRow.LinkedEventId = existing.Id;
                }

                if (evt.AwaitingDingId && evt.DownloadedAtUtc is null && evt.DownloadStatus == EventDownloadStatus.NotAttempted)
                {
                    _ = db.Events.Remove(evt);
                }

                return null;
            }, ct);
        }
    }
}
