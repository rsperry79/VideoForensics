using System.Collections.Generic;
using System.Linq;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Decides how a finished endpoint call is recorded in index.json. An alarm-hub-only endpoint
    /// (NotApplicableWithoutAlarmHub) that returns 404 is "not-applicable" on a camera-only account,
    /// so it is not counted as a failure; every other failure, and any 404 on a regular endpoint,
    /// stays "failed".
    /// </summary>
    public static class CallStatusClassifier
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string NotApplicable = "not-applicable";

        public static string Classify(EndpointDescriptor descriptor, bool success, IEnumerable<int> httpStatusCodes)
        {
            if (success)
            {
                return Passed;
            }

            if (descriptor.NotApplicableWithoutAlarmHub && httpStatusCodes.Contains(404))
            {
                return NotApplicable;
            }

            return Failed;
        }
    }
}
