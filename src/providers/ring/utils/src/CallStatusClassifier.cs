using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Decides how a finished endpoint call is recorded in index.json. An alarm-hub-only endpoint
    /// (NotApplicableWithoutAlarmHub) that returns 404 is "not-applicable" on a camera-only account,
    /// so it is not counted as a failure; every other failure, and any 404 on a regular endpoint,
    /// stays "failed". A RequiresPushRegistration endpoint that answers 422 "Client Device required"
    /// is likewise "not-applicable".
    /// </summary>
    public static class CallStatusClassifier
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string NotApplicable = "not-applicable";

        private const string ClientDeviceRequiredMarker = "Client Device required";

        public static string Classify(EndpointDescriptor descriptor, bool success, IEnumerable<int> httpStatusCodes)
            => Classify(descriptor, success, httpStatusCodes.Select(code => (StatusCode: code, Body: (string?)null)));

        /// <summary>
        /// Body-aware form: a RequiresPushRegistration endpoint answering 422 "Client Device required" is
        /// "not-applicable" because Ring only accepts that call once a push device is registered.
        /// </summary>
        public static string Classify(EndpointDescriptor descriptor, bool success, IEnumerable<(int StatusCode, string? Body)> calls)
        {
            if (success)
            {
                return Passed;
            }

            var calledList = calls.ToList();

            if (descriptor.NotApplicableWithoutAlarmHub && calledList.Any(c => c.StatusCode == 404))
            {
                return NotApplicable;
            }

            if (descriptor.RequiresPushRegistration && calledList.Any(c => c.StatusCode == 422
                    && c.Body != null && c.Body.Contains(ClientDeviceRequiredMarker, StringComparison.OrdinalIgnoreCase)))
            {
                return NotApplicable;
            }

            return Failed;
        }
    }
}
