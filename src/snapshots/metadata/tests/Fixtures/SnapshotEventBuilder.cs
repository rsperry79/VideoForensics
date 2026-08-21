using Ring.Api.Entities;

namespace Ring.Api.Snapshots.Metadata.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for creating test snapshot events.
    /// </summary>
    public class SnapshotEventBuilder
    {
        private long? _id = (long)new System.Random().Next();
        private string _kind = "motion";
        private DateTime _createdAt = DateTime.UtcNow;
        private Doorbot? _doorbot;
        private CvProperties? _cvProperties;

        public SnapshotEventBuilder WithId(long id)
        {
            _id = id;
            return this;
        }

        public SnapshotEventBuilder WithKind(string kind)
        {
            _kind = kind;
            return this;
        }

        public SnapshotEventBuilder WithCreatedAt(DateTime createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public SnapshotEventBuilder WithDoorbot(Doorbot doorbot)
        {
            _doorbot = doorbot;
            return this;
        }

        public SnapshotEventBuilder WithCvProperties(CvProperties cvProperties)
        {
            _cvProperties = cvProperties;
            return this;
        }

        public SnapshotEventBuilder WithPersonDetection(bool detected, int confidence = 95)
        {
            if (_cvProperties == null)
            {
                _cvProperties = new CvProperties();
            }

            _cvProperties.PersonDetected = detected;
            _cvProperties.Similarity = confidence;
            _cvProperties.DetectionType = "person";

            return this;
        }

        public SnapshotEventBuilder WithMotionDetection(bool detected)
        {
            if (_cvProperties == null)
            {
                _cvProperties = new CvProperties();
            }

            if (detected)
            {
                _cvProperties.DetectionType = "motion";
            }

            return this;
        }

        public SnapshotEventBuilder WithDefaultDoorbot()
        {
            _doorbot = new Doorbot
            {
                Id = 123456789,
                Description = "Front Door",
                Kind = "doorbot",
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.0060,
                Address = "123 Main St, New York, NY 10001",
                Health = new Ring.Api.Entities.DeviceHealth
                {
                    Rssi = -50,
                    BatteryPercentage = 95
                }
            };

            return this;
        }

        public DoorbotHistoryEvent Build()
        {
            var doorbot = _doorbot;
            if (doorbot == null)
            {
                WithDefaultDoorbot();
                doorbot = _doorbot!;
            }

            var @event = new DoorbotHistoryEvent
            {
                Id = _id,
                Kind = _kind,
                Doorbot = doorbot,
                CvProperties = _cvProperties
            };

            return @event;
        }
    }
}
