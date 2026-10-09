namespace VideoForensics.Ui.Shared.Tests;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Tests for which OperatorDetails.razor sections an Admin (not SuperAdmin) viewer sees. The page's operator
/// fetch uses a raw HttpClient rooted at NavigationManager.BaseUri, so the NavigationManager here points at a
/// loopback <see cref="OperatorEndpointProbe"/> to observe whether the operator GET is attempted at all.
/// </summary>
public class OperatorDetailsAdminViewTests : BunitContext
{
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<IAdminOperatorService> _admin = new();
    private readonly PairedSessionState _session;

    public OperatorDetailsAdminViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();

        Services.AddScoped(_ => _admin.Object);
        Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        _session = new PairedSessionState(JSInterop.JSRuntime);
        Services.AddScoped(_ => _session);
        // Singleton factory (not an instance) so the BunitContext service provider disposes the listener at teardown.
        Services.AddSingleton(_ => new OperatorEndpointProbe());
        Services.AddScoped<NavigationManager>(sp => new ProbeNavigationManager(sp.GetRequiredService<OperatorEndpointProbe>()));

        _admin.Setup(s => s.GetUiModeAsync(TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorUiMode("Standard", false));

        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private IStringLocalizer<OperatorDetails> Localizer => Services.GetRequiredService<IStringLocalizer<OperatorDetails>>();

    private async Task SignInAsync(OperatorRole role)
    {
        await _session.SetAsync("test-token", ViewerId, role.ToString());
    }

    private IRenderedComponent<OperatorDetails> RenderPage()
        => Render<OperatorDetails>(p => p.Add(c => c.Id, TargetId));

    [Fact]
    public async Task OperatorDetails_AdminViewer_DoesNotShowAuthErrorAlert()
    {
        await SignInAsync(OperatorRole.Admin);

        var probe = Services.GetRequiredService<OperatorEndpointProbe>();
        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#display-mode-section")));
        // Give any (incorrect) operator fetch time to reach the probe before asserting none did.
        await Task.Delay(300);

        Assert.Empty(cut.FindAll(".alert-danger"));
        Assert.DoesNotContain(Localizer["NotAuthorizedToView"], cut.Markup);
        Assert.DoesNotContain(Localizer["NotAuthorizedMessage"], cut.Markup);
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task OperatorDetails_AdminViewer_DoesNotRenderOperatorDetails()
    {
        await SignInAsync(OperatorRole.Admin);

        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#display-mode-section")));
        var labels = cut.FindAll("label.form-label").Select(l => l.TextContent.Trim()).ToList();
        Assert.DoesNotContain(Localizer["DisplayName"], labels);
        Assert.DoesNotContain(Localizer["ActiveStatus"], labels);
        Assert.Empty(cut.FindAll("input.e-input"));
    }

    [Fact]
    public async Task OperatorDetails_SuperAdminViewer_StillFetchesOperator()
    {
        await SignInAsync(OperatorRole.SuperAdmin);

        var probe = Services.GetRequiredService<OperatorEndpointProbe>();
        var cut = RenderPage();

        cut.WaitForAssertion(() =>
        {
            // The page fetches from both OnInitializedAsync and OnAfterRenderAsync, so assert on the shape of
            // each request rather than the exact count.
            Assert.NotEmpty(probe.RequestLines);
            Assert.All(probe.RequestLines, line => Assert.Contains($"GET /api/devices-management/operators/{TargetId} ", line));
        });
        // The probe answers 403, so the SuperAdmin path keeps its existing not-authorized alert.
        cut.WaitForAssertion(() => Assert.Contains(Localizer["NotAuthorizedToView"], cut.Markup));
    }

    /// <summary>NavigationManager whose BaseUri is the loopback probe, so the page's raw HttpClient hits it.</summary>
    private sealed class ProbeNavigationManager : NavigationManager
    {
        public ProbeNavigationManager(OperatorEndpointProbe probe)
        {
            Initialize(probe.BaseUri, $"{probe.BaseUri}settings/operators/{TargetId}");
        }
    }

    /// <summary>
    /// Loopback HTTP endpoint that records each request line and answers 403 Forbidden. Lets tests observe
    /// whether the page attempted its operator GET without mocking HttpClient.
    /// </summary>
    private sealed class OperatorEndpointProbe : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentQueue<string> _requestLines = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;

        public OperatorEndpointProbe()
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUri = $"http://127.0.0.1:{port}/";
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public string BaseUri { get; }

        public int RequestCount => _requestLines.Count;

        public IReadOnlyList<string> RequestLines => _requestLines.ToArray();

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
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(ct);
                string? header;
                do
                {
                    header = await reader.ReadLineAsync(ct);
                }
                while (!string.IsNullOrEmpty(header));

                _requestLines.Enqueue(requestLine ?? string.Empty);

                var response = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, ct);
            }
            catch (Exception)
            {
                // Client went away mid-request; nothing further to record.
            }
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
