namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Thrown when a live-view start is refused because the number of live streams has reached the cap.
    /// The web API answers 409 with <see cref="Code"/> in the body, so the client can show a localized message
    /// without matching the server's text.
    /// </summary>
    public sealed class LiveViewCapReachedException : InvalidOperationException
    {
        /// <summary>Typed error code carried in the 409 body.</summary>
        public const string Code = "LiveViewCapReached";

        /// <summary>Creates the exception with the server's message.</summary>
        public LiveViewCapReachedException(string message)
            : base(message)
        {
        }
    }
}
