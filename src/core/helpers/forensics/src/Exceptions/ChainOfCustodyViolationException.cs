using System;

namespace VideoForensics.Core.Helpers.Forensics.Exceptions
{
    /// <summary>
    /// Thrown when chain of custody integrity is violated or cannot be verified.
    /// </summary>
    public class ChainOfCustodyViolationException : Exception
    {
        public ChainOfCustodyViolationException(string message) : base(message) { }
        public ChainOfCustodyViolationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
