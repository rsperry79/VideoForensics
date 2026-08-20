using System;
using System.Net;

namespace KoenZomers.Ring.Api.Exceptions
{
    /// <summary>
    /// Exception thrown when the Ring API returns a different response than it was expected to return
    /// </summary>
    public class UnexpectedOutcomeException : Exception
    {
        /// <summary>
        /// The error message to return when a specific status code was expected
        /// </summary>
        private const string errorMessageWithExpectation = "The Ring API returned a different response {0} than was expected {1}";

        /// <summary>
        /// The error message to return when no specific status code was expected, just any success
        /// </summary>
        private const string errorMessageGeneric = "The Ring API returned an unsuccessful HTTP status code: {0}";

        /// <summary>
        /// The Http Status code that was returned
        /// </summary>
        public readonly HttpStatusCode ReturnedStatusCode;

        /// <summary>
        /// The Http Status code that was expected to be returned. NULL when no specific status code
        /// was expected - just any success (2xx) - and the API returned something else.
        /// </summary>
        public readonly HttpStatusCode? ExpectedStatusCode;

        public UnexpectedOutcomeException(HttpStatusCode returnedStatusCode, HttpStatusCode expectedStatusCode) : base(string.Format(errorMessageWithExpectation, returnedStatusCode, expectedStatusCode))
        {
            ReturnedStatusCode = returnedStatusCode;
            ExpectedStatusCode = expectedStatusCode;
        }

        public UnexpectedOutcomeException(HttpStatusCode returnedStatusCode, HttpStatusCode expectedStatusCode, WebException innerException) : base(string.Format(errorMessageWithExpectation, returnedStatusCode, expectedStatusCode), innerException)
        {
            ReturnedStatusCode = returnedStatusCode;
            ExpectedStatusCode = expectedStatusCode;
        }

        /// <summary>
        /// Thrown when the caller didn't expect a specific status code (just any success), and the
        /// Ring API returned a non-2xx status.
        /// </summary>
        public UnexpectedOutcomeException(HttpStatusCode returnedStatusCode) : base(string.Format(errorMessageGeneric, returnedStatusCode))
        {
            ReturnedStatusCode = returnedStatusCode;
            ExpectedStatusCode = null;
        }
    }
}
