using Bogus;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for creating test snapshot events using Bogus Faker.
    /// </summary>
    public class SnapshotEventBuilder
    {
        private readonly Faker<DoorbotHistoryEvent> _faker;

        public SnapshotEventBuilder()
        {
            _faker = new Faker<DoorbotHistoryEvent>()
                .RuleFor(e => e.Id, f => f.Random.Long(1, 1000000))
                .RuleFor(e => e.Kind, f => "motion")
                .RuleFor(e => e.CreatedAt, f => f.Date.Recent().ToString("o")) // Ring API returns ISO-8601 datetime strings, not DateTime objects
                .RuleFor(e => e.Answered, f => false)
                .RuleFor(e => e.Favorite, f => false)
                .RuleFor(e => e.Doorbot, f => GetDefaultDoorbot()) // Default Doorbot includes realistic test values
                .RuleFor(e => e.CvProperties, f => null as CvProperties);
        }

        public SnapshotEventBuilder WithId(long id)
        {
            _faker.RuleFor(e => e.Id, f => id);
            return this;
        }

        public SnapshotEventBuilder WithKind(string kind)
        {
            _faker.RuleFor(e => e.Kind, f => kind);
            return this;
        }

        public SnapshotEventBuilder WithCreatedAt(DateTime createdAt)
        {
            // Ring API returns ISO-8601 datetime strings, not DateTime objects
            _faker.RuleFor(e => e.CreatedAt, f => createdAt.ToString("o"));
            return this;
        }

        public SnapshotEventBuilder WithDoorbot(Doorbot doorbot)
        {
            _faker.RuleFor(e => e.Doorbot, f => doorbot);
            return this;
        }

        public SnapshotEventBuilder WithCvProperties(CvProperties cvProperties)
        {
            _faker.RuleFor(e => e.CvProperties, f => cvProperties);
            return this;
        }

        public SnapshotEventBuilder WithPersonDetection(bool detected, int confidence = 95)
        {
            _faker.RuleFor(e => e.CvProperties, (f, evt) =>
            {
                CvProperties cvProps = evt.CvProperties ?? new CvProperties();
                cvProps.PersonDetected = detected;
                cvProps.Similarity = confidence;
                cvProps.DetectionType = "person";
                return cvProps;
            });
            return this;
        }

        public SnapshotEventBuilder WithMotionDetection(bool detected)
        {
            _faker.RuleFor(e => e.CvProperties, (f, evt) =>
            {
                CvProperties cvProps = evt.CvProperties ?? new CvProperties();
                if (detected)
                {
                    cvProps.DetectionType = "motion";
                }
                return cvProps;
            });
            return this;
        }

        public SnapshotEventBuilder WithDefaultDoorbot()
        {
            _faker.RuleFor(e => e.Doorbot, f => GetDefaultDoorbot());
            return this;
        }

        private static Doorbot GetDefaultDoorbot()
        {
            return new Doorbot
            {
                Id = 123456789,
                Description = "Front Door",
                Kind = "doorbot",
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.0060,
                Address = "123 Main St, New York, NY 10001",
                Health = new DeviceHealth
                {
                    Rssi = -50,
                    BatteryPercentage = 95
                }
            };
        }

        public DoorbotHistoryEvent Build()
        {
            return _faker.Generate();
        }
    }
}
