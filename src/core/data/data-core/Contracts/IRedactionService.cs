namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Redaction levels for exporting evidence to external parties.</summary>
    public enum RedactionLevel
    {
        /// <summary>No redaction; full data in the exported file.</summary>
        None = 0,

        /// <summary>Mask email local-part and phone digits.</summary>
        Light = 1,

        /// <summary>Mask email, phone, location address, and coordinates.</summary>
        Medium = 2,

        /// <summary>Mask email, phone, location, coordinates, recognized-person names, and device GPS.</summary>
        Heavy = 3
    }

    /// <summary>Service for redacting sensitive information from report DTOs for external export.</summary>
    public interface IRedactionService
    {
        /// <summary>
        /// Creates a redacted copy of a report DTO, never mutating the original.
        /// Applied only when exporting reports to external parties; in-app views always show full, unredacted data.
        /// </summary>
        T RedactForExport<T>(T reportDto, RedactionLevel level) where T : class;
    }
}
