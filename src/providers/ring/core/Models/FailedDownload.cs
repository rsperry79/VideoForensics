using System;

namespace VideoForensics.Providers.Ring.Models
{
    public class FailedDownload
    {
        public DateTime Timestamp { get; set; }
        public string LocationName { get; set; }
        public string CameraName { get; set; }
        public int CameraId { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ErrorDescription { get; set; }
    }
}
