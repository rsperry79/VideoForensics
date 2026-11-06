using System.Reflection;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Core.Licensing
{
    /// <summary>
    /// Reads the Syncfusion key embedded in a host assembly as the manifest resource <c>syncfusion-license.key</c>.
    /// CI writes that file from the SYNCFUSION_LICENSE_KEY secret before building the WebApp. Local builds have no
    /// such file, so this source reads as absent and the registrar falls through to user secrets.
    /// </summary>
    public sealed class EmbeddedLicenseKeySource : ILicenseKeySource
    {
        private const string ResourceName = "syncfusion-license.key";

        private readonly Assembly _assembly;

        /// <param name="assembly">Host assembly that carries the embedded key resource. The resource itself is optional.</param>
        public EmbeddedLicenseKeySource(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            _assembly = assembly;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Synchronous on purpose: this runs once at startup, before the host has a running request pipeline,
        /// and the registrar's entry point is synchronous.
        /// </remarks>
        public string? ReadKey()
        {
            using Stream? stream = _assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            string trimmed = reader.ReadToEnd().Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
