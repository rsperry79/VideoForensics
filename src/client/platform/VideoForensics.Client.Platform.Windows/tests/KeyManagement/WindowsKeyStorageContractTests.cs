using System.Runtime.Versioning;

using VideoForensics.Client.Platform.Windows.KeyManagement;
using VideoForensics.Core.Helpers.Forensics.KeyManagement;
using VideoForensics.Core.Helpers.Forensics.Tests.KeyManagement;

namespace VideoForensics.Client.Platform.Windows.Tests.KeyManagement
{
    /// <summary>
    /// Runs the shared IKeyStorageProvider contract against the Windows DPAPI provider.
    /// </summary>
    internal class DpapiKeyStorageProviderContractTests : IKeyStorageProviderContractTests
    {
        protected override IKeyStorageProvider CreateProvider()
        {
            return new DpapiKeyStorageProvider();
        }
    }

    /// <summary>
    /// Runs the shared IKeyStorageProvider contract against the TPM 2.0 provider.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class TpmKeyStorageProviderContractTests : IKeyStorageProviderContractTests
    {
        protected override IKeyStorageProvider CreateProvider()
        {
            return new TpmKeyStorageProvider();
        }
    }
}
