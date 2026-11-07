using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Pages;

/// <summary>Tests that the Notices page restores its remembered Provider filter from local storage on load.</summary>
public class NoticesPage_FilterRestore_Tests : BunitContext
{
    private static readonly Guid RingAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid WyzeAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private readonly Guid _operatorId = Guid.NewGuid();
    private readonly Mock<INoticeRepository> _noticeRepository = new();

    public NoticesPage_FilterRestore_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var accounts = new[]
        {
            new ProviderAccount { Id = RingAccountId, UserId = Guid.NewGuid(), ProviderName = "Ring" },
            new ProviderAccount { Id = WyzeAccountId, UserId = Guid.NewGuid(), ProviderName = "Wyze" },
        };
        ScopeOwnershipTestDoubles.Register(Services, accounts, new Dictionary<Guid, IReadOnlyList<Location>>());

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(_ => _noticeRepository.Object);
        _ = Services.AddScoped(_ => Mock.Of<INoticeDismissalRepository>());
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped<RightPanelFilterStore>();
        _ = Services.AddScoped<RightPanelContentService>();

        _noticeRepository
            .Setup(r => r.ListForOperatorAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notice>
            {
                new() { EventType = "DeviceOffline", Details = "ring-notice", TimestampUtc = DateTime.UtcNow, ProviderAccountId = RingAccountId },
                new() { EventType = "DeviceOffline", Details = "wyze-notice", TimestampUtc = DateTime.UtcNow.AddMinutes(-1), ProviderAccountId = WyzeAccountId },
            });

        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public async Task Notices_StoredProviderFilter_IsAppliedOnLoad()
    {
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", _operatorId, "Admin");
        JSInterop.Setup<string?>("vfLayoutPrefs.get", "vf.filters.notices").SetResult("{\"providers\":[\"Wyze\"],\"account\":null}");

        IRenderedComponent<Notices> cut = Render<Notices>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("wyze-notice", cut.Markup);
            Assert.DoesNotContain("ring-notice", cut.Markup);
        });
    }
}
