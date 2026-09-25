namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Shared setup for SecurityEvents page tests: mocked <see cref="ISecurityEventsService"/> and
/// <see cref="IAdminOperatorService"/>, and a default signed-out <see cref="PairedSessionState"/>
/// (tests override with <see cref="RegisterRoleAsync"/> - later registration wins, per the
/// CasesPagesTests/RingSelfTestPageTests convention).
/// </summary>
public abstract class SecurityEventsPageTestBase : BunitContext
{
    protected static readonly Guid TestOperatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected Mock<ISecurityEventsService> SecurityEventsServiceMock { get; } = new();
    protected Mock<IAdminOperatorService> AdminOperatorServiceMock { get; } = new();

    protected SecurityEventsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        Services.AddScoped(_ => SecurityEventsServiceMock.Object);
        Services.AddScoped(_ => AdminOperatorServiceMock.Object);

        AdminOperatorServiceMock
            .Setup(s => s.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorSummary>());

        // Default: signed out. RegisterRoleAsync overrides this (later registration wins).
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    protected async Task<PairedSessionState> RegisterRoleAsync(OperatorRole role, Guid? operatorId = null)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", operatorId ?? TestOperatorId, role.ToString());
        Services.AddScoped(_ => session);
        return session;
    }

    protected static SecurityEventSummary MakeEvent(Guid? operatorId = null, string eventType = "LoginAttemptSuccess", bool success = true) => new(
        Id: Guid.NewGuid(),
        OperatorId: operatorId ?? TestOperatorId,
        EventType: eventType,
        Success: success,
        IpAddress: "192.168.1.1",
        OccurredAtUtc: new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
        Reason: null);

    protected IRenderedComponent<SecurityEvents> RenderPage() => Render<SecurityEvents>();
}

public class SecurityEventsPage_OwnEvents_Tests : SecurityEventsPageTestBase
{
    [Fact]
    public async Task PageOpen_LoadsAndRendersOwnEvents()
    {
        await RegisterRoleAsync(OperatorRole.Review);

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(null, 0, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEventSummary> { MakeEvent(eventType: "LoginAttemptSuccess") });

        var component = RenderPage();

        // The page reloads on OnAfterRenderAsync(firstRender) after SessionState.EnsureLoadedAsync
        // (the same pattern SecurityDevices.razor/SecurityOperators.razor use, to cope with
        // PairedSessionState only being readable once the interactive circuit connects) - when a test
        // pre-registers an already-signed-in session (unlike a real first page load, where the
        // OnInitializedAsync call is a no-op because the session isn't loaded yet), both the
        // OnInitializedAsync and OnAfterRenderAsync loads go through. What matters here is that it was
        // called with the self-service arguments (operatorId: null, offset 0), not the exact count.
        SecurityEventsServiceMock.Verify(
            s => s.GetEventsAsync(null, 0, 25, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        Assert.Contains("LoginAttemptSuccess", component.Markup);
    }
}

public class SecurityEventsPage_Forbidden_Tests : SecurityEventsPageTestBase
{
    [Fact]
    public async Task ServiceReportsForbidden_ShowsForbiddenMessage()
    {
        await RegisterRoleAsync(OperatorRole.Review);

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(null, 0, 25, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden));

        var component = RenderPage();

        var forbidden = component.Find("[data-testid='security-events-forbidden']");
        Assert.False(string.IsNullOrWhiteSpace(forbidden.TextContent));
        Assert.Empty(component.FindAll("[data-testid='security-events-table']"));
    }
}

public class SecurityEventsPage_SuperAdminOperatorPicker_Tests : SecurityEventsPageTestBase
{
    [Fact]
    public async Task SuperAdmin_SeesOperatorPicker_AndSelectingOperatorQueriesThatOperator()
    {
        await RegisterRoleAsync(OperatorRole.SuperAdmin);

        Guid otherOperatorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        AdminOperatorServiceMock
            .Setup(s => s.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorSummary> { new(otherOperatorId, "Jane Operator", true) });

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(null, 0, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEventSummary>());

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(otherOperatorId, 0, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEventSummary> { MakeEvent(operatorId: otherOperatorId, eventType: "LoginAttemptFailure") });

        var component = RenderPage();

        var picker = (IHtmlSelectElement)component.Find("[data-testid='security-events-operator-picker']");
        Assert.Contains("Jane Operator", component.Markup);

        picker.Change(otherOperatorId.ToString());

        SecurityEventsServiceMock.Verify(
            s => s.GetEventsAsync(otherOperatorId, 0, 25, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Contains("LoginAttemptFailure", component.Markup);
    }

    [Fact]
    public async Task ReadOnly_DoesNotSeeOperatorPicker()
    {
        await RegisterRoleAsync(OperatorRole.ReadOnly);

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(null, 0, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEventSummary>());

        var component = RenderPage();

        Assert.Empty(component.FindAll("[data-testid='security-events-operator-picker']"));
        AdminOperatorServiceMock.Verify(s => s.ListOperatorsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class SecurityEventsPage_Pagination_Tests : SecurityEventsPageTestBase
{
    [Fact]
    public async Task NextPage_CallsServiceWithNextOffset()
    {
        await RegisterRoleAsync(OperatorRole.Review);

        var firstPage = Enumerable.Range(0, 25).Select(_ => MakeEvent()).ToList();

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(null, 0, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage);

        SecurityEventsServiceMock
            .Setup(s => s.GetEventsAsync(null, 25, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEventSummary> { MakeEvent(eventType: "SecondPageEvent") });

        var component = RenderPage();

        var nextButton = (IHtmlButtonElement)component.Find("[data-testid='security-events-next-page']");
        await component.InvokeAsync(() => nextButton.Click());

        SecurityEventsServiceMock.Verify(
            s => s.GetEventsAsync(null, 25, 25, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Contains("SecondPageEvent", component.Markup);
    }
}
