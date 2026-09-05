using System;

namespace VideoForensics.Forensics.Exceptions
{
    /// <summary>
    /// Thrown when forensic analysis operations fail.
    /// </summary>
    public class ForensicAnalysisException : Exception
    {
        public ForensicAnalysisException(string message) : base(message) { }
        public ForensicAnalysisException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
