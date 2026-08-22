using System;

namespace VideoForensics.Providers.Ring.Exceptions
{
    /// <summary>
    /// Exception thrown when an attempt to authenticate failed
    /// </summary>
    public class AuthenticationFailedException : Exception
    {
        public AuthenticationFailedException() : base("Authentication of the session failed")
        {
        }

        public AuthenticationFailedException(string message) : base(message)
        {
        }

        public AuthenticationFailedException(Exception innerException) : base("Authentication of the session failed", innerException)
        {
        }
    }
}
