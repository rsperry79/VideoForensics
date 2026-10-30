namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Runs an action once, <c>delay</c> after the last <see cref="Schedule"/>. Every Schedule restarts the wait, so a burst of
/// edits produces one run. <see cref="Cancel"/> and <see cref="Dispose"/> drop a pending run.
/// </summary>
/// <remarks>
/// The action runs on a thread-pool continuation, not the UI thread. A Blazor caller must marshal with <c>InvokeAsync</c>
/// inside the action.
/// </remarks>
public sealed class DebouncedAction : IDisposable
{
    /// <summary>The delay the review sidebar controls use: one second.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(1000);

    private readonly TimeSpan _delay;
    private readonly Func<Task> _action;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayer;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>Creates a debounce around an async action. <paramref name="delayer"/> replaces <see cref="Task.Delay(TimeSpan, CancellationToken)"/> in tests.</summary>
    public DebouncedAction(TimeSpan delay, Func<Task> action, Func<TimeSpan, CancellationToken, Task>? delayer = null)
    {
        _delay = delay;
        _action = action;
        _delayer = delayer ?? Task.Delay;
    }

    /// <summary>Creates a debounce around a synchronous action.</summary>
    public DebouncedAction(TimeSpan delay, Action action, Func<TimeSpan, CancellationToken, Task>? delayer = null)
        : this(delay, () =>
        {
            action();
            return Task.CompletedTask;
        }, delayer)
    {
    }

    /// <summary>Starts, or restarts, the wait. The previous pending run is dropped.</summary>
    public void Schedule()
    {
        CancellationToken token;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            CancelCore();
            _cts = new CancellationTokenSource();
            token = _cts.Token;
        }

        _ = RunAsync(token);
    }

    /// <summary>Drops the pending run, if any.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            CancelCore();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            CancelCore();
        }
    }

    private void CancelCore()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await _delayer(_delay, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        await _action().ConfigureAwait(false);
    }
}
