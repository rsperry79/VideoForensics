using System;
using System.Text.Json.Serialization;

namespace VideoForensics.Utils.LoggerViewer.Maui.Models
{
    /// <summary>
    /// Represents a single log entry received from IPC (named pipe or Unix socket).
    /// </summary>
    public class LogEntry
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("level")]
        public string Level { get; set; } = "Information";

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Category { get; set; }

        [JsonPropertyName("eventId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EventId { get; set; }

        [JsonPropertyName("eventName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EventName { get; set; }

        /// <summary>
        /// Returns a formatted display string for the log entry.
        /// </summary>
        public override string ToString()
        {
            var timestamp = Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            var category = string.IsNullOrEmpty(Category) ? string.Empty : $" [{Category}]";
            return $"{timestamp} [{Level,8}]{category} {Message}";
        }
    }
}
