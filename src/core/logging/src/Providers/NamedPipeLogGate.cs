using System;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>
    /// Runtime switch for <see cref="NamedPipeLoggerProvider"/>. Starts closed, so no pipe exists until a host opens it.
    /// Closing it disposes the pipe server, which disconnects any connected Logger Viewer client.
    /// </summary>
    public sealed class NamedPipeLogGate
    {
        private volatile bool _isOpen;

        /// <summary>True while the provider may create the pipe and write entries to it.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Raised when <see cref="IsOpen"/> changes, so the provider can release the pipe as soon as it closes.</summary>
        public event EventHandler? Changed;

        /// <summary>Opens or closes the gate. Setting the current value again does nothing.</summary>
        public void SetOpen(bool isOpen)
        {
            if (_isOpen == isOpen)
            {
                return;
            }

            _isOpen = isOpen;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
