using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.WebApp.Api;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class OperatorCredentialRegisterTests
    {
        private static Operator CreateOperator(
            bool isPrimarySuperAdmin = false,
            DateTime? createdAtUtc = null,
            DateTime? passwordUpdatedAtUtc = null)
        {
            DateTime created = createdAtUtc ?? DateTime.UtcNow.AddDays(-30);
            return new Operator
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test Operator",
                Username = "testoperator",
                FirstName = "Test",
                LastName = "Operator",
                Email = "test.operator@example.com",
                CreatedAtUtc = created,
                PasswordUpdatedAtUtc = passwordUpdatedAtUtc,
                IsPrimarySuperAdmin = isPrimarySuperAdmin
            };
        }

        [Fact]
        public void CredentialRegisterComplete_PrimarySuperAdminNoApprovedPasskey_AutoApproves()
        {
            Operator op = CreateOperator(isPrimarySuperAdmin: true);

            bool result = CredentialApprovalPolicy.ShouldAutoApprove(op, hasApprovedCredential: false);

            Assert.True(result);
        }

        [Fact]
        public void CredentialRegisterComplete_OperatorWithApprovedPasskey_LeavesNewPasskeyUnapproved()
        {
            Operator primary = CreateOperator(isPrimarySuperAdmin: true);
            Operator reset = CreateOperator(
                createdAtUtc: DateTime.UtcNow.AddDays(-30),
                passwordUpdatedAtUtc: DateTime.UtcNow.AddDays(-1));

            Assert.False(CredentialApprovalPolicy.ShouldAutoApprove(primary, hasApprovedCredential: true));
            Assert.False(CredentialApprovalPolicy.ShouldAutoApprove(reset, hasApprovedCredential: true));
        }

        [Fact]
        public void CredentialRegisterComplete_NonPrimaryOperatorNoPasswordReset_LeavesUnapproved()
        {
            Operator op = CreateOperator(isPrimarySuperAdmin: false, passwordUpdatedAtUtc: null);

            bool result = CredentialApprovalPolicy.ShouldAutoApprove(op, hasApprovedCredential: false);

            Assert.False(result);
        }

        [Fact]
        public void CredentialRegisterComplete_OperatorAfterPasswordReset_NoApprovedPasskey_AutoApproves()
        {
            DateTime created = DateTime.UtcNow.AddDays(-30);
            Operator op = CreateOperator(
                isPrimarySuperAdmin: false,
                createdAtUtc: created,
                passwordUpdatedAtUtc: created.AddDays(10));

            bool result = CredentialApprovalPolicy.ShouldAutoApprove(op, hasApprovedCredential: false);

            Assert.True(result);
        }

        [Fact]
        public void CredentialRegisterComplete_SelfRegisteredOperatorNoReset_LeavesUnapproved()
        {
            // Self-registration stamps CreatedAtUtc and PasswordUpdatedAtUtc in the same request,
            // a few microseconds apart. That is not a reset and must not auto-approve.
            DateTime created = DateTime.UtcNow.AddDays(-1);
            Operator op = CreateOperator(
                isPrimarySuperAdmin: false,
                createdAtUtc: created,
                passwordUpdatedAtUtc: created.AddMilliseconds(2));

            bool result = CredentialApprovalPolicy.ShouldAutoApprove(op, hasApprovedCredential: false);

            Assert.False(result);
        }
    }
}
