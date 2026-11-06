using System.Text.Json;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// A logged repair of a broken action log chain. Entries are never rewritten: the repair is a new, hash-chained
    /// <c>ChainRepaired</c> entry whose details name each gap as a bridge, the last verified hash before the gap and the
    /// hash the next entry still points at. The verifier accepts a gap only when a bridge names it exactly.
    /// </summary>
    public static class ChainRepairRecord
    {
        /// <summary>The action name written for a logged repair.</summary>
        public const string Action = "ChainRepaired";

        /// <summary>One gap: the verified hash before it, and the hash the entry after it still records.</summary>
        public sealed record Bridge(string From, string To);

        /// <summary>The details JSON of a <c>ChainRepaired</c> entry.</summary>
        public sealed record Details(IReadOnlyList<Bridge> Bridges);

        /// <summary>Serializes the bridges for the entry's details column.</summary>
        public static string Serialize(IReadOnlyList<Bridge> bridges)
        {
            return JsonSerializer.Serialize(new Details(bridges));
        }

        /// <summary>Reads the bridges from a details column. A malformed column bridges nothing, so the gap stays visible.</summary>
        public static IEnumerable<Bridge> ReadBridges(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<Details>(detailsJson)?.Bridges ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
