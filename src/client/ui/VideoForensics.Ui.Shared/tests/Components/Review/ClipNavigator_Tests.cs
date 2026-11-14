using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>The flag navigator: shows the flag total and moves to the previous or next flag by button or arrow key.</summary>
public class ClipNavigator_Tests : BunitContext
{
    private readonly Mock<IMediaFlagRepository> _repository = new();
    private readonly Guid _currentId = Guid.NewGuid();
    private readonly MediaFlagFilterDto _filter = new(new[] { "RssiDeviation" }, null, null, null, null);

    public ClipNavigator_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_repository.Object);
        _ = _repository
            .Setup(r => r.GetPositionAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
    }

    [Fact]
    public void ClipNavigator_ButtonsAreLabelledPreviousAndNext()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);

        IRenderedComponent<ClipNavigator> cut = Render();

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        Assert.Equal("Previous", cut.Find("[data-testid=clip-nav-prev]").TextContent.Trim());
        Assert.Equal("Next", cut.Find("[data-testid=clip-nav-next]").TextContent.Trim());
    }

    [Fact]
    public void ClipNavigator_Readout_PrecedesButtons()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);

        IRenderedComponent<ClipNavigator> cut = Render();

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        Assert.Equal("clip-nav-total", cut.Find("[data-testid=clip-navigator]").Children[0].GetAttribute("data-testid"));
    }

    private static MediaFlag Flag()
    {
        return new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = MediaFlagType.RssiDeviation,
            DeviceId = Guid.NewGuid(),
            WindowStartUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc),
            WindowEndUtc = new DateTime(2026, 10, 30, 12, 5, 0, DateTimeKind.Utc),
            RaisedAtUtc = new DateTime(2026, 10, 30, 12, 6, 0, DateTimeKind.Utc),
        };
    }

    private IRenderedComponent<ClipNavigator> Render(Action<ComponentParameterCollectionBuilder<ClipNavigator>>? extra = null)
    {
        return Render<ClipNavigator>(p =>
        {
            _ = p.Add(c => c.Filter, _filter);
            _ = p.Add(c => c.CurrentFlagId, _currentId);
            extra?.Invoke(p);
        });
    }

    [Fact]
    public void ClipNavigator_Renders_TotalFromCount()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);

        IRenderedComponent<ClipNavigator> cut = Render();

        IElement total = cut.WaitForElement("[data-testid=clip-nav-total]");
        Assert.Equal(TestLocalizer.Create()["ClipNavigatorTotal", 17].Value, total.TextContent.Trim());
    }

    [Fact]
    public void ClipNavigator_Previous_CallsNeighbourPreviousAndNavigates()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);
        MediaFlagDto target = Flag().ToDto();
        _ = _repository
            .Setup(r => r.GetNeighbourAsync(_currentId, _filter, MediaFlagNeighbourDto.Previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        MediaFlagDto? navigated = null;

        IRenderedComponent<ClipNavigator> cut = Render(p => p
            .Add(c => c.OnNavigate, flag => navigated = flag));

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        cut.Find("[data-testid=clip-nav-prev]").Click();

        cut.WaitForAssertion(() => Assert.Same(target, navigated));
        _repository.Verify(r => r.GetNeighbourAsync(_currentId, _filter, MediaFlagNeighbourDto.Previous, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClipNavigator_NoNext_DisablesNextButton()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _ = _repository
            .Setup(r => r.GetNeighbourAsync(_currentId, _filter, MediaFlagNeighbourDto.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFlagDto?)null);

        IRenderedComponent<ClipNavigator> cut = Render();

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        cut.Find("[data-testid=clip-nav-next]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=clip-nav-next]").GetAttribute("disabled")));
    }

    [Fact]
    public void ClipNavigator_ArrowLeft_MovesPrevious()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);
        _ = _repository
            .Setup(r => r.GetNeighbourAsync(_currentId, _filter, MediaFlagNeighbourDto.Previous, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFlagDto?)null);

        IRenderedComponent<ClipNavigator> cut = Render();

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        cut.Find("[data-testid=clip-navigator]").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        cut.WaitForAssertion(() => _repository.Verify(
            r => r.GetNeighbourAsync(_currentId, _filter, MediaFlagNeighbourDto.Previous, It.IsAny<CancellationToken>()),
            Times.Once));
    }

    [Fact]
    public void ClipNavigator_Renders_PositionOfTotal()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);
        _ = _repository
            .Setup(r => r.GetPositionAsync(_currentId, _filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        IRenderedComponent<ClipNavigator> cut = Render();

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        cut.WaitForAssertion(() => Assert.Equal(
            TestLocalizer.Create()["ClipNavigatorPosition", 3, 17].Value,
            cut.Find("[data-testid=clip-nav-total]").TextContent.Trim()));
    }

    [Fact]
    public void ClipNavigator_PositionUnknown_ShowsTotalOnly()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);
        _ = _repository
            .Setup(r => r.GetPositionAsync(_currentId, _filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        IRenderedComponent<ClipNavigator> cut = Render();

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        cut.WaitForAssertion(() => Assert.Equal(
            TestLocalizer.Create()["ClipNavigatorTotal", 17].Value,
            cut.Find("[data-testid=clip-nav-total]").TextContent.Trim()));
    }

    [Fact]
    public void ClipNavigator_AfterMove_RefreshesPosition()
    {
        _ = _repository
            .Setup(r => r.CountAsync(It.IsAny<MediaFlagFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(17);
        MediaFlagDto target = Flag().ToDto();
        _ = _repository
            .Setup(r => r.GetPositionAsync(_currentId, _filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _ = _repository
            .Setup(r => r.GetPositionAsync(target.Id, _filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        _ = _repository
            .Setup(r => r.GetNeighbourAsync(_currentId, _filter, MediaFlagNeighbourDto.Next, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        MediaFlagDto? navigated = null;

        IRenderedComponent<ClipNavigator> cut = Render(p => p
            .Add(c => c.OnNavigate, flag => navigated = flag));

        _ = cut.WaitForElement("[data-testid=clip-nav-total]");
        cut.WaitForAssertion(() => Assert.Equal(
            TestLocalizer.Create()["ClipNavigatorPosition", 3, 17].Value,
            cut.Find("[data-testid=clip-nav-total]").TextContent.Trim()));

        cut.Find("[data-testid=clip-nav-next]").Click();
        cut.WaitForAssertion(() => Assert.Same(target, navigated));

        cut.Render(p => p
            .Add(c => c.Filter, _filter)
            .Add(c => c.CurrentFlagId, target.Id));

        cut.WaitForAssertion(() => Assert.Equal(
            TestLocalizer.Create()["ClipNavigatorPosition", 4, 17].Value,
            cut.Find("[data-testid=clip-nav-total]").TextContent.Trim()));
        _repository.Verify(r => r.GetPositionAsync(target.Id, _filter, It.IsAny<CancellationToken>()), Times.Once);
    }
}
