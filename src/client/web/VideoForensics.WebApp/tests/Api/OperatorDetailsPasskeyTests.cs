using VideoForensics.Client.WebApp.Api;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>The operator details the SuperAdmin page reads carry the passkey setting, so the switch shows its current state.</summary>
    public class OperatorDetailsPasskeyTests
    {
        [Fact]
        public void ToDto_CarriesPasskeyRequiredSetting()
        {
            var op = new Operator
            {
                DisplayName = "Sysop",
                Username = "sysop",
                FirstName = "Sys",
                LastName = "Op",
                Email = "sysop@example.com",
                PasskeyRequired = false,
            };

            Assert.False(op.ToDto().PasskeyRequired);
        }
    }
}
