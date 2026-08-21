namespace Ring.Api.Video.Metadata.Tests.Fixtures
{
    /// <summary>
    /// Builder for creating test DoorbotHistoryEvent instances.
    /// </summary>
    public class DoorbotHistoryEventBuilder
    {
        private DoorbotHistoryEvent _event;
        private Doorbot _doorbot;
        private CvProperties _cvProperties;

        public DoorbotHistoryEventBuilder()
        {
            Reset();
        }

        public static DoorbotHistoryEventBuilder Create() => new();

        public DoorbotHistoryEventBuilder WithId(long id)
        {
            _event.Id = id;
            return this;
        }

        public DoorbotHistoryEventBuilder WithCreatedAt(DateTime dateTime)
        {
            _event.CreatedAt = dateTime.ToString("o");
            return this;
        }

        public DoorbotHistoryEventBuilder WithKind(string kind)
        {
            _event.Kind = kind;
            return this;
        }

        public DoorbotHistoryEventBuilder WithAnswered(bool answered)
        {
            _event.Answered = answered;
            return this;
        }

        public DoorbotHistoryEventBuilder WithFavorite(bool favorite)
        {
            _event.Favorite = favorite;
            return this;
        }

        public DoorbotHistoryEventBuilder WithDoorbot(Action<DoorbotBuilder> action)
        {
            var builder = new DoorbotBuilder(_doorbot);
            action(builder);
            _doorbot = builder.Build();
            _event.Doorbot = _doorbot;
            return this;
        }

        public DoorbotHistoryEventBuilder WithCvProperties(Action<CvPropertiesBuilder> action)
        {
            var builder = new CvPropertiesBuilder(_cvProperties);
            action(builder);
            _cvProperties = builder.Build();
            _event.CvProperties = _cvProperties;
            return this;
        }

        public DoorbotHistoryEvent Build()
        {
            return _event;
        }

        private void Reset()
        {
            _event = new DoorbotHistoryEvent
            {
                Id = 1,
                CreatedAt = DateTime.Now.ToString("o"),
                Kind = "motion",
                Answered = false,
                Favorite = false
            };

            _doorbot = new Doorbot
            {
                Id = 1,
                DeviceId = "aacdef123456",
                Description = "Front Door",
                Kind = "doorbot",
                TimeZone = "America/New_York",
                Address = "123 Main St",
                Latitude = 40.7128,
                Longitude = -74.0060,
                Health = new DeviceHealth { BatteryPercentage = 85, Rssi = -45.5 }
            };

            _cvProperties = null!;
            _event.Doorbot = _doorbot;
        }
    }

    /// <summary>
    /// Builder for Doorbot instances.
    /// </summary>
    public class DoorbotBuilder
    {
        private readonly Doorbot _doorbot;

        public DoorbotBuilder(Doorbot? doorbot = null)
        {
            _doorbot = doorbot ?? new Doorbot();
        }

        public DoorbotBuilder WithDescription(string description)
        {
            _doorbot.Description = description;
            return this;
        }

        public DoorbotBuilder WithAddress(string address)
        {
            _doorbot.Address = address;
            return this;
        }

        public DoorbotBuilder WithLatitude(double latitude)
        {
            _doorbot.Latitude = latitude;
            return this;
        }

        public DoorbotBuilder WithLongitude(double longitude)
        {
            _doorbot.Longitude = longitude;
            return this;
        }

        public DoorbotBuilder WithBatteryHealth(int? percentage, double? rssi)
        {
            _doorbot.Health ??= new DeviceHealth();
            _doorbot.Health.BatteryPercentage = percentage;
            _doorbot.Health.Rssi = rssi;
            return this;
        }

        public Doorbot Build() => _doorbot;
    }

    /// <summary>
    /// Builder for CvProperties instances.
    /// </summary>
    public class CvPropertiesBuilder
    {
        private readonly CvProperties _cvProperties;

        public CvPropertiesBuilder(CvProperties? cvProperties = null)
        {
            _cvProperties = cvProperties ?? new CvProperties();
        }

        public CvPropertiesBuilder WithPersonDetected(bool detected)
        {
            _cvProperties.PersonDetected = detected;
            return this;
        }

        public CvPropertiesBuilder WithDetectionType(string detectionType)
        {
            _cvProperties.DetectionType = detectionType;
            return this;
        }

        public CvPropertiesBuilder WithSimilarity(double similarity)
        {
            _cvProperties.Similarity = similarity;
            return this;
        }

        public CvPropertiesBuilder WithStreamBroken(bool broken)
        {
            _cvProperties.StreamBroken = broken;
            return this;
        }

        public CvProperties Build() => _cvProperties;
    }
}
