using System;

namespace VideoForensics.Forensics
{
    /// <summary>
    /// Thrown when evidence integrity checks fail, indicating potential data tampering
    /// or corruption.
    /// </summary>
    public class EvidenceIntegrityException : Exception
    {
        public EvidenceIntegrityException(string message) : base(message) { }
        public EvidenceIntegrityException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
