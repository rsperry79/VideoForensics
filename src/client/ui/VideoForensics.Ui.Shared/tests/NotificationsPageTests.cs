
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Tests for the system-wide /settings/notifications page. The page's requests use a raw HttpClient rooted at
/// NavigationManager.BaseUri, so the NavigationManager here points at a loopback <see cref="NotificationsApiProbe"/>
/// that answers the two GETs the page makes and lets the success branch render.
/// </summary>
public class NotificationsPageTests : BunitContext
{
    private readonly PairedSessionState _session;

    public NotificationsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddLocalization();

        _session = new PairedSessionState(JSInterop.JSRuntime);
        _ = Services.AddScoped(_ => _session);
        // Singleton factory (not an instance) so the BunitContext service provider disposes the listener at teardown.
        _ = Services.AddSingleton(_ => new NotificationsApiProbe());
        _ = Services.AddScoped<NavigationManager>(sp => new ProbeNavigationManager(sp.GetRequiredService<NotificationsApiProbe>()));
        _ = Services.AddScoped(sp => new WebPushClient(sp.GetRequiredService<IJSRuntime>(), sp.GetRequiredService<NavigationManager>()));

        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    [Fact]
    public async Task Notifications_SystemPage_DoesNotShowPushSection()
    {
        await _session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        IStringLocalizer<Notifications> localizer = Services.GetRequiredService<IStringLocalizer<Notifications>>();

        IRenderedComponent<Notifications> cut = Render<Notifications>();

        // The system-wide urgency section only renders once the probe's answers load, so wait for it before
        // checking that the push section is absent.
        cut.WaitForAssertion(() => Assert.Contains(localizer["UrgentEventOverrides"].Value, cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain(localizer["PushNotifications"].Value, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(localizer["EnablePushNotifications"].Value, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(localizer["CheckConfigButton"].Value, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>NavigationManager whose BaseUri is the loopback probe, so the page's raw HttpClient hits it.</summary>
    private sealed class ProbeNavigationManager : NavigationManager
    {
        public ProbeNavigationManager(NotificationsApiProbe probe)
        {
            Initialize(probe.BaseUri, $"{probe.BaseUri}settings/notifications");
        }
    }

    /// <summary>
    /// Loopback HTTP endpoint that answers the notification-settings GETs the page makes: the caller's access
    /// level (SuperAdmin, so the controls show) and an empty urgency-override list. Anything else gets 404.
    /// </summary>
    private sealed class NotificationsApiProbe : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;

        public NotificationsApiProbe()
        {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = $"http://127.0.0.1:{port}/";
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public string BaseUri { get; }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (Exception)
                {
                    // Listener stopped during teardown.
                    return;
                }

                using (client)
                {
                    await HandleAsync(client, ct);
                }
            }
        }

        private async Task HandleAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                string? requestLine = await reader.ReadLineAsync(ct);
                string? header;
                do
                {
                    header = await reader.ReadLineAsync(ct);
                }
                while (!string.IsNullOrEmpty(header));

                string[] parts = (requestLine ?? string.Empty).Split(' ');
                string path = parts.Length > 1 ? parts[1] : string.Empty;
                string response = path switch
                {
                    "/api/v1/permissions/notifications" => Json("{\"access\":\"SuperAdmin\"}"),
                    "/api/notifications/urgency-overrides" => Json("[]"),
                    _ => "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                };

                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), ct);
            }
            catch (Exception)
            {
                // Client went away mid-request; nothing further to answer.
            }
        }

        private static string Json(string body)
        {
            return $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch (Exception)
            {
                // Loop exits when the listener stops; ignore during teardown.
            }

            _cts.Dispose();
        }
    }
}
