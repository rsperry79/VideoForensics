using System;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Details of a single raw HTTP call made to the Ring API, surfaced for diagnostic logging.
    /// </summary>
    public class RawApiCall
    {
        public DateTime Timestamp { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public int StatusCode { get; set; }
        public string Body { get; set; }
    }

    /// <summary>
    /// A notable lifecycle event in the API layer - authentication, session refresh, retries,
    /// throttling - that isn't a raw HTTP call in its own right but is useful to see on a timeline.
    /// Never carries tokens/credentials.
    /// </summary>
    public class ApiLifecycleEvent
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// The parsed Ring event ("ding") data returned from a history call, re-serialized after
    /// deserialization so consumers can verify what our entity classes actually captured versus
    /// what Ring sent (see RawApiCall.Body for the latter).
    /// </summary>
    public class RingEventsBatch
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string EventsJson { get; set; }
    }

    /// <summary>
    /// Fires events for everything the API layer does that's worth diagnosing: raw HTTP
    /// request/response traffic, lifecycle events (auth, refresh, retry, throttling), and the
    /// parsed Ring event/ding JSON returned from history calls. Consumers subscribe to capture and
    /// persist this however they like, without the API layer needing to know about logging
    /// frameworks or file paths. Authentication calls deliberately never surface response bodies or
    /// tokens through any of these channels.
    /// </summary>
    public static class ApiRawLogger
    {
        public static event Action<RawApiCall> OnRawResponse;
        public static event Action<ApiLifecycleEvent> OnEvent;
        public static event Action<RingEventsBatch> OnRingEvents;

        internal static void Raise(string method, string url, int statusCode, string body)
        {
            OnRawResponse?.Invoke(new RawApiCall
            {
                Timestamp = DateTime.UtcNow,
                Method = method,
                Url = url,
                StatusCode = statusCode,
                Body = body
            });
        }

        internal static void LogEvent(string category, string message)
        {
            OnEvent?.Invoke(new ApiLifecycleEvent
            {
                Timestamp = DateTime.UtcNow,
                Category = category,
                Message = message
            });
        }

        internal static void LogRingEvents(string category, string message, string eventsJson)
        {
            OnRingEvents?.Invoke(new RingEventsBatch
            {
                Timestamp = DateTime.UtcNow,
                Category = category,
                Message = message,
                EventsJson = eventsJson
            });
        }
    }
}
