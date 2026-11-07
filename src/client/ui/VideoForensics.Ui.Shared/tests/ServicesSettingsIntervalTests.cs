using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Contracts;

using Xunit;

using ServicesSettingsPage = VideoForensics.Client.Ui.Shared.Pages.ServicesSettings;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The Services page shows one minute-based backfill interval per service (Jamming, Codec, Flag). Each row has its
/// own input and Save button, and Save goes through IConfigToolsOrchestrator.SetServiceIntervalMinutesAsync.
/// </summary>
public class ServicesSettingsIntervalTests : OperatorSessionGateTestBase
{
    private readonly Mock<IConfigToolsOrchestrator> _orchestrator = new();

    public ServicesSettingsIntervalTests()
    {
        _ = _orchestrator
            .Setup(o => o.SetServiceIntervalMinutesAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "saved"));
        _ = Services.AddScoped(_ => _orchestrator.Object);
    }

    [Fact]
    public async Task ServicesSettings_SignedIn_RendersOneMinuteInputPerService()
    {
        await SignInOperatorAsync();
        IRenderedComponent<ServicesSettingsPage> component = Render<ServicesSettingsPage>();

        Assert.Single(component.FindAll("[data-testid='jamming-interval-minutes']"));
        Assert.Single(component.FindAll("[data-testid='codec-interval-minutes']"));
        Assert.Single(component.FindAll("[data-testid='flag-interval-minutes']"));
    }

    [Theory]
    [InlineData("Jamming", "jamming", 20)]
    [InlineData("Codec", "codec", 30)]
    [InlineData("Flag", "flag", 45)]
    public async Task ServicesSettings_SaveMinutes_CallsOrchestratorWithServiceAndTypedMinutes(string service, string testIdPrefix, int minutes)
    {
        await SignInOperatorAsync();
        IRenderedComponent<ServicesSettingsPage> component = Render<ServicesSettingsPage>();

        component.Find($"[data-testid='{testIdPrefix}-interval-minutes']").Change(minutes.ToString());
        component.Find($"[data-testid='{testIdPrefix}-interval-save']").Click();

        component.WaitForAssertion(() => _orchestrator.Verify(
            o => o.SetServiceIntervalMinutesAsync(It.IsAny<IForensicsConfiguration>(), service, minutes, It.IsAny<CancellationToken>()),
            Times.Once));
    }
}
