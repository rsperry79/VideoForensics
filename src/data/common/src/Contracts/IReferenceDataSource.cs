using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// A versioned set of reference rows a provider contributes to the database (for example the Ring model catalog).
    /// The seeder loads it on database generation and re-applies it only when <see cref="Version"/> is higher than the stored version.
    /// </summary>
    public interface IReferenceDataSource
    {
        /// <summary>Source name, unique per database (for example "Ring").</summary>
        string Name { get; }

        /// <summary>Monotonic data version. Bump it whenever <see cref="GetDataSet"/> changes.</summary>
        int Version { get; }

        /// <summary>The rows this source owns at its current version.</summary>
        ReferenceDataSet GetDataSet();
    }

    /// <summary>Rows for one reference-data source.</summary>
    /// <param name="Models">Device models.</param>
    /// <param name="Features">Feature definitions referenced by <paramref name="ModelFeatures"/>.</param>
    /// <param name="ModelFeatures">Model-to-feature links.</param>
    public sealed record ReferenceDataSet(
        IReadOnlyList<DeviceModel> Models,
        IReadOnlyList<DeviceFeatureDefinition> Features,
        IReadOnlyList<DeviceModelFeature> ModelFeatures);
}
