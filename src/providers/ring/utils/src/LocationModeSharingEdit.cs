using System;
using System.Collections.Generic;
using System.Text.Json;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Builds the one-field body the selftest uses to flip Location Mode sharing and then restore it. The sharing
    /// resource holds a single boolean in this client's read shape, so writing only this field cannot overwrite any
    /// other setting the tester did not read.
    /// </summary>
    public static class LocationModeSharingEdit
    {
        /// <summary>The sharing value read before the test step, per location, so the restore sends exactly what was there.</summary>
        public static Dictionary<Guid, bool> Captured { get; set; } = [];

        /// <summary>The value to send in the test step: the opposite of what was captured.</summary>
        public static bool Toggled(bool captured) => !captured;

        /// <summary>The JSON body that sets only <c>sharedUsersEnabled</c>.</summary>
        public static string Body(bool sharedUsersEnabled) =>
            JsonSerializer.Serialize(new { sharedUsersEnabled });
    }
}
