
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>Jamming is per device: several scoped devices are each analyzed and their incidents combined.</summary>
public class Analyze_JammingMultiDevice_Tests : AnalyzePageTestBase
{
    public Analyze_JammingMultiDevice_Tests()
    {
        // The Jamming tab is Admin-only (PageAccessRules), so these scenarios run as an Admin.
        Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Admin").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task SupersededLoad_DoesNotOverwriteTheNewerResult()
    {
        Device first = CreateTestDevice("First");
        Device second = CreateTestDevice("Second");
        var gate = new TaskCompletionSource<IReadOnlyList<DeviceHealth>>();
        _ = DeviceHealthRepositoryMock
            .Setup(m => m.GetHistoryAsync(first.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(gate.Task);
        ScopeState.Set(ScopeState.Current with { DeviceIds = [first.Id] });
        NavigateToAnalysis("jamming");
        SetRendererInfo(new RendererInfo("Server", true));
        IRenderedComponent<Analyze> component = RenderPage();

        // Switch to the second device while the first is still in flight, then let the first finish.
        await component.InvokeAsync(() => ScopeState.Set(ScopeState.Current with { DeviceIds = [second.Id] }));
        component.WaitForAssertion(() => Assert.Contains("across 1 device(s)", component.Markup));
        await component.InvokeAsync(() => gate.SetResult([]));

        // Without the guard the superseded run would bump the shared count to 2.
        component.WaitForAssertion(() => Assert.Contains("across 1 device(s)", component.Markup));
        Assert.DoesNotContain("across 2 device(s)", component.Markup);
    }

    [Fact]
    public void TwoDevices_Jamming_AnalyzesEachDevice_NoErrors()
    {
        Device first = CreateTestDevice("Front");
        Device second = CreateTestDevice("Back");
        ScopeState.Set(ScopeState.Current with { DeviceIds = [first.Id, second.Id] });
        NavigateToAnalysis("jamming");

        IRenderedComponent<Analyze> component = RenderPage();

        DeviceHealthRepositoryMock.Verify(m => m.GetHistoryAsync(first.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        DeviceHealthRepositoryMock.Verify(m => m.GetHistoryAsync(second.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(component.FindAll(".alert-danger"));
    }
}
