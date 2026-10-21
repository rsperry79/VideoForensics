using System.Collections.Generic;

using VideoForensics.Providers.Ring.Catalog;

namespace VideoForensics.Providers.Ring.Interfaces;

/// <summary>
/// Maps Ring's raw device kind (for example <c>cocoa_doorbell_v5</c>) to its product family, marketing model and generation.
/// </summary>
public interface IRingModelCatalog
{
    /// <summary>
    /// Returns the catalog entry for a Ring kind, or null when the kind is blank or not in the catalog.
    /// </summary>
    /// <param name="kind">The raw Ring kind as stored on <c>Device.RingKind</c>.</param>
    RingModelInfo? Find(string? kind);

    /// <summary>All catalog entries.</summary>
    IReadOnlyList<RingModelInfo> All { get; }
}
