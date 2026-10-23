using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards the in-process evidence export that the Export Evidence page runs. The HTTP export also requires
    /// SuperAdminLocal plus step-up; step-up is enforced on that endpoint, not here.
    /// </summary>
    public sealed class GuardedEvidenceExportService(IEvidenceExportService inner, ICircuitOperatorGate gate) : IEvidenceExportService
    {
        public Task<ExportResult> ExportEvidenceAsync(
            Guid caseId,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct,
            string? stepUpToken = null)
        {
            if (!gate.Allows(AccessPolicy.SuperAdminLocal))
            {
                throw new UnauthorizedAccessException(gate.Denial(AccessPolicy.SuperAdminLocal));
            }

            // Matches the HTTP export, which requires step-up through the endpoint filter.
            return !gate.IsStepUpValid(stepUpToken)
                ? throw new UnauthorizedAccessException(gate.StepUpDenial())
                : inner.ExportEvidenceAsync(caseId, recipientDescription, passphrase, ct, stepUpToken);
        }
    }
}
