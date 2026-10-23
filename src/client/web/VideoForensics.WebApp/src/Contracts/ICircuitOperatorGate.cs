using VideoForensics.Client.Ui.Shared.Layout;

namespace VideoForensics.Client.WebApp.Contracts
{
    /// <summary>
    /// Server-side check of a privileged in-process call against the operator behind the current Blazor circuit.
    /// The page table only hides pages; this is what stops a privileged service call that bypasses the page.
    /// </summary>
    public interface ICircuitOperatorGate
    {
        /// <summary>
        /// True when the circuit's operator satisfies <paramref name="policy"/>. Calls made outside any UI circuit
        /// (background sync, startup jobs) are not an operator's action, so they pass through.
        /// </summary>
        bool Allows(AccessPolicy policy);

        /// <summary>
        /// True when <paramref name="stepUpToken"/> is a valid step-up for this circuit's operator. Outside a UI circuit
        /// there is no operator to re-authenticate, so this passes, as <see cref="Allows"/> does.
        /// </summary>
        bool IsStepUpValid(string? stepUpToken);

        /// <summary>
        /// The id of the operator behind this circuit, from the re-validated session token; null when the circuit is not
        /// open or there is no valid session. For audit attribution only; the token itself is never exposed.
        /// </summary>
        Guid? OperatorId { get; }

        /// <summary>The localized message for an operator who does not satisfy <paramref name="policy"/>.</summary>
        string Denial(AccessPolicy policy);

        /// <summary>The localized message for a missing or invalid step-up.</summary>
        string StepUpDenial();
    }
}
