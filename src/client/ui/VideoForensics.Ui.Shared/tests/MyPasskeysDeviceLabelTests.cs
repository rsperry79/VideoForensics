
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Covers the device-label prefill on MyPasskeys: the name field starts with a browser/OS label
/// (for example "Chrome on Windows") and never overwrites text the operator already typed.
/// </summary>
public class MyPasskeysDeviceLabelTests : BunitContext
{
    private const string LabelInputSelector = "input[type='text']";

    // The label each test's vfDeviceLabel.get call resolves with; set through UseDeviceLabel.
    private Task<string?> _deviceLabel = Task.FromResult<string?>(null);

    public MyPasskeysDeviceLabelTests()
    {
        // Registered before any service is resolved so the page and PairedSessionState both get it.
        _ = Services.AddSingleton<IJSRuntime>(new DeviceLabelJSRuntime(() => _deviceLabel));
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        WebAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        _ = Services.AddScoped(_ => WebAuthn.Object);
        _ = Services.AddScoped<StepUpService>();
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private Mock<WebAuthnClient> WebAuthn { get; }

    [Fact]
    public void MyPasskeys_FirstRender_PrefillsNameFromDeviceLabel()
    {
        // Arrange - the browser reports a label
        UseDeviceLabel(Task.FromResult<string?>("Chrome on Windows"));

        // Act
        IRenderedComponent<MyPasskeys> component = Render<MyPasskeys>();

        // Assert
        component.WaitForAssertion(() => Assert.Equal("Chrome on Windows", component.Find(LabelInputSelector).GetAttribute("value")));
    }

    [Fact]
    public async Task MyPasskeys_UserTypedName_IsNotOverwritten()
    {
        // Arrange - the operator types a name before the device label call finishes
        var gate = new TaskCompletionSource<string?>();
        UseDeviceLabel(gate.Task);
        IRenderedComponent<MyPasskeys> component = Render<MyPasskeys>();
        component.Find(LabelInputSelector).Change("My work PC");

        // Act - the label arrives late
        gate.SetResult("Chrome on Windows");
        await FlushAsync(component);

        // Assert
        Assert.Equal("My work PC", component.Find(LabelInputSelector).GetAttribute("value"));
    }

    [Fact]
    public async Task MyPasskeys_DeviceLabelCallFails_LeavesNameEmpty()
    {
        // Arrange - the label call throws (for example, the script is not loaded)
        UseDeviceLabel(Task.FromException<string?>(new JSException("device label unavailable")));

        // Act
        IRenderedComponent<MyPasskeys> component = Render<MyPasskeys>();
        await FlushAsync(component);

        // Assert - the field stays empty and the page still renders its controls
        Assert.True(string.IsNullOrEmpty(component.Find(LabelInputSelector).GetAttribute("value")));
        Assert.NotEmpty(component.FindAll("button"));
    }

    /// <summary>Sets the task that vfDeviceLabel.get resolves with for this test.</summary>
    private void UseDeviceLabel(Task<string?> label)
    {
        _deviceLabel = label;
    }

    /// <summary>Lets queued render continuations run so the component has applied any late result.</summary>
    private static async Task FlushAsync(IRenderedComponent<MyPasskeys> component)
    {
        for (int i = 0; i < 5; i++)
        {
            await component.InvokeAsync(() => Task.CompletedTask);
        }
    }

    /// <summary>
    /// Answers vfDeviceLabel.get from a test-controlled task and returns default for every other JS call,
    /// the same result bUnit's loose JSInterop gives.
    /// </summary>
    private sealed class DeviceLabelJSRuntime(Func<Task<string?>> label) : IJSRuntime
    {
        private const string DeviceLabelFunction = "vfDeviceLabel.get";

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return Invoke<TValue>(identifier);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return Invoke<TValue>(identifier);
        }

        private ValueTask<TValue> Invoke<TValue>(string identifier)
        {
            return identifier == DeviceLabelFunction
                ? AwaitLabelAsync<TValue>(label())
                : ValueTask.FromResult(default(TValue)!);
        }

        private static async ValueTask<TValue> AwaitLabelAsync<TValue>(Task<string?> pending)
        {
            return (TValue)(object)(await pending)!;
        }
    }
}
