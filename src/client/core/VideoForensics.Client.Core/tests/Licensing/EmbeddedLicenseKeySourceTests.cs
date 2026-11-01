using VideoForensics.Client.Core.Licensing;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Licensing
{
    /// <summary>
    /// Tests for <see cref="EmbeddedLicenseKeySource"/>: the Syncfusion key embedded in the host assembly by CI.
    /// </summary>
    public class EmbeddedLicenseKeySourceTests
    {
        /// <summary>Verifies that an assembly without the embedded key reads as no key, so local builds fall through to user secrets.</summary>
        [Fact]
        public void ReadKey_NoEmbeddedResource_ReturnsNull()
        {
            var source = new EmbeddedLicenseKeySource(typeof(EmbeddedLicenseKeySourceTests).Assembly);

            Assert.Null(source.ReadKey());
        }

        /// <summary>Verifies that a null assembly is rejected at construction, not at read time.</summary>
        [Fact]
        public void Constructor_NullAssembly_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EmbeddedLicenseKeySource(null!));
        }
    }
}
