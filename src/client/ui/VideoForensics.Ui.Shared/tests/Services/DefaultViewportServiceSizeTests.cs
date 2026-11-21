using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class DefaultViewportServiceSizeTests
{
    [Fact]
    public void DefaultViewportService_OnViewportSizeChanged_RecordsTheSizeAndFiresOnChange()
    {
        var service = new DefaultViewportService(Mock.Of<IJSRuntime>());
        int changes = 0;
        service.OnChange += () => changes++;

        service.OnViewportSizeChanged(1280, 720);

        Assert.Equal(1280, service.WidthPx);
        Assert.Equal(720, service.HeightPx);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void DefaultViewportService_OnViewportSizeChanged_SameSize_DoesNotFireOnChange()
    {
        var service = new DefaultViewportService(Mock.Of<IJSRuntime>());
        service.OnViewportSizeChanged(1280, 720);
        int changes = 0;
        service.OnChange += () => changes++;

        service.OnViewportSizeChanged(1280, 720);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void DefaultViewportService_OnViewportSizeChanged_ZeroEdge_IsIgnored()
    {
        var service = new DefaultViewportService(Mock.Of<IJSRuntime>());
        service.OnViewportSizeChanged(1280, 720);

        service.OnViewportSizeChanged(0, 0);

        Assert.Equal(720, service.HeightPx);
    }
}
