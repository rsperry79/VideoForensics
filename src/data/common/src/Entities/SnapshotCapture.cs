namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One row per snapshot attempt against a device. Ring gives no capture timestamp, so
    /// <see cref="CapturedAtUtc"/> is the time we sent the request. Only a 200 attempt is
    /// <see cref="IsCurrent"/> and carries a <see cref="MediaFilePath"/>; use <see cref="Create"/>
    /// so that invariant cannot be broken by a caller.
    /// </summary>
    public class SnapshotCapture
    {
        public Guid Id { get; private set; }

        /// <summary>Internal device GUID (FK to Device), not the Ring numeric id.</summary>
        public Guid DeviceId { get; private set; }

        /// <summary>When the request was sent, from our clock.</summary>
        public DateTimeOffset CapturedAtUtc { get; private set; }

        /// <summary>"fresh" (trigger then read) or "latest" (read the previous frame).</summary>
        public string Trigger { get; private set; } = string.Empty;

        public int HttpStatus { get; private set; }

        /// <summary>True only when <see cref="HttpStatus"/> is 200.</summary>
        public bool IsCurrent { get; private set; }

        /// <summary>Path relative to the snapshot root. Set only when <see cref="IsCurrent"/> is true.</summary>
        public string? MediaFilePath { get; private set; }

        /// <summary>Link to the DeviceHealth row captured for the same tick, when one exists.</summary>
        public Guid? DeviceHealthId { get; private set; }

        private SnapshotCapture()
        {
        }

        /// <summary>Creates a capture attempt record. Throws if the trigger, status, or media path is inconsistent.</summary>
        public static SnapshotCapture Create(
            Guid deviceId,
            DateTimeOffset capturedAtUtc,
            string trigger,
            int httpStatus,
            string? mediaFilePath,
            Guid? deviceHealthId)
        {
            // Case-sensitive on purpose: the trigger is a wire/storage enum, not free text.
            if (trigger is not ("fresh" or "latest"))
            {
                throw new ArgumentException("Trigger must be \"fresh\" or \"latest\".", nameof(trigger));
            }

            // HTTP status codes are 100-599; anything else is a caller bug, not a failed capture.
            if (httpStatus is < 100 or > 599)
            {
                throw new ArgumentOutOfRangeException(nameof(httpStatus), httpStatus, "HTTP status must be between 100 and 599.");
            }

            bool isCurrent = httpStatus == 200;
            bool hasMediaPath = !string.IsNullOrWhiteSpace(mediaFilePath);

            // A non-200 attempt produced no media; a 200 must have media to be "current".
            return !isCurrent && mediaFilePath is not null
                ? throw new ArgumentException("A non-200 capture attempt cannot have a media file path.", nameof(mediaFilePath))
                : isCurrent && !hasMediaPath
                ? throw new ArgumentException("A 200 capture must have a media file path.", nameof(mediaFilePath))
                : new SnapshotCapture
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    // Store the instant in UTC so ordering by the column is correct across offsets.
                    CapturedAtUtc = capturedAtUtc.ToUniversalTime(),
                    Trigger = trigger,
                    HttpStatus = httpStatus,
                    IsCurrent = isCurrent,
                    MediaFilePath = isCurrent ? mediaFilePath : null,
                    DeviceHealthId = deviceHealthId,
                };
        }
    }
}
