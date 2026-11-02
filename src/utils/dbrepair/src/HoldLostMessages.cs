namespace VideoForensics.Utils.DbRepair;

/// <summary>Operator messages for a run whose server maintenance ended before the run finished.</summary>
public static class HoldLostMessages
{
    /// <summary>
    /// Says what state the database is in when the hold was lost. When the repair committed, the repair is kept and the
    /// backup is only for a rollback. Otherwise nothing was committed and the backup is the way back.
    /// </summary>
    /// <param name="applied">True when the repair report says the commit happened.</param>
    /// <param name="backupPath">Path of the backup taken before the repair, or a placeholder when none was written.</param>
    public static string DescribeHoldLost(bool applied, string backupPath) =>
        applied
            ? $"maintenance ended after the repair committed; the repair is in the database. Check the backup at {backupPath} only if you need to roll back."
            : $"the server ended maintenance; the repair was stopped; the repair did not commit or was rolled back; check the database and the backup at {backupPath}";
}
