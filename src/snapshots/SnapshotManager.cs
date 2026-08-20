using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace Ring.Api.Snapshots
{
    /// <summary>
    /// Default implementation of ISnapshotManager. Manages Ring device snapshots by coordinating
    /// with a Session instance.
    /// </summary>
    public class SnapshotManager : ISnapshotManager
    {
        private readonly KoenZomers.Ring.Api.Session _session;

        public SnapshotManager(KoenZomers.Ring.Api.Session session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<bool> SaveLatestSnapshotAsync(Doorbot doorbot, string outputPath, CancellationToken cancellationToken = default)
        {
            if (doorbot == null) throw new ArgumentNullException(nameof(doorbot));
            return await SaveLatestSnapshotAsync(doorbot.Id, outputPath, cancellationToken);
        }

        public async Task<bool> SaveLatestSnapshotAsync(long doorbotId, string outputPath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

                using var stream = await GetLatestSnapshotAsync(doorbotId, cancellationToken);
                using var fileStream = File.Create(outputPath);

                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(fileStream, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Stream> GetLatestSnapshotAsync(Doorbot doorbot, CancellationToken cancellationToken = default)
        {
            if (doorbot == null) throw new ArgumentNullException(nameof(doorbot));
            return await GetLatestSnapshotAsync(doorbot.Id, cancellationToken);
        }

        public async Task<Stream> GetLatestSnapshotAsync(long doorbotId, CancellationToken cancellationToken = default)
        {
            return await _session.GetLatestSnapshot((int)doorbotId);
        }

        public async Task<bool> RefreshSnapshotAsync(long doorbotId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _session.UpdateSnapshot((int)doorbotId);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
