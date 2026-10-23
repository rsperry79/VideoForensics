using System;
using System.Text.RegularExpressions;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// Redacts sensitive data from log messages before they enter the buffer.
    /// Removes Bearer tokens, X-StepUp-Token values, and key-value pairs like password=, pwd=, token=, secret=.
    /// </summary>
    public static partial class LogRedactor
    {
        /// <summary>
        /// Redacts sensitive data patterns from the input text.
        /// Returns null if input is null, empty string if input is empty, otherwise redacted text.
        /// </summary>
        public static string? Redact(string? input)
        {
            if (input == null)
                return null;

            if (string.IsNullOrEmpty(input))
                return input;

            string result = input;

            // Redact Bearer tokens: "Bearer <token>"
            result = BearerTokenPattern().Replace(result, "Bearer [REDACTED]");

            // Redact X-StepUp-Token: "X-StepUp-Token: <token>"
            result = StepUpTokenPattern().Replace(result, "X-StepUp-Token: [REDACTED]");

            // Redact key-value pairs: password=, pwd=, token=, secret= (case-insensitive)
            result = KeyValuePasswordPattern().Replace(result, "password=[REDACTED]");
            result = KeyValuePwdPattern().Replace(result, "pwd=[REDACTED]");
            result = KeyValueTokenPattern().Replace(result, "token=[REDACTED]");
            result = KeyValueSecretPattern().Replace(result, "secret=[REDACTED]");

            return result;
        }

        [GeneratedRegex(@"Bearer\s+[^\s]+", RegexOptions.IgnoreCase)]
        private static partial Regex BearerTokenPattern();

        [GeneratedRegex(@"X-StepUp-Token:\s*[^\s&\n]+", RegexOptions.IgnoreCase)]
        private static partial Regex StepUpTokenPattern();

        [GeneratedRegex(@"password\s*=\s*[^\s&\n]+", RegexOptions.IgnoreCase)]
        private static partial Regex KeyValuePasswordPattern();

        [GeneratedRegex(@"pwd\s*=\s*[^\s&\n]+", RegexOptions.IgnoreCase)]
        private static partial Regex KeyValuePwdPattern();

        [GeneratedRegex(@"token\s*=\s*[^\s&\n]+", RegexOptions.IgnoreCase)]
        private static partial Regex KeyValueTokenPattern();

        [GeneratedRegex(@"secret\s*=\s*[^\s&\n]+", RegexOptions.IgnoreCase)]
        private static partial Regex KeyValueSecretPattern();
    }
}
