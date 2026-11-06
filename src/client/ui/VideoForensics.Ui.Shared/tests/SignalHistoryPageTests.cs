using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The Signal history page shows the server's signal analysis per device and the flagged RSSI deviations.</summary>
public class SignalHistory_Tests : ScopeRailTestContext
{
    private IRenderedComponent<SignalHistory> RenderPage()
    {
        _ = RegisterDeviceRepository();
        var service = new Mock<ISignalAnalysisService>();
        _ = service
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignalAnalysis(Array.Empty<SignalDeviceAnalysis>()));
        _ = Services.AddScoped(_ => service.Object);
        _ = Services.AddScoped<SignalDrillTrail>();
        var flags = new Mock<IMediaFlagRepository>();
        _ = flags
            .Setup(r => r.ListPageAsync(It.IsAny<MediaFlagFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaFlagPage(Array.Empty<MediaFlag>(), 0));
        _ = Services.AddScoped(_ => flags.Object);
        return Render<SignalHistory>();
    }

    [Fact]
    public void SignalHistory_Renders_TheFlaggedMediaTable()
    {
        IRenderedComponent<SignalHistory> page = RenderPage();

        FlaggedMediaTable table = page.FindComponent<FlaggedMediaTable>().Instance;

        Assert.NotNull(table);
        Assert.Equal(new[] { MediaFlagType.RssiDeviation }, table.Types);
    }
}
