using VideoForensics.Providers.Ring;

using System;

namespace VideoForensics.Providers.Ring.Tests.Mocks
{
    /// <summary>
    /// Helper class for creating mock Ring API sessions for testing
    /// </summary>
    public class MockSessionHelper
    {
        private readonly MockHttpMessageHandler _mockHandler;

        public MockSessionHelper()
        {
            _mockHandler = new MockHttpMessageHandler();
        }

        /// <summary>
        /// Creates a session with a mock HTTP handler
        /// </summary>
        public Session CreateSessionWithMockHandler(string username = "test@example.com", string password = "testpass")
        {
            return new Session(username, password, _mockHandler);
        }

        /// <summary>
        /// Gets the underlying mock handler for custom response setup
        /// </summary>
        public MockHttpMessageHandler GetMockHandler() => _mockHandler;

        /// <summary>
        /// Sets up a mock response for a specific URL
        /// </summary>
        public void SetupMockResponse(string urlFragment, string responseBody, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _mockHandler.SetupResponse(urlFragment, statusCode, responseBody);
        }
    }
}
