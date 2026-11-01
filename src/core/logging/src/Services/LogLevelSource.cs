using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// The minimum level the in-memory log buffer records. The host updates it when the saved logging level changes,
    /// so the live log view follows the same level as the console and the audit files.
    /// </summary>
    public sealed class LogLevelSource
    {
        private volatile int _minimum = (int)LogLevel.Trace;

        /// <summary>Entries below this level are not captured.</summary>
        public LogLevel Minimum
        {
            get => (LogLevel)_minimum;
            set => _minimum = (int)value;
        }

        /// <summary>
        /// Category prefixes whose entries are Trace-only: framework noise such as render-tree and localizer lines.
        /// They show only in the live outputs (console and named pipe) and only when <see cref="Minimum"/> is Trace.
        /// They are never stored in files or in the in-memory buffer.
        /// </summary>
        public HashSet<string> TraceOnlyCategories { get; } = new(StringComparer.Ordinal);

        /// <summary>True when the category is in <see cref="TraceOnlyCategories"/>.</summary>
        public bool IsTraceOnly(string category)
        {
            foreach (string prefix in TraceOnlyCategories)
            {
                if (category.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a live output (console or named pipe) shows the entry.</summary>
        public bool AdmitsLive(string category)
        {
            return Minimum == LogLevel.Trace || !IsTraceOnly(category);
        }

        /// <summary>Whether the entry is stored: the audit files and the in-memory buffer. Trace-only entries never are.</summary>
        public bool AdmitsStored(string category)
        {
            return !IsTraceOnly(category);
        }
    }
}
