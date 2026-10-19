namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Scope;

/// <summary>Jamming is per device: several scoped devices are each analyzed and their incidents combined.</summary>
public class Analyze_JammingMultiDevice_Tests : AnalyzePageTestBase
{
    public Analyze_JammingMultiDevice_Tests()
    {
        // The Jamming tab is Admin-only (PageAccessRules), so these scenarios run as an Admin.
        Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Admin").GetAwaiter().GetResult();
    }

    [Fact]
    public void TwoDevices_Jamming_AnalyzesEachDevice_NoErrors()
    {
        var first = CreateTestDevice("Front");
        var second = CreateTestDevice("Back");
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { first.Id, second.Id } });
        NavigateToAnalysis("jamming");

        var component = RenderPage();

        DeviceHealthRepositoryMock.Verify(m => m.GetHistoryAsync(first.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        DeviceHealthRepositoryMock.Verify(m => m.GetHistoryAsync(second.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(component.FindAll(".alert-danger"));
    }
}
