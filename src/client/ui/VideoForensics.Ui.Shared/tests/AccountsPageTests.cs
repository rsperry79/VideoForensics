namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using Xunit;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

/// <summary>
/// Shared setup for the /accounts page tests: repository and auth mocks, the real
/// <see cref="AccountSelectionState"/> / <see cref="PairedSessionState"/> over the bUnit JS runtime,
/// and two linked accounts on different providers (Ring, Wyze). The grid is the real SfGrid; the
/// provider filter lives in the right-panel slot, so tests render the captured
/// <see cref="RightPanelContentService.Content"/> fragment to reach it.
/// </summary>
public abstract class AccountsPageTestBase : BunitContext
{
    protected Mock<IProviderAccountRepository> AccountRepositoryMock { get; } = new();
    protected Mock<IUserRepository> UserRepositoryMock { get; } = new();

    protected static readonly Guid RingAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid WyzeAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    protected AccountsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        var ringUserId = Guid.NewGuid();
        var wyzeUserId = Guid.NewGuid();
        var accounts = new List<ProviderAccountEntity>
        {
            new() { Id = RingAccountId, UserId = ringUserId, ProviderName = "Ring", LinkedUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
            new()
            {
                Id = WyzeAccountId,
                UserId = wyzeUserId,
                ProviderName = "Wyze",
                LinkedUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
                LastErrorMessage = "Token expired",
                LastErrorUtc = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
            },
        };

        AccountRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);
        UserRepositoryMock
            .Setup(r => r.GetAsync(ringUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });
        UserRepositoryMock
            .Setup(r => r.GetAsync(wyzeUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "wyze-key", DisplayName = "Wyze Owner" });

        Services.AddScoped(_ => AccountRepositoryMock.Object);
        Services.AddScoped(_ => UserRepositoryMock.Object);
        Services.AddScoped(_ => new Mock<IProviderAuthService>().Object);
        Services.AddScoped(_ => new Mock<IMultiProviderAuthService>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfiguration>().Object);
        Services.AddScoped(_ => new Mock<IForensicsConfigurationService>().Object);
        Services.AddScoped(sp => new AccountSelectionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new WebAuthnClient(sp.GetRequiredService<IJSRuntime>(), new Mock<ISelfApiHttpClientFactory>().Object));
        Services.AddScoped<RightPanelContentService>();
    }

    protected IRenderedComponent<Accounts> RenderPage() => Render<Accounts>();

    /// <summary>The fragment the page pushed into the right-panel slot; render it with Render(...) to drive its controls.</summary>
    protected RenderFragment RightPanelFragment()
    {
        var slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        return slot.Content!;
    }

}

public class AccountsPage_Grid_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_GridRendered_NoBlankLeadingHeaderColumn()
    {
        var cut = RenderPage();

        var labels = cut.FindAll(".e-headercell .e-headertext");
        Assert.NotEmpty(labels);
        Assert.Equal("Display Name", labels[0].TextContent.Trim());
    }

    [Fact]
    public void Accounts_GridRows_EachRowShowsOnlyDetailsButton()
    {
        var cut = RenderPage();

        var buttonTexts = cut.FindAll(".e-gridcontent tbody td button").Select(b => b.TextContent.Trim()).ToList();

        Assert.Equal(2, buttonTexts.Count);
        Assert.All(buttonTexts, text => Assert.Equal("Details", text));
    }

    [Fact]
    public void Accounts_GridRowWithError_ErrorIconSitsInDisplayNameCell()
    {
        var cut = RenderPage();

        var wyzeRow = cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Wyze Owner"));
        var firstCell = wyzeRow.QuerySelector("td");

        Assert.NotNull(firstCell);
        Assert.NotNull(firstCell!.QuerySelector("i.material-icons"));
    }
}

public class AccountsPage_ProviderFilter_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_ProviderFilterDefault_AllProvidersSelected()
    {
        RenderPage();
        var panel = Render(RightPanelFragment());

        var multiSelect = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();

        Assert.NotNull(multiSelect.Instance.Value);
        Assert.Equal(new[] { "Ring", "Wyze" }.OrderBy(x => x), multiSelect.Instance.Value!.OrderBy(x => x));
    }

    [Fact]
    public async Task Accounts_ProviderSelectionExcludesProvider_HidesThatProvidersRows()
    {
        var cut = RenderPage();
        Assert.Contains("Wyze Owner", cut.Markup);

        var panel = Render(RightPanelFragment());
        var multiSelect = panel.FindComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], string>>();

        await panel.InvokeAsync(() => multiSelect.Instance.ValueChanged.InvokeAsync(new[] { "Ring" }));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ring Owner", cut.Markup);
            Assert.DoesNotContain("Wyze Owner", cut.Markup);
        });
    }
}

public class AccountsPage_ErrorTooltip_Tests : AccountsPageTestBase
{
    [Fact]
    public void Accounts_ErrorWithoutTimestamp_TooltipUsesLocalizedUnknownTime()
    {
        var strings = Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<VideoForensics.Ui.Shared.Pages.Accounts>>();
        var userId = Guid.NewGuid();
        AccountRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProviderAccountEntity>
            {
                new() { Id = RingAccountId, UserId = userId, ProviderName = "Ring", LastErrorMessage = "Token expired" },
            });
        UserRepositoryMock
            .Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { ProviderUserKey = "ring-key", DisplayName = "Ring Owner" });

        var cut = RenderPage();

        var title = cut.Find("i.material-icons").GetAttribute("title");
        Assert.Contains($"(as of {strings["UnknownTime"]})", title);
    }
}
