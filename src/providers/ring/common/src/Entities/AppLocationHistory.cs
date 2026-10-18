using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// One message of the app location history feed (GET .../rs/history). The response is a JSON array of these.
    /// Each message describes a device state change; body[].device is an empty object in every capture and has no fields.
    /// </summary>
    public class AppLocationHistoryMessage
    {
        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("datatype")]
        public string DataType { get; set; }

        [JsonPropertyName("src")]
        public string Src { get; set; }

        [JsonPropertyName("context")]
        public AppLocationHistoryContext Context { get; set; }

        [JsonPropertyName("body")]
        public List<AppLocationHistoryBody> Body { get; set; }

        [JsonPropertyName("seq")]
        public int Seq { get; set; }
    }

    /// <summary>The context object of a message.</summary>
    public class AppLocationHistoryContext
    {
        [JsonPropertyName("eventId")]
        public string EventId { get; set; }

        [JsonPropertyName("eventPublishedTsMs")]
        public long? EventPublishedTsMs { get; set; }

        [JsonPropertyName("requestSeq")]
        public long? RequestSeq { get; set; }

        [JsonPropertyName("affectedEntityId")]
        public string AffectedEntityId { get; set; }

        [JsonPropertyName("deviceFamily")]
        public string DeviceFamily { get; set; }

        [JsonPropertyName("initiatingEntityType")]
        public string InitiatingEntityType { get; set; }

        [JsonPropertyName("affectedEntityType")]
        public string AffectedEntityType { get; set; }

        [JsonPropertyName("forwarderTopic")]
        public string ForwarderTopic { get; set; }

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; }

        [JsonPropertyName("requestSessionId")]
        public long? RequestSessionId { get; set; }

        [JsonPropertyName("eventLevel")]
        public int? EventLevel { get; set; }

        [JsonPropertyName("initiatingEntityId")]
        public string InitiatingEntityId { get; set; }

        [JsonPropertyName("eventOccurredTsMs")]
        public long? EventOccurredTsMs { get; set; }

        [JsonPropertyName("affectedEntityName")]
        public string AffectedEntityName { get; set; }

        [JsonPropertyName("programId")]
        public string ProgramId { get; set; }
    }

    /// <summary>One device entry of a message body.</summary>
    public class AppLocationHistoryBody
    {
        [JsonPropertyName("general")]
        public AppLocationHistoryGeneral General { get; set; }

        [JsonPropertyName("context")]
        public AppLocationHistoryBodyContext Context { get; set; }

        [JsonPropertyName("impulse")]
        public AppLocationHistoryImpulseContainer Impulse { get; set; }
    }

    /// <summary>The general object of a body entry.</summary>
    public class AppLocationHistoryGeneral
    {
        [JsonPropertyName("v2")]
        public AppLocationHistoryGeneralV2 V2 { get; set; }
    }

    /// <summary>The general.v2 fields of a body entry.</summary>
    public class AppLocationHistoryGeneralV2
    {
        [JsonPropertyName("deviceType")]
        public string DeviceType { get; set; }

        [JsonPropertyName("catalogId")]
        public string CatalogId { get; set; }

        [JsonPropertyName("zid")]
        public string Zid { get; set; }

        [JsonPropertyName("lastUpdate")]
        public long? LastUpdate { get; set; }

        [JsonPropertyName("adapterType")]
        public string AdapterType { get; set; }
    }

    /// <summary>The context object of a body entry.</summary>
    public class AppLocationHistoryBodyContext
    {
        [JsonPropertyName("v1")]
        public AppLocationHistoryBodyContextV1 V1 { get; set; }
    }

    /// <summary>The context.v1 fields of a body entry. The adapter field is null in every capture and has no known shape.</summary>
    public class AppLocationHistoryBodyContextV1
    {
        [JsonPropertyName("batteryStatus")]
        public string BatteryStatus { get; set; }

        [JsonPropertyName("subCategoryId")]
        public int? SubCategoryId { get; set; }

        [JsonPropertyName("device")]
        public AppLocationHistoryDeviceContainer Device { get; set; }

        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; }

        [JsonPropertyName("categoryId")]
        public int? CategoryId { get; set; }

        [JsonPropertyName("roomName")]
        public string RoomName { get; set; }
    }

    /// <summary>The device object of a body entry, versioned.</summary>
    public class AppLocationHistoryDeviceContainer
    {
        [JsonPropertyName("v1")]
        public AppLocationHistoryDeviceV1 V1 { get; set; }
    }

    /// <summary>The device state of a body entry: the active mode, the per-mode settings and the siren state.</summary>
    public class AppLocationHistoryDeviceV1
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("modes")]
        public AppLocationHistoryModes Modes { get; set; }

        [JsonPropertyName("siren")]
        public AppLocationHistorySiren Siren { get; set; }
    }

    /// <summary>The per-mode settings of a device state. Each mode is optional in the payload.</summary>
    public class AppLocationHistoryModes
    {
        [JsonPropertyName("all")]
        public AppLocationHistoryModeSettings All { get; set; }

        [JsonPropertyName("some")]
        public AppLocationHistoryModeSettings Some { get; set; }

        [JsonPropertyName("none")]
        public AppLocationHistoryModeSettings None { get; set; }
    }

    /// <summary>The settings of one mode.</summary>
    public class AppLocationHistoryModeSettings
    {
        [JsonPropertyName("transitionDelay")]
        public int? TransitionDelay { get; set; }

        [JsonPropertyName("editable")]
        public bool? Editable { get; set; }

        [JsonPropertyName("removable")]
        public bool? Removable { get; set; }

        [JsonPropertyName("chirpsEnabled")]
        public bool? ChirpsEnabled { get; set; }

        [JsonPropertyName("entryDelay")]
        public int? EntryDelay { get; set; }
    }

    /// <summary>The siren object of a device state.</summary>
    public class AppLocationHistorySiren
    {
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    /// <summary>The impulse object of a body entry.</summary>
    public class AppLocationHistoryImpulseContainer
    {
        [JsonPropertyName("v1")]
        public List<AppLocationHistoryImpulse> V1 { get; set; }
    }

    /// <summary>One impulse of a body entry.</summary>
    public class AppLocationHistoryImpulse
    {
        [JsonPropertyName("impulseType")]
        public string ImpulseType { get; set; }

        [JsonPropertyName("data")]
        public AppLocationHistoryImpulseData Data { get; set; }
    }

    /// <summary>The data object of an impulse: the command type and its arguments.</summary>
    public class AppLocationHistoryImpulseData
    {
        [JsonPropertyName("commandType")]
        public string CommandType { get; set; }

        [JsonPropertyName("data")]
        public AppLocationHistoryImpulseArguments Data { get; set; }
    }

    /// <summary>The arguments of an impulse command.</summary>
    public class AppLocationHistoryImpulseArguments
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }
    }
}
