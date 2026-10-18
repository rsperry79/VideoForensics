namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Source feed names stored in the Source column of the shared Ring event detail rows.</summary>
    public static class RingEventSources
    {
        /// <summary>Rows from clients_api/video_search/history.</summary>
        public const string VideoSearch = "video-search";

        /// <summary>Rows from clients_api/locations/{id}/events.</summary>
        public const string LocationEvent = "location-event";

        /// <summary>Rows from the evm location history feed.</summary>
        public const string LocationHistory = "location-history";

        /// <summary>Rows from clients_api/doorbots/history.</summary>
        public const string DoorbotHistory = "doorbot-history";
    }

    /// <summary>
    /// The nested objects of one Ring event (device, properties, placement, geolocation, receipt, last location, siren,
    /// recording and computer vision). Each object is one row in its own table, keyed by the feed in
    /// <see cref="RingEventSources"/> and the id of the event row it belongs to. The writer assigns Source and
    /// SourceRowId, so mappers only set the object's own fields.
    /// </summary>
    public class RingEventDetails
    {
        public RingEventCvProperties? CvProperties { get; set; }

        public RingEventDoorbot? Doorbot { get; set; }

        public RingEventProperties? Properties { get; set; }

        public RingEventDevicePlacement? DevicePlacement { get; set; }

        public RingEventGeolocation? Geolocation { get; set; }

        public RingEventSubscriptionReceipt? SubscriptionReceipt { get; set; }

        public RingEventLastLocation? LastLocation { get; set; }

        public RingEventSiren? Siren { get; set; }

        public RingEventRecording? Recording { get; set; }
    }
}
