using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.ReferenceData
{
    /// <summary>
    /// Loads versioned reference sources into the database, and records unknown provider kinds as discovered rows.
    /// A source is applied only when its version is higher than the stored version. Catalog rows are retired when a
    /// version drops them; discovered rows are never retired by the seeder, only promoted when the catalog lists them.
    /// </summary>
    public static class ReferenceDataSeeder
    {
        /// <summary>Applies every source whose version is ahead of the stored version.</summary>
        public static async Task SeedAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            IEnumerable<IReferenceDataSource> sources,
            CancellationToken ct)
        {
            foreach (IReferenceDataSource source in sources)
            {
                await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);

                ReferenceDataVersion? stored = await db.ReferenceDataVersions.FirstOrDefaultAsync(v => v.SourceName == source.Name, ct);
                if (stored is not null && stored.Version >= source.Version)
                {
                    continue;
                }

                ReferenceDataSet data = source.GetDataSet();

                // Every row this provider owns, catalog or discovered, so an incoming catalog row can promote a discovered one.
                Dictionary<string, DeviceModel> existing = await db.DeviceModels
                    .Where(m => m.Provider == source.Name)
                    .ToDictionaryAsync(m => m.Kind, ct);
                var incomingKinds = data.Models.Select(m => m.Kind).ToHashSet(StringComparer.Ordinal);

                foreach (DeviceModel model in data.Models)
                {
                    if (existing.TryGetValue(model.Kind, out DeviceModel? current))
                    {
                        current.Family = model.Family;
                        current.ModelName = model.ModelName;
                        current.Generation = model.Generation;
                        current.Source = DeviceModelSources.Catalog;
                    }
                    else
                    {
                        _ = db.DeviceModels.Add(model);
                    }
                }

                // Catalog rows the source no longer lists are retired. Discovered rows stay, because nothing in the catalog owns them.
                var retired = existing.Values
                    .Where(m => m.Source == DeviceModelSources.Catalog && !incomingKinds.Contains(m.Kind))
                    .ToList();
                var catalogKinds = incomingKinds.Concat(retired.Select(m => m.Kind)).ToHashSet(StringComparer.Ordinal);

                // Links for catalog-owned kinds are replaced wholesale, so a removed link does not survive a version bump.
                // Links for discovered kinds are not touched.
                List<DeviceModelFeature> staleLinks = await db.DeviceModelFeatures
                    .Where(l => catalogKinds.Contains(l.DeviceModelKind))
                    .ToListAsync(ct);
                db.DeviceModelFeatures.RemoveRange(staleLinks);
                db.DeviceModelFeatures.AddRange(data.ModelFeatures);

                // Feature definitions are shared across providers, so only codes not yet present are added.
                var knownFeatures = (await db.DeviceFeatureDefinitions.Select(f => f.Code).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
                foreach (DeviceFeatureDefinition feature in data.Features.Where(f => !knownFeatures.Contains(f.Code)))
                {
                    _ = db.DeviceFeatureDefinitions.Add(feature);
                }

                db.DeviceModels.RemoveRange(retired);

                if (stored is null)
                {
                    _ = db.ReferenceDataVersions.Add(new ReferenceDataVersion { SourceName = source.Name, Version = source.Version, AppliedUtc = DateTime.UtcNow });
                }
                else
                {
                    stored.Version = source.Version;
                    stored.AppliedUtc = DateTime.UtcNow;
                }

                _ = await db.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Records a provider kind the catalog does not list, so the database knows it exists. The row has no product name and
        /// is marked <see cref="DeviceModelSources.Discovered"/>. An existing row for the kind (catalog or discovered) is left alone.
        /// </summary>
        public static async Task EnsureDiscoveredModelAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            string provider,
            string kind,
            string family,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);

            if (await db.DeviceModels.AnyAsync(m => m.Kind == kind, ct))
            {
                return;
            }

            _ = db.DeviceModels.Add(new DeviceModel
            {
                Kind = kind,
                Provider = provider,
                Family = family,
                ModelName = null,
                Generation = null,
                Source = DeviceModelSources.Discovered,
            });
            _ = await db.SaveChangesAsync(ct);
        }
    }
}
