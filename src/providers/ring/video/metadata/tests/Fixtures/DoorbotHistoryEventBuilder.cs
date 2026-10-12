namespace VideoForensics.Providers.Ring.Video.Metadata.Tests.Fixtures
{
    using Bogus;

    /// <summary>
    /// Builder for creating test DoorbotHistoryEvent instances using Bogus Faker.
    /// Default values match Ring API patterns (e.g., CreatedAt as ISO-8601 string).
    /// </summary>
    public class DoorbotHistoryEventBuilder
    {
        private readonly Faker<DoorbotHistoryEvent> _faker;
        private Doorbot _doorbot;
        private CvProperties _cvProperties;

        public DoorbotHistoryEventBuilder()
        {
            _faker = new Faker<DoorbotHistoryEvent>()
                .RuleFor(e => e.Id, f => f.Random.Long(1, 1000000))
                .RuleFor(e => e.CreatedAt, f => f.Date.Recent().ToString("o")) // Ring API: CreatedAt is ISO-8601 string, not DateTime
                .RuleFor(e => e.Kind, _ => "motion")
                .RuleFor(e => e.Answered, _ => false)
                .RuleFor(e => e.Favorite, _ => false);

            Reset();
        }

        public static DoorbotHistoryEventBuilder Create()
        {
            return new();
        }

        public DoorbotHistoryEventBuilder WithId(long id)
        {
            _faker.RuleFor(e => e.Id, _ => id);
            return this;
        }

        public DoorbotHistoryEventBuilder WithCreatedAt(DateTime dateTime)
        {
            _faker.RuleFor(e => e.CreatedAt, _ => dateTime.ToString("o"));
            return this;
        }

        public DoorbotHistoryEventBuilder WithKind(string kind)
        {
            _faker.RuleFor(e => e.Kind, _ => kind);
            return this;
        }

        public DoorbotHistoryEventBuilder WithAnswered(bool answered)
        {
            _faker.RuleFor(e => e.Answered, _ => answered);
            return this;
        }

        public DoorbotHistoryEventBuilder WithFavorite(bool favorite)
        {
            _faker.RuleFor(e => e.Favorite, _ => favorite);
            return this;
        }

        public DoorbotHistoryEventBuilder WithDoorbot(Action<DoorbotBuilder> action)
        {
            var builder = new DoorbotBuilder(_doorbot);
            action(builder);
            _doorbot = builder.Build();
            _faker.RuleFor(e => e.Doorbot, _ => _doorbot);
            return this;
        }

        public DoorbotHistoryEventBuilder WithCvProperties(Action<CvPropertiesBuilder> action)
        {
            var builder = new CvPropertiesBuilder(_cvProperties);
            action(builder);
            _cvProperties = builder.Build();
            _faker.RuleFor(e => e.CvProperties, _ => _cvProperties);
            return this;
        }

        public DoorbotHistoryEvent Build()
        {
            return _faker.Generate();
        }

        private void Reset()
        {
            // Default values match Ring API patterns
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
            _faker.RuleFor(e => e.Doorbot, _ => _doorbot);
            _faker.RuleFor(e => e.CvProperties, _ => _cvProperties);
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

        public Doorbot Build()
        {
            return _doorbot;
        }
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

        public CvProperties Build()
        {
            return _cvProperties;
        }
    }
}
