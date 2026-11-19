using System.Runtime.CompilerServices;

using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// The context of the unit of work running in this async flow, if any. A standalone append made from inside a unit
    /// writes on this context, so it joins the unit's transaction. A second connection would wait on the unit's write lock.
    /// </summary>
    internal static class AmbientUnitOfWork
    {
        private static readonly AsyncLocal<VideoForensicsDbContext?> Current = new();

        /// <summary>The open unit's context in this flow, or null when none is open.</summary>
        public static VideoForensicsDbContext? DbContext
        {
            get => Current.Value;
            set => Current.Value = value;
        }
    }

    /// <summary>
    /// One write gate per database. SQLite defers its write lock to the first write, so two transactions on the same
    /// database can both read the same chain tail and chain off it. Holding that database's gate from the tail read
    /// through commit stops that for both the standalone repository and the unit of work.
    /// </summary>
    /// <remarks>
    /// The gate is keyed by the database's context factory, not held process-wide. A process has one database in
    /// production, so behavior there is unchanged. Test fixtures each get their own database and therefore their own
    /// gate, so one fixture's held or stalled write cannot block another fixture's writes.
    /// Re-entrant within one async flow: a standalone append made from inside a unit of work runs in the same flow
    /// that already holds the gate, so it must not wait on itself. Callers set the held flag themselves after
    /// <see cref="TryWaitAsync"/>, because an async helper's AsyncLocal write is not visible to the awaiting caller.
    /// </remarks>
    internal static class ActionLogAppendGate
    {
        private static readonly ConditionalWeakTable<object, SemaphoreSlim> Gates = new();
        private static readonly AsyncLocal<object?> HeldKey = new();

        /// <summary>True when the current async flow already holds the gate for this database.</summary>
        public static bool IsHeldByCurrentFlow(object databaseKey)
        {
            return ReferenceEquals(HeldKey.Value, databaseKey);
        }

        /// <summary>Records whether the current async flow holds the gate for this database.</summary>
        public static void SetHeldByCurrentFlow(object databaseKey, bool held)
        {
            HeldKey.Value = held ? databaseKey : null;
        }

        /// <summary>Waits for this database's gate for at most <paramref name="timeout"/>. Returns false when it timed out.</summary>
        public static Task<bool> TryWaitAsync(object databaseKey, TimeSpan timeout, CancellationToken ct)
        {
            return GateFor(databaseKey).WaitAsync(timeout, ct);
        }

        /// <summary>Releases this database's gate. Call exactly once for each successful wait.</summary>
        public static void Release(object databaseKey)
        {
            _ = GateFor(databaseKey).Release();
        }

        private static SemaphoreSlim GateFor(object databaseKey)
        {
            return Gates.GetValue(databaseKey, _ => new SemaphoreSlim(1, 1));
        }
    }
}
