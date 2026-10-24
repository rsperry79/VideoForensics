using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class AccountSelectionTests
{
    [Fact]
    public void Resolve_StoredIdIsStillLinked_ReturnsStoredId()
    {
        var first = Guid.NewGuid();
        var stored = Guid.NewGuid();

        Guid? result = AccountSelection.Resolve(stored.ToString(), new[] { first, stored });

        Assert.Equal(stored, result);
    }

    [Fact]
    public void Resolve_StoredIdNoLongerLinked_ReturnsFirstAccount()
    {
        var first = Guid.NewGuid();
        var removed = Guid.NewGuid();

        Guid? result = AccountSelection.Resolve(removed.ToString(), new[] { first });

        Assert.Equal(first, result);
    }

    [Fact]
    public void Resolve_NoStoredId_ReturnsFirstAccount()
    {
        var first = Guid.NewGuid();

        Guid? result = AccountSelection.Resolve(null, new[] { first, Guid.NewGuid() });

        Assert.Equal(first, result);
    }

    [Fact]
    public void Resolve_MalformedStoredId_ReturnsFirstAccount()
    {
        var first = Guid.NewGuid();

        Guid? result = AccountSelection.Resolve("not-a-guid", new[] { first });

        Assert.Equal(first, result);
    }

    [Fact]
    public void Resolve_NoAccounts_ReturnsNull()
    {
        Guid? result = AccountSelection.Resolve(Guid.NewGuid().ToString(), Array.Empty<Guid>());

        Assert.Null(result);
    }
}
