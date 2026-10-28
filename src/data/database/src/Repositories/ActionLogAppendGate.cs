namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// One gate for every hash-chain write in the process. SQLite defers its write lock to the first write, so two
    /// transactions can both read the same chain tail and chain off it. Holding this gate from the tail read through
    /// commit stops that for both the standalone repository and the unit of work.
    /// </summary>
    /// <remarks>
    /// Re-entrant within one async flow: a standalone append made from inside a unit of work runs in the same flow
    /// that already holds the gate, so it must not wait on itself. Callers set <see cref="IsHeldByCurrentFlow"/> themselves
    /// after <see cref="WaitAsync"/>, because an async helper's AsyncLocal write is not visible to the awaiting caller.
    /// </remarks>
    internal static class ActionLogAppendGate
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly AsyncLocal<bool> HeldByCurrentFlow = new();

        /// <summary>True when the current async flow already holds the gate.</summary>
        public static bool IsHeldByCurrentFlow
        {
            get => HeldByCurrentFlow.Value;
            set => HeldByCurrentFlow.Value = value;
        }

        /// <summary>Waits until no other flow holds the gate. Only call when <see cref="IsHeldByCurrentFlow"/> is false.</summary>
        public static Task WaitAsync(CancellationToken ct) => Gate.WaitAsync(ct);

        /// <summary>Releases the gate. Call exactly once for each successful <see cref="WaitAsync"/>.</summary>
        public static void Release() => _ = Gate.Release();
    }
}
