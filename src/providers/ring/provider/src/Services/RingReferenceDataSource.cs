using System.Collections.Generic;
using System.Linq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Catalog;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Feeds the Ring model catalog into the database as a versioned reference source. The seeder runs it on database generation
    /// and again whenever <see cref="RingModelCatalog.Version"/> is higher than the stored version.
    /// </summary>
    public sealed class RingReferenceDataSource : IReferenceDataSource
    {
        /// <summary>Feature code for devices that carry a camera.</summary>
        public const string CameraFeature = "camera";

        /// <summary>Feature code for chimes.</summary>
        public const string ChimeFeature = "chime";

        /// <summary>Feature code for devices that extend the Wi-Fi network (the Chime Pro).</summary>
        public const string WifiBridgeFeature = "wifi_bridge";

        private readonly IRingModelCatalog _catalog;

        /// <summary>Creates the source over the given catalog.</summary>
        public RingReferenceDataSource(IRingModelCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <inheritdoc />
        public string Name => "Ring";

        /// <inheritdoc />
        public int Version => RingModelCatalog.Version;

        /// <inheritdoc />
        public ReferenceDataSet GetDataSet()
        {
            var models = new List<DeviceModel>();
            var links = new List<DeviceModelFeature>();

            foreach (RingModelInfo info in _catalog.All)
            {
                models.Add(new DeviceModel
                {
                    Kind = info.Kind,
                    Provider = Name,
                    Family = info.Family.ToString(),
                    ModelName = info.ModelName,
                    Generation = info.Generation,
                });

                // A model can have several features (the Chime Pro is both a chime and a Wi-Fi bridge), so each feature is its own link row.
                if (info.HasCamera)
                {
                    links.Add(new DeviceModelFeature { DeviceModelKind = info.Kind, FeatureCode = CameraFeature });
                }

                if (info.Family == RingDeviceFamily.Chime)
                {
                    links.Add(new DeviceModelFeature { DeviceModelKind = info.Kind, FeatureCode = ChimeFeature });
                }

                if (info.IsWifiBridge)
                {
                    links.Add(new DeviceModelFeature { DeviceModelKind = info.Kind, FeatureCode = WifiBridgeFeature });
                }
            }

            var features = new List<DeviceFeatureDefinition>
            {
                new() { Code = CameraFeature, Name = "Camera" },
                new() { Code = ChimeFeature, Name = "Chime" },
                new() { Code = WifiBridgeFeature, Name = "Wi-Fi bridge" },
            };

            return new ReferenceDataSet(models, features, links);
        }
    }
}
