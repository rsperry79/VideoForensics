using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.ReferenceData
{
    /// <summary>
    /// Loads versioned reference sources into the database. A source is applied only when its version is higher than the
    /// stored version, so a database at the current version is left alone and a bumped catalog updates its own rows.
    /// </summary>
    public static class ReferenceDataSeeder
    {
        /// <summary>Applies every source whose version is ahead of the stored version. Rows are upserted by key and the source's links are replaced.</summary>
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

                // Models: upsert by kind. Kinds this provider no longer ships are removed below.
                Dictionary<string, DeviceModel> existingByKind = await db.DeviceModels
                    .Where(m => m.Provider == source.Name)
                    .ToDictionaryAsync(m => m.Kind, ct);

                // Every kind this provider owns, before or after the change, so links for kinds that disappear are cleared too.
                HashSet<string> providerKinds = data.Models.Select(m => m.Kind)
                    .Concat(existingByKind.Keys)
                    .ToHashSet(StringComparer.Ordinal);

                Dictionary<string, DeviceModel> retired = new(existingByKind, StringComparer.Ordinal);
                foreach (DeviceModel model in data.Models)
                {
                    if (retired.Remove(model.Kind, out DeviceModel? current))
                    {
                        current.Family = model.Family;
                        current.ModelName = model.ModelName;
                        current.Generation = model.Generation;
                    }
                    else
                    {
                        _ = db.DeviceModels.Add(model);
                    }
                }

                // Links for this provider's kinds are replaced wholesale, so a removed link does not survive a version bump.
                List<DeviceModelFeature> staleLinks = await db.DeviceModelFeatures
                    .Where(l => providerKinds.Contains(l.DeviceModelKind))
                    .ToListAsync(ct);
                db.DeviceModelFeatures.RemoveRange(staleLinks);
                db.DeviceModelFeatures.AddRange(data.ModelFeatures);

                // Feature definitions are shared across providers, so only codes not yet present are added.
                HashSet<string> knownFeatures = (await db.DeviceFeatureDefinitions.Select(f => f.Code).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
                foreach (DeviceFeatureDefinition feature in data.Features.Where(f => !knownFeatures.Contains(f.Code)))
                {
                    _ = db.DeviceFeatureDefinitions.Add(feature);
                }

                foreach (DeviceModel gone in retired.Values)
                {
                    _ = db.DeviceModels.Remove(gone);
                }

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
    }
}
