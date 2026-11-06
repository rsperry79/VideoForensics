using Microsoft.EntityFrameworkCore;

using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// The flag logic shared by the standalone repository (own context and transaction) and the unit-of-work repository
    /// (the unit's context). Nothing here commits: the caller owns SaveChanges and the transaction.
    /// </summary>
    internal static class MediaFlagOperations
    {
        private const int MaxNoteLength = 1024;

        public static void ValidateRaised(MediaFlag raised)
        {
            ArgumentNullException.ThrowIfNull(raised);
            if (raised.DeviceId == Guid.Empty)
            {
                throw new ArgumentException("A flag needs a device id.", nameof(raised));
            }

            if (raised.WindowEndUtc < raised.WindowStartUtc)
            {
                throw new ArgumentException("The flag window ends before it starts.", nameof(raised));
            }
        }

        /// <summary>Finds the row a raised flag maps onto, including rows this context added but has not saved.</summary>
        public static async Task<MediaFlag?> FindExistingAsync(VideoForensicsDbContext db, MediaFlag raised, CancellationToken ct)
        {
            Guid? mediaItemId = raised.MediaItemId;
            MediaFlagType type = raised.FlagType;
            Guid deviceId = raised.DeviceId;
            DateTime start = raised.WindowStartUtc;

            MediaFlag? pending = mediaItemId is not null
                ? db.MediaFlags.Local.FirstOrDefault(f => f.MediaItemId == mediaItemId && f.FlagType == type)
                : db.MediaFlags.Local.FirstOrDefault(f => f.MediaItemId == null && f.DeviceId == deviceId && f.FlagType == type && f.WindowStartUtc == start);
            if (pending is not null)
            {
                return pending;
            }

            return mediaItemId is not null
                ? await db.MediaFlags.FirstOrDefaultAsync(f => f.MediaItemId == mediaItemId && f.FlagType == type, ct)
                : await db.MediaFlags.FirstOrDefaultAsync(f => f.DeviceId == deviceId && f.FlagType == type && f.WindowStartUtc == start, ct);
        }

        /// <summary>Adds the flag or refreshes the matching Unreviewed row. Reviewed rows are returned as they are.</summary>
        public static async Task<MediaFlag> StageUpsertAsync(VideoForensicsDbContext db, MediaFlag raised, CancellationToken ct)
        {
            ValidateRaised(raised);
            MediaFlag? existing = await FindExistingAsync(db, raised, ct);
            if (existing is null)
            {
                raised.Id = raised.Id == Guid.Empty ? Guid.NewGuid() : raised.Id;
                raised.Status = MediaFlagStatus.Unreviewed;
                raised.ReviewedBy = null;
                raised.ReviewedAtUtc = null;
                raised.ReviewNote = null;
                raised.RowVersion = 0;
                if (raised.RaisedAtUtc == default)
                {
                    raised.RaisedAtUtc = DateTime.UtcNow;
                }

                _ = db.MediaFlags.Add(raised);
                return raised;
            }

            if (existing.Status != MediaFlagStatus.Unreviewed)
            {
                return existing;
            }

            bool changed = existing.Severity != raised.Severity
                || existing.WindowStartUtc != raised.WindowStartUtc
                || existing.WindowEndUtc != raised.WindowEndUtc
                || (raised.NoticeId is not null && existing.NoticeId != raised.NoticeId);
            if (changed)
            {
                existing.Severity = raised.Severity;
                existing.WindowStartUtc = raised.WindowStartUtc;
                existing.WindowEndUtc = raised.WindowEndUtc;
                existing.NoticeId = raised.NoticeId ?? existing.NoticeId;
                existing.RowVersion++;
            }

            return existing;
        }

        public static async Task<int> ReconcileAsync(VideoForensicsDbContext db, Guid mediaItemId, IReadOnlySet<MediaFlagType> raised, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(raised);
            List<MediaFlagType> keep = raised.ToList();
            return await db.MediaFlags
                .Where(f => f.MediaItemId == mediaItemId && f.Status == MediaFlagStatus.Unreviewed && !keep.Contains(f.FlagType))
                .ExecuteDeleteAsync(ct);
        }

        public static IQueryable<MediaFlag> ApplyFilter(IQueryable<MediaFlag> query, MediaFlagFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            if (filter.Types is { Count: > 0 } types)
            {
                List<MediaFlagType> list = types.ToList();
                query = query.Where(f => list.Contains(f.FlagType));
            }

            if (filter.Statuses is { Count: > 0 } statuses)
            {
                List<MediaFlagStatus> list = statuses.ToList();
                query = query.Where(f => list.Contains(f.Status));
            }

            if (filter.DeviceIds is { Count: > 0 } devices)
            {
                List<Guid> list = devices.ToList();
                query = query.Where(f => list.Contains(f.DeviceId));
            }

            if (filter.FromUtc is { } from)
            {
                query = query.Where(f => f.WindowStartUtc >= from);
            }

            if (filter.ToUtc is { } to)
            {
                query = query.Where(f => f.WindowStartUtc <= to);
            }

            return query;
        }

        public static async Task<MediaFlagPage> ListPageAsync(VideoForensicsDbContext db, MediaFlagFilter filter, int skip, int take, CancellationToken ct)
        {
            IQueryable<MediaFlag> query = ApplyFilter(db.MediaFlags.AsNoTracking(), filter);
            int total = await query.CountAsync(ct);
            if (take <= 0)
            {
                return new MediaFlagPage([], total);
            }

            List<MediaFlag> items = await query
                .OrderByDescending(f => f.RaisedAtUtc).ThenByDescending(f => f.Id)
                .Skip(Math.Max(skip, 0)).Take(take)
                .ToListAsync(ct);
            return new MediaFlagPage(items, total);
        }

        public static Task<int> CountAsync(VideoForensicsDbContext db, MediaFlagFilter filter, CancellationToken ct)
        {
            return ApplyFilter(db.MediaFlags.AsNoTracking(), filter).CountAsync(ct);
        }

        /// <summary>Keyset step on (RaisedAtUtc, Id), so a neighbour lookup never scans or pages the whole list.</summary>
        public static async Task<MediaFlag?> GetNeighbourAsync(VideoForensicsDbContext db, Guid flagId, MediaFlagFilter filter, MediaFlagNeighbour direction, CancellationToken ct)
        {
            MediaFlag? anchor = await db.MediaFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Id == flagId, ct);
            if (anchor is null)
            {
                return null;
            }

            DateTime at = anchor.RaisedAtUtc;
            Guid id = anchor.Id;
            IQueryable<MediaFlag> query = ApplyFilter(db.MediaFlags.AsNoTracking(), filter);
            if (direction == MediaFlagNeighbour.Next)
            {
                return await query
                    .Where(f => f.RaisedAtUtc < at || (f.RaisedAtUtc == at && f.Id.CompareTo(id) < 0))
                    .OrderByDescending(f => f.RaisedAtUtc).ThenByDescending(f => f.Id)
                    .FirstOrDefaultAsync(ct);
            }

            return await query
                .Where(f => f.RaisedAtUtc > at || (f.RaisedAtUtc == at && f.Id.CompareTo(id) > 0))
                .OrderBy(f => f.RaisedAtUtc).ThenBy(f => f.Id)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>
        /// Counts the matching flags listed before this one, then adds one. The "before" test is the reverse of the list's
        /// sort (RaisedAtUtc then Id, both descending), so it must stay in step with <see cref="ListPageAsync"/>.
        /// </summary>
        public static async Task<int?> GetPositionAsync(VideoForensicsDbContext db, Guid flagId, MediaFlagFilter filter, CancellationToken ct)
        {
            IQueryable<MediaFlag> query = ApplyFilter(db.MediaFlags.AsNoTracking(), filter);
            MediaFlag? anchor = await query.FirstOrDefaultAsync(f => f.Id == flagId, ct);
            if (anchor is null)
            {
                return null;
            }

            DateTime at = anchor.RaisedAtUtc;
            Guid id = anchor.Id;
            int before = await query.CountAsync(f => f.RaisedAtUtc > at || (f.RaisedAtUtc == at && f.Id.CompareTo(id) > 0), ct);
            return before + 1;
        }

        public static void ValidateReview(MediaFlagStatus expectedStatus, MediaFlagStatus newStatus, string? reason, string operatorName)
        {
            if (string.IsNullOrWhiteSpace(operatorName))
            {
                throw new ArgumentException("An operator is required.", nameof(operatorName));
            }

            if (newStatus == MediaFlagStatus.Unreviewed)
            {
                throw new ArgumentException("Use ReopenAsync to move a flag back to Unreviewed.", nameof(newStatus));
            }

            if (newStatus == expectedStatus)
            {
                throw new ArgumentException("The new status equals the expected status.", nameof(newStatus));
            }

            if (newStatus == MediaFlagStatus.Dismissed && string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Dismissing a flag requires a reason.", nameof(reason));
            }

            if (reason is { Length: > MaxNoteLength })
            {
                throw new ArgumentException($"The reason is longer than {MaxNoteLength} characters.", nameof(reason));
            }
        }

        public static void ValidateReopen(string operatorName, string reason)
        {
            if (string.IsNullOrWhiteSpace(operatorName))
            {
                throw new ArgumentException("An operator is required.", nameof(operatorName));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Re-opening a flag requires a reason.", nameof(reason));
            }

            if (reason.Length > MaxNoteLength)
            {
                throw new ArgumentException($"The reason is longer than {MaxNoteLength} characters.", nameof(reason));
            }
        }

        /// <summary>
        /// Changes the status and appends the audit entry on the same context, so one SaveChanges commits both. The status
        /// is checked before anything is written, so a conflict leaves no audit entry.
        /// </summary>
        public static async Task<MediaFlag> StageReviewAsync(
            VideoForensicsDbContext db, IActionLogRepository actionLog, Guid flagId, MediaFlagStatus expectedStatus,
            MediaFlagStatus newStatus, string? reason, string operatorName, string action, CancellationToken ct)
        {
            // Local first: a flag upserted earlier in the same unit of work is not in the database yet.
            MediaFlag flag = db.MediaFlags.Local.FirstOrDefault(f => f.Id == flagId)
                ?? await db.MediaFlags.FirstOrDefaultAsync(f => f.Id == flagId, ct)
                ?? throw new KeyNotFoundException($"Flag {flagId} does not exist.");
            if (flag.Status != expectedStatus)
            {
                throw new FlagStatusConflictException(flagId, expectedStatus, flag.Status, flag.ReviewedBy, flag.ReviewedAtUtc);
            }

            string? note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            flag.Status = newStatus;
            if (newStatus == MediaFlagStatus.Unreviewed)
            {
                flag.ReviewedBy = null;
                flag.ReviewedAtUtc = null;
                flag.ReviewNote = null;
            }
            else
            {
                flag.ReviewedBy = operatorName;
                flag.ReviewedAtUtc = DateTime.UtcNow;
                flag.ReviewNote = note;
            }

            flag.RowVersion++;

            string details = JsonSerializer.Serialize(new
            {
                flagId = flag.Id,
                flagType = flag.FlagType.ToString(),
                mediaItemId = flag.MediaItemId,
                deviceId = flag.DeviceId,
                oldStatus = expectedStatus.ToString(),
                newStatus = newStatus.ToString(),
                reason = note,
            });
            _ = await actionLog.AppendAsync(operatorName, ActorType.Human, action, "MediaFlag", flag.Id, details, ct);
            return flag;
        }

        public static string ActionFor(MediaFlagStatus newStatus)
        {
            return newStatus switch
            {
                MediaFlagStatus.Kept => "KeepMediaFlag",
                MediaFlagStatus.Dismissed => "DismissMediaFlag",
                _ => "ReopenMediaFlag",
            };
        }
    }
}
