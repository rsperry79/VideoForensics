using System;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Progress notification emitted during credential-based authentication, so a caller can surface
    /// it to the user (e.g. spinner text) without knowing the internal retry logic.
    /// </summary>
    public class AuthProgressEventArgs : EventArgs
    {
        public AuthProgressEventArgs(string message, bool isWarning = false)
        {
            Message = message;
            IsWarning = isWarning;
        }

        public string Message { get; }
        public bool IsWarning { get; }
    }
}
