namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Derives the display case type from a case number's prefix.</summary>
    public static class CaseTypeClassifier
    {
        /// <summary>
        /// Returns "Detected", "Suspected", or "Manual" for the matching case number prefix, otherwise "Other".
        /// The prefix is the only source of truth: case numbers are generated with it (see CaseRepository.GenerateCaseNumber).
        /// </summary>
        /// <param name="caseNumber">The case number, which may be null for legacy or malformed rows.</param>
        public static string FromCaseNumber(string? caseNumber)
        {
            if (caseNumber is null)
            {
                return "Other";
            }

            if (caseNumber.StartsWith("detected-", StringComparison.Ordinal))
            {
                return "Detected";
            }

            if (caseNumber.StartsWith("suspected-", StringComparison.Ordinal))
            {
                return "Suspected";
            }

            if (caseNumber.StartsWith("manual-", StringComparison.Ordinal))
            {
                return "Manual";
            }

            return "Other";
        }
    }
}
