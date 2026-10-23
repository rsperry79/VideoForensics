using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Decides whether a newly registered operator passkey is approved on creation, or left pending admin approval.
    /// </summary>
    internal static class CredentialApprovalPolicy
    {
        /// <summary>
        /// Registration sets CreatedAtUtc and PasswordUpdatedAtUtc in the same request (a few microseconds apart),
        /// so a PasswordUpdatedAtUtc only counts as a password reset when it is clearly after creation.
        /// </summary>
        private static readonly TimeSpan ResetGracePeriod = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Auto-approves a new passkey only when the operator has no approved, non-revoked passkey yet AND either:
        /// (a) the operator is the primary SuperAdmin (first SuperAdmin account), or
        /// (b) the operator's password was reset after creation (PasswordUpdatedAtUtc is the reset marker), so the
        ///     passkey is the first one since the reset.
        /// Any other case stays unapproved and requires admin approval.
        /// </summary>
        public static bool ShouldAutoApprove(Operator op, bool hasApprovedCredential)
        {
            return !hasApprovedCredential && (op.IsPrimarySuperAdmin || (op.PasswordUpdatedAtUtc is DateTime updated
                && updated - op.CreatedAtUtc > ResetGracePeriod));
        }
    }
}
