
using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>Filtering to several devices yields one combined audit list and one verdict covering all of them.</summary>
public class ChainOfCustody_Combined_Tests : BunitContext
{
    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IReportGenerationService> _reportService = new();

    public ChainOfCustody_Combined_Tests()
    {
        // Registrations happen here, before anything resolves a service: bUnit rejects late registrations.
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(_ => _deviceRepository.Object);
        _ = Services.AddScoped(_ => _reportService.Object);
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    [Fact]
    public async Task ReportServiceThrows_ShowsErrorAlertInsteadOfFailingThePage()
    {
        _ = _deviceRepository
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database offline"));
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

        IRenderedComponent<ChainOfCustody> page = Render<ChainOfCustody>();

        AngleSharp.Dom.IElement error = Assert.Single(page.FindAll("[data-testid='chain-report-error']"));
        Assert.Contains("database offline", error.TextContent);
    }

    [Fact]
    public async Task FilteredToTwoDevices_ShowsOneGlobalVerdict()
    {
        var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var frontLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "F", Name = "Front" };
        var backLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "B", Name = "Back" };
        var front = new Device { Id = Guid.NewGuid(), LocationId = frontLocation.Id, ProviderDeviceId = "f", Name = "Front", Type = "camera" };
        var back = new Device { Id = Guid.NewGuid(), LocationId = backLocation.Id, ProviderDeviceId = "b", Name = "Back", Type = "camera" };

        _ = _deviceRepository
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([front, back]);
        // The page first loads the unfiltered whole-database report before the filter is applied.
        _ = _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = true });
        _ = _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(front.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = false, ChainVerificationStatus = "hash mismatch" });
        _ = _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(back.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = false, ChainVerificationStatus = "hash mismatch" });

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Location>> { [account.Id] = new[] { frontLocation, backLocation } });
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

        IRenderedComponent<ChainOfCustody> page = Render<ChainOfCustody>();

        // The filter lives in the right panel, which the page hands to RightPanelContentService. Rendering that
        // fragment keeps its callbacks bound to the page under test.
        RenderFragment? panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        IRenderedComponent<ContainerFragment> panel = Render(panelContent!);
        panel.Find("[data-testid='provider-filter']").Change("Ring");
        IRenderedComponent<ChainOfCustody> component = page;

        // One verdict for the whole selection, not one per device, and it names the device that failed.
        AngleSharp.Dom.IElement alert = Assert.Single(component.FindAll(".alert-danger"));
        Assert.Contains("hash mismatch", alert.TextContent);
        Assert.Empty(component.FindAll(".alert-success"));
        _reportService.Verify(
            m => m.BuildChainOfCustodyReportAsync(front.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _reportService.Verify(
            m => m.BuildChainOfCustodyReportAsync(back.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BrokenChain_ShowsBreakReasonEntriesCheckedAndFailedEntry()
    {
        var failedId = Guid.NewGuid();
        _ = _deviceRepository
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport
            {
                ChainIntegrityVerified = false,
                ChainVerificationStatus = "Chain integrity check failed",
                ChainBreak = ChainBreakKind.EntryHashMismatch,
                ChainEntriesChecked = 4,
                ChainFailedEntryId = failedId,
                ChainFailedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            });
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

        IRenderedComponent<ChainOfCustody> page = Render<ChainOfCustody>();

        AngleSharp.Dom.IElement failed = Assert.Single(page.FindAll("[data-testid='chain-integrity-failed']"));
        Assert.Contains("altered after it was written", failed.TextContent);
        Assert.Contains("4 entries verified before the break", failed.TextContent);
        Assert.Contains(failedId.ToString("N")[..8], failed.TextContent);
    }

    [Fact]
    public async Task BrokenChain_ReverifyButton_RunsTheReportAgain()
    {
        _ = _deviceRepository
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = false, ChainBreak = ChainBreakKind.PreviousHashMismatch });
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

        IRenderedComponent<ChainOfCustody> page = Render<ChainOfCustody>();
        page.Find("[data-testid='chain-reverify']").Click();

        // Once when the page loads, once from the re-verify action.
        _reportService.Verify(
            m => m.BuildChainOfCustodyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
