using Xunit;

namespace VideoForensics.Utils.DbRepair.Tests;

public class HoldLostMessages_Tests
{
    [Fact]
    public void DescribeHoldLost_Applied_SaysRepairIsInDatabase()
    {
        string message = HoldLostMessages.DescribeHoldLost(applied: true, backupPath: @"C:\data\db.bak-20261102010101");

        Assert.Equal(@"maintenance ended after the repair committed; the repair is in the database. Check the backup at C:\data\db.bak-20261102010101 only if you need to roll back.", message);
    }

    [Fact]
    public void DescribeHoldLost_NotApplied_KeepsRollbackMessage()
    {
        string message = HoldLostMessages.DescribeHoldLost(applied: false, backupPath: @"C:\data\db.bak-20261102010101");

        Assert.Equal(@"the server ended maintenance; the repair was stopped; the repair did not commit or was rolled back; check the database and the backup at C:\data\db.bak-20261102010101", message);
    }
}
