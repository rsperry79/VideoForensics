namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// Tracks whether the startup initialization (DB migrations, Events backfill, persisted config load) has finished.
/// The host starts serving as soon as it can bind, so the Windows service reports Running without waiting for
/// that work; requests made before it completes get a 503 from the startup gate in Program.cs.
/// </summary>
public sealed class StartupInitializationGate
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>True once the initialization has finished, whether or not it succeeded.</summary>
    public bool IsReady => _ready.Task.IsCompleted;

    /// <summary>Marks the initialization finished. Calling it again does nothing.</summary>
    public void MarkReady()
    {
        _ = _ready.TrySetResult();
    }

    /// <summary>Completes once <see cref="MarkReady"/> has been called.</summary>
    public Task WhenReadyAsync(CancellationToken ct)
    {
        return _ready.Task.WaitAsync(ct);
    }
}
