using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Models;

namespace VideoForensics.Ui.Shared.Services.Cases;

/// <summary>
/// Describes the integrity status of a pinned case item compared to its current state.
/// </summary>
public enum PinIntegrity
{
    /// <summary>Not applicable - event items have no hash to compare.</summary>
    NotApplicable = 0,

    /// <summary>Media item hash matches the hash at pin time.</summary>
    Match = 1,

    /// <summary>Media item hash differs from the hash at pin time.</summary>
    Mismatch = 2,

    /// <summary>Media item was deleted or is no longer available.</summary>
    MediaMissing = 3
}

/// <summary>
/// Pure helper for comparing case item integrity against current media state.
/// </summary>
public static class CaseItemIntegrity
{
    /// <summary>
    /// Compares a case item against its current media state and returns the integrity status.
    /// Event items always return NotApplicable.
    /// Media items with null current media return MediaMissing.
    /// Media items with matching SHA256 (case-insensitive) return Match; otherwise Mismatch.
    /// </summary>
    public static PinIntegrity Compare(CaseItem item, MediaItem? currentMedia)
    {
        // Event items have no hash to compare
        if (item.Kind == CaseItemKind.Event)
        {
            return PinIntegrity.NotApplicable;
        }

        // Media item is no longer available
        if (currentMedia is null)
        {
            return PinIntegrity.MediaMissing;
        }

        // Compare hashes (case-insensitive)
        var hashAtAddMatch = string.Equals(
            item.MediaSha256AtAdd,
            currentMedia.Sha256Hash,
            StringComparison.OrdinalIgnoreCase);

        return hashAtAddMatch ? PinIntegrity.Match : PinIntegrity.Mismatch;
    }
}
