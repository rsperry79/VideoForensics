namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Thrown when a live-view start is refused because the provider's API call budget is used up.
    /// The web API answers 409 with <see cref="Code"/> in the body, so the client can show a localized message
    /// without matching the server's text.
    /// </summary>
    public sealed class ProviderBudgetExceededException : InvalidOperationException
    {
        /// <summary>Typed error code carried in the 409 body.</summary>
        public const string Code = "ProviderBudgetExceeded";

        /// <summary>Creates the exception with the server's message.</summary>
        public ProviderBudgetExceededException(string message)
            : base(message)
        {
        }
    }
}
