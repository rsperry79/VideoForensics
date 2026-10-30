namespace VideoForensics.Client.Ui.Shared.Services.Review
{
    /// <summary>
    /// Lets the right sidebar's Grab still button start a capture that the timeline tab owns. The tab holds the
    /// single-camera player (the playback position) and the capture state, so it publishes its handler and the reason
    /// Grab still is unavailable. The sidebar only reads them. Nothing is captured until the handler runs.
    /// </summary>
    public sealed class ReviewStillCommand
    {
        private Func<Task>? _grab;

        /// <summary>Raised when the reason or the handler changes, so the sidebar button re-reads them.</summary>
        public event Action? OnChange;

        /// <summary>Why Grab still does not apply right now, or null when it does.</summary>
        public string? Reason { get; private set; }

        /// <summary>True when a timeline tab has published a handler and nothing blocks the capture.</summary>
        public bool CanGrab => _grab is not null && Reason is null;

        /// <summary>Runs the published capture. Does nothing when <see cref="CanGrab"/> is false.</summary>
        public Task GrabAsync()
        {
            return CanGrab ? _grab!() : Task.CompletedTask;
        }

        /// <summary>
        /// Publishes the current reason and handler. The owner calls this after each render. Subscribers are notified
        /// only when the reason changes, so a steady render does not refresh the sidebar.
        /// </summary>
        public void Publish(string? reason, Func<Task>? grab)
        {
            _grab = grab;
            if (reason == Reason)
            {
                return;
            }

            Reason = reason;
            OnChange?.Invoke();
        }
    }
}
