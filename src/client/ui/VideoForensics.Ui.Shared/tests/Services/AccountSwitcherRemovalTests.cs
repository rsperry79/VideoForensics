using System.Linq;

using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class AccountSwitcherRemovalTests
{
    [Theory]
    [InlineData("AccountSwitcher")]
    [InlineData("AccountSelectionState")]
    public void UiSharedAssembly_RemovedAccountSelectionType_DoesNotExist(string typeName)
    {
        var assembly = typeof(PairedSessionState).Assembly;

        var matches = assembly.GetTypes().Where(t => t.Name == typeName).ToList();

        Assert.Empty(matches);
    }
}
