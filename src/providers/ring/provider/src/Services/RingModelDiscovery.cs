using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Catalog;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Records a Ring device kind the catalog does not list, so the database knows the model exists. Every path that sees a
    /// device's kind calls this, so unknown kinds are recorded whichever path reports them first.
    /// </summary>
    internal static class RingModelDiscovery
    {
        private const string RingProviderName = "Ring";

        private static readonly IRingModelCatalog RingCatalog = new RingModelCatalog();

        /// <summary>Records the kind when it is non-blank and not in the catalog. Known and blank kinds are left alone.</summary>
        /// <param name="data">Data client that writes the discovered row.</param>
        /// <param name="kind">The device's raw Ring kind, null when Ring gave none.</param>
        /// <param name="family">Product family for the kind (see <see cref="FamilyForType"/> and <see cref="FamilyFor"/>).</param>
        /// <param name="ct">Cancellation token.</param>
        public static async Task RecordUnknownKindAsync(IVideoForensicsDataClient data, string? kind, string family, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(kind) || RingCatalog.Find(kind) != null)
            {
                return;
            }

            await data.EnsureDeviceModelAsync(RingProviderName, kind, family, ct);
        }

        /// <summary>Maps a Ring device type (as discovery sets it on the device row) to its product family.</summary>
        public static string FamilyForType(string type)
        {
            return type switch
            {
                "doorbot" or "authorized_doorbot" => RingDeviceFamily.Doorbell.ToString(),
                "stickup_cam" => RingDeviceFamily.Camera.ToString(),
                "chime" => RingDeviceFamily.Chime.ToString(),
                _ => "Unknown",
            };
        }

        /// <summary>
        /// Derives the family from the Ring device list a device appears in, for paths that have the list but not the device row.
        /// Returns "Unknown" when the device is not listed.
        /// </summary>
        public static string FamilyFor(Devices? devices, string providerDeviceId)
        {
            if (devices == null)
            {
                return "Unknown";
            }

            if (devices.Doorbots?.Any(d => d.Id.ToString() == providerDeviceId) == true
                || devices.AuthorizedDoorbots?.Any(d => d.Id.ToString() == providerDeviceId) == true)
            {
                return RingDeviceFamily.Doorbell.ToString();
            }

            if (devices.StickupCams?.Any(d => d.Id?.ToString() == providerDeviceId) == true)
            {
                return RingDeviceFamily.Camera.ToString();
            }

            if (devices.Chimes?.Any(c => c.Id.ToString() == providerDeviceId) == true)
            {
                return RingDeviceFamily.Chime.ToString();
            }

            return "Unknown";
        }
    }
}
