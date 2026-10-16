using VideoForensics.Ui.Shared.Layout;

namespace VideoForensics.WebApp.Contracts
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
    }
}
