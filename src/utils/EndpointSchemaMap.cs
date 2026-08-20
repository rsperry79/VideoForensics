using System;
using System.Collections.Generic;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api.Utils
{
    /// <summary>
    /// Maps each API endpoint to its expected entity type(s) for schema validation.
    /// Endpoints that deserialize directly into entity types are mapped; others are skipped (raw JsonElement responses).
    /// </summary>
    public static class EndpointSchemaMap
    {
        private static readonly Dictionary<string, Type> EndpointEntityMap = new()
        {
            // Endpoints with strong schemas - these will be validated
            { "doorbot-health", typeof(DeviceHealthResponse) },
            { "chime-health", typeof(DeviceHealthResponse) },
            { "video-search", typeof(VideoSearchResponse) },
            { "location-events", typeof(LocationEventsResponse) },
            { "profile", typeof(ProfileResponse) },
            { "ringtones", typeof(RingtonesResponse) },

            // Endpoints that return raw JsonElement or untyped responses are skipped (type is object)
            // If more typed wrappers are created for devices, locations, etc., add them here
        };

        public static bool TryGetExpectedType(string endpointKey, out Type? expectedType)
        {
            return EndpointEntityMap.TryGetValue(endpointKey, out expectedType);
        }
    }
}
