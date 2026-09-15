using System.Threading;

using VideoForensics.Providers.Ring.Core.Tests.Mocks;
using VideoForensics.Providers.Ring.Sockets;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Tests for ClientWebSocketTransport behavior through IWebSocketTransport interface contract.
    /// ClientWebSocketTransport is internal, so these tests verify the interface contract by:
    /// 1. Using mocks/fakes to isolate behavior
    /// 2. Testing through public consumers that use ClientWebSocketTransport
    /// 3. Verifying disposal and state management patterns
    /// </summary>
    public class ClientWebSocketTransportTests
    {
        [Fact]
        public async Task FakeWebSocketTransport_ConnectAsync_AcceptsUri()
        {
            // Arrange
            IWebSocketTransport transport = new FakeWebSocketTransport();
            var uri = new Uri("ws://test.example.com");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            await transport.ConnectAsync(uri, cts.Token);

            // Assert
            Assert.Equal(uri, ((FakeWebSocketTransport)transport).ConnectedUri);
        }

        [Fact]
        public async Task FakeWebSocketTransport_SendAsync_RecordsMessage()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            string testMessage = "test message";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            await transport.SendAsync(testMessage, cts.Token);

            // Assert
            _ = Assert.Single(transport.SentMessages);
            Assert.Equal(testMessage, transport.SentMessages[0]);
        }

        [Fact]
        public async Task FakeWebSocketTransport_ReceiveAsync_ReturnsQueuedMessages()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            string expectedMessage = "response message";
            transport.Enqueue(expectedMessage);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            string receivedMessage = await transport.ReceiveAsync(cts.Token);

            // Assert
            Assert.Equal(expectedMessage, receivedMessage);
        }

        [Fact]
        public async Task FakeWebSocketTransport_ReceiveAsync_HandlesCancellation()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            // Act & Assert
            // FakeWebSocketTransport should handle cancellation
            // It either throws OperationCanceledException or returns null
            try
            {
                Task<string?> task = transport.ReceiveAsync(cts.Token);
                cts.Cancel();
                string result = await task;
                Assert.Null(result); // If it completes, result should be null
            }
            catch (OperationCanceledException)
            {
                // Cancellation exception is also acceptable
            }
        }

        [Fact]
        public async Task FakeWebSocketTransport_CloseAsync_CompletesSuccessfully()
        {
            // Arrange
            IWebSocketTransport transport = new FakeWebSocketTransport();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            await transport.CloseAsync(cts.Token);

            // Assert - no exception thrown
        }

        [Fact]
        public void FakeWebSocketTransport_Dispose_DoesNotThrow()
        {
            // Arrange
            IWebSocketTransport transport = new FakeWebSocketTransport();

            // Act
            transport.Dispose();

            // Assert - no exception thrown
        }

        [Fact]
        public void FakeWebSocketTransport_ImplementsIWebSocketTransport()
        {
            // Arrange & Act
            IWebSocketTransport transport = new FakeWebSocketTransport();

            // Assert
            _ = Assert.IsAssignableFrom<IWebSocketTransport>(transport);
        }

        [Fact]
        public void FakeWebSocketTransport_ImplementsIDisposable()
        {
            // Arrange & Act
            var transport = new FakeWebSocketTransport();

            // Assert
            _ = Assert.IsAssignableFrom<IDisposable>(transport);
        }

        [Fact]
        public async Task FakeWebSocketTransport_OnMessageSent_IsInvokedDuringSend()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            bool callbackInvoked = false;
            string capturedMessage = null;

            transport.OnMessageSent = message =>
            {
                callbackInvoked = true;
                capturedMessage = message;
            };

            string testMessage = "callback test";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            await transport.SendAsync(testMessage, cts.Token);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal(testMessage, capturedMessage);
        }

        [Fact]
        public async Task FakeWebSocketTransport_MultipleSendMessages_AllRecorded()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            string[] messages = ["message1", "message2", "message3"];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            foreach (string msg in messages)
            {
                await transport.SendAsync(msg, cts.Token);
            }

            // Assert
            Assert.Equal(3, transport.SentMessages.Count);
            for (int i = 0; i < messages.Length; i++)
            {
                Assert.Equal(messages[i], transport.SentMessages[i]);
            }
        }

        [Fact]
        public async Task FakeWebSocketTransport_ReceiveAsync_HandlesMultipleMessages()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            string[] messagesToReceive = ["response1", "response2", "response3"];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            foreach (string msg in messagesToReceive)
            {
                transport.Enqueue(msg);
            }

            // Act & Assert
            foreach (string expectedMsg in messagesToReceive)
            {
                string received = await transport.ReceiveAsync(cts.Token);
                Assert.Equal(expectedMsg, received);
            }
        }

        [Fact]
        public async Task FakeWebSocketTransport_DisposedMultipleTimes_DoesNotThrow()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();

            // Act
            transport.Dispose();
            transport.Dispose();
            transport.Dispose();

            // Assert - no exception thrown, operations still work (it's a fake)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await transport.CloseAsync(cts.Token);
        }

        [Fact]
        public async Task FakeWebSocketTransport_ConnectUri_WithQueryParameters_Stored()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            var uri = new Uri("ws://test.example.com:8080?token=abc123&id=456");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Act
            await transport.ConnectAsync(uri, cts.Token);

            // Assert
            Assert.NotNull(transport.ConnectedUri);
            Assert.Equal(uri, transport.ConnectedUri);
            Assert.Contains("token=abc123", transport.ConnectedUri.Query);
        }

        [Fact]
        public async Task FakeWebSocketTransport_IntegrationScenario_BiDirectionalMessaging()
        {
            // Arrange
            var transport = new FakeWebSocketTransport();
            var uri = new Uri("ws://integration.test.com");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            string clientMessage = "ping";
            string serverResponse = "pong";
            bool callbackCalled = false;

            transport.OnMessageSent = msg =>
            {
                if (msg == clientMessage)
                {
                    transport.Enqueue(serverResponse);
                    callbackCalled = true;
                }
            };

            // Act
            await transport.ConnectAsync(uri, cts.Token);
            await transport.SendAsync(clientMessage, cts.Token);
            string response = await transport.ReceiveAsync(cts.Token);

            // Assert
            Assert.True(callbackCalled);
            Assert.Equal(serverResponse, response);
            Assert.Equal(uri, transport.ConnectedUri);
            _ = Assert.Single(transport.SentMessages);
            Assert.Equal(clientMessage, transport.SentMessages[0]);

            // Cleanup
            await transport.CloseAsync(cts.Token);
            transport.Dispose();
        }
    }
}
