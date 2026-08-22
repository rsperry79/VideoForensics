using System;

namespace VideoForensics.Providers.Ring.Exceptions
{
    /// <summary>
    /// Exception thrown when trying to retrieve information regarding a specific Ring device which could not be found
    /// </summary>
    public class DeviceUnknownException : Exception
    {
        /// <summary>
        /// The error message to return
        /// </summary>
        private const string errorMessage = "The Ring device with Id '{0}' could not be found";

        public DeviceUnknownException() : base(string.Format(errorMessage, "unknown"))
        {
        }

        public DeviceUnknownException(int? ringDeviceId) : base(string.Format(errorMessage, ringDeviceId.HasValue ? ringDeviceId.Value.ToString() : "unknown"))
        {
        }

        public DeviceUnknownException(int? ringDeviceId, System.Net.WebException innerException) : base(string.Format(errorMessage, ringDeviceId.HasValue ? ringDeviceId.Value.ToString() : "unknown"), innerException)
        {
        }

        /// <summary>
        /// Used when a GET request 404s and only the request Uri (not a specific device id) is
        /// known at the throw site - includes the Uri in the message so the resource that
        /// wasn't found is identifiable, rather than always reporting "unknown".
        /// </summary>
        public DeviceUnknownException(Uri requestUri) : base($"The Ring resource requested at '{requestUri}' could not be found (HTTP 404)")
        {
        }
    }
}
