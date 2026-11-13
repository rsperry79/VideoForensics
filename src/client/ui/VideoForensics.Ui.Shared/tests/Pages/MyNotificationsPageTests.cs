using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Pages;

/// <summary>Covers the My Notifications page (/settings/my-notifications): loading saved preferences and saving the checked email event types.</summary>
public class MyNotificationsPageTests : BunitContext
{
    private static readonly NotificationPreferencesSummary Saved = new(
        PushEnabled: false,
        MinimumSeverity: 1,
        EmailEnabled: true,
        EmailEventTypes: new[] { SecurityAuditEventTypes.AuthFailure, SecurityAuditEventTypes.PairingRevoked });

    public MyNotificationsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped(sp => new WebPushClient(sp.GetRequiredService<IJSRuntime>(), sp.GetRequiredService<NavigationManager>()));
        Preferences = new Mock<INotificationPreferencesService>();
        _ = Services.AddSingleton(Preferences.Object);
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private Mock<INotificationPreferencesService> Preferences { get; }

    [Fact]
    public void Load_ShowsSavedEmailEventTypesAsChecked()
    {
        // Arrange
        Preferences.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Saved);

        // Act
        IRenderedComponent<MyNotifications> cut = Render<MyNotifications>();

        // Assert - the two saved event types are checked, an unsaved one is not
        cut.WaitForAssertion(() => Assert.True(IsChecked(cut, $"email-event-{SecurityAuditEventTypes.AuthFailure}")));
        Assert.True(IsChecked(cut, $"email-event-{SecurityAuditEventTypes.PairingRevoked}"));
        Assert.False(IsChecked(cut, $"email-event-{SecurityAuditEventTypes.AuthSuccess}"));
    }

    [Fact]
    public async Task Save_SendsTheCheckedEventTypeNames()
    {
        // Arrange
        Preferences.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Saved);
        Preferences.Setup(p => p.SaveAsync(It.IsAny<NotificationPreferencesSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferencesSummary s, CancellationToken _) => s);
        IRenderedComponent<MyNotifications> cut = Render<MyNotifications>();
        cut.WaitForAssertion(() => Assert.True(IsChecked(cut, $"email-event-{SecurityAuditEventTypes.AuthFailure}")));

        // Act
        await SaveButton(cut).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Preferences.Verify(
            p => p.SaveAsync(
                It.Is<NotificationPreferencesSummary>(s => s.EmailEnabled
                    && s.EmailEventTypes.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(
                        new[] { SecurityAuditEventTypes.AuthFailure, SecurityAuditEventTypes.PairingRevoked }.OrderBy(n => n, StringComparer.Ordinal))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static bool IsChecked(IRenderedComponent<MyNotifications> cut, string testId)
    {
        return cut.Find($"[data-testid='{testId}']").QuerySelector("input")?.HasAttribute("checked") ?? false;
    }

    private IElement SaveButton(IRenderedComponent<MyNotifications> cut)
    {
        string save = Services.GetRequiredService<IStringLocalizer<MyNotifications>>()["SaveButton"].Value;
        return cut.FindAll("button").Single(b => b.TextContent.Trim() == save);
    }
}
