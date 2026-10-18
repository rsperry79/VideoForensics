namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using VideoForensics.Ui.Shared.Layout;
using Xunit;

/// <summary>
/// Restoring the saved left-nav collapse state on a circuit reconnect. The Syncfusion splitter's JS
/// widget may not be initialised yet, so the imperative CollapseAsync call can throw a JSException.
/// The layout must survive that and retry the collapse on a later render.
/// </summary>
public class MainLayoutLeftNavRestoreTests : SimpleModeLayoutTestBase
{
    // Syncfusion's Splitter JS identifier for CollapseAsync (sfBlazor.Splitter.collapse).
    private const string CollapseIdentifier = "sfBlazor.Splitter.collapse";
    private const string LeftNavKey = "vf.layout.leftNavCollapsed";

    private readonly CollapseJSRuntime _runtime;

    public MainLayoutLeftNavRestoreTests()
    {
        Services.AddLogging();
        // bUnit's sync interop path wraps setup exceptions in InvalidOperationException, so a real
        // JSException is raised by this decorator for the splitter collapse call instead.
        _runtime = new CollapseJSRuntime(JSInterop.JSRuntime);
        Services.AddSingleton<IJSRuntime>(_runtime);
    }

    [Fact]
    public async Task MainLayout_LeftNavRestoreThrowsJSException_RenderDoesNotThrow()
    {
        // Arrange - the saved state says the left nav is collapsed, but the splitter JS is not ready
        JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("true");
        _runtime.FailCollapse = true;

        // Act
        Exception? ex = await Record.ExceptionAsync(async () =>
        {
            IRenderedComponent<MainLayout> component = Render<MainLayout>();
            await component.InvokeAsync(() => Task.CompletedTask);
        });

        // Assert
        Assert.Null(ex);
        Assert.Equal(0, _runtime.SuccessfulCollapses);
    }

    [Fact]
    public async Task MainLayout_LeftNavRestoreFailedOnFirstRender_RetriedOnLaterRenderUntilSuccess()
    {
        // Arrange - the first collapse fails, later ones succeed
        JSInterop.Setup<string?>("vfLayoutPrefs.get", LeftNavKey).SetResult("true");
        _runtime.FailCollapse = true;

        IRenderedComponent<MainLayout> component = Render<MainLayout>();
        await component.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(0, _runtime.SuccessfulCollapses);

        // Act - a later render (the splitter is now ready) retries the collapse; the right-panel toggle re-renders the layout
        _runtime.FailCollapse = false;
        component.Find(".right-panel-toggle").Click();
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert - the retry succeeded
        Assert.Equal(1, _runtime.SuccessfulCollapses);
        int attemptsAfterSuccess = _runtime.CollapseAttempts;

        // Act - another render after success must not collapse again
        component.Find(".right-panel-toggle").Click();
        await component.InvokeAsync(() => Task.CompletedTask);

        // Assert
        Assert.Equal(attemptsAfterSuccess, _runtime.CollapseAttempts);
        Assert.Equal(1, _runtime.SuccessfulCollapses);
    }

    /// <summary>
    /// Delegates every call to bUnit's runtime, except the splitter collapse, which can be made to fail with a JSException.
    /// </summary>
    private sealed class CollapseJSRuntime(IJSRuntime inner) : IJSRuntime
    {
        public bool FailCollapse { get; set; }

        public int CollapseAttempts { get; private set; }

        public int SuccessfulCollapses { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == CollapseIdentifier)
            {
                CollapseAttempts++;
                if (FailCollapse)
                {
                    return ValueTask.FromException<TValue>(new JSException("splitter not ready"));
                }

                SuccessfulCollapses++;
            }

            return inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
    }
}
