using System.Reflection;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests
{
    public class RemoteLockoutPolicyServiceTests
    {
        [Fact]
        public async Task GetAsync_ReturnsLockoutPolicySettings()
        {
            // This test verifies that RemoteLockoutPolicyService implements ILockoutPolicyService
            // and has the GetAsync method with correct signature
            _ = new RemoteLockoutPolicyService(new HttpClient() { BaseAddress = new Uri("http://localhost") });

            // Verify the method exists and is accessible
            MethodInfo? method = typeof(RemoteLockoutPolicyService).GetMethod("GetAsync");
            Assert.NotNull(method);
            Assert.True(method?.IsPublic);
            Assert.Equal(typeof(Task<LockoutPolicySettings>), method?.ReturnType);
        }

        [Fact]
        public async Task UpdateAsync_MethodExists()
        {
            // This test verifies that RemoteLockoutPolicyService has the UpdateAsync method
            _ = new RemoteLockoutPolicyService(new HttpClient() { BaseAddress = new Uri("http://localhost") });

            // Verify the method exists and is accessible
            MethodInfo? method = typeof(RemoteLockoutPolicyService).GetMethod("UpdateAsync");
            Assert.NotNull(method);
            Assert.True(method?.IsPublic);
        }

        [Fact]
        public void ImplementsILockoutPolicyService()
        {
            // Verify that RemoteLockoutPolicyService implements ILockoutPolicyService
            var service = new RemoteLockoutPolicyService(new HttpClient() { BaseAddress = new Uri("http://localhost") });
            _ = Assert.IsAssignableFrom<Contracts.ILockoutPolicyService>(service);
        }

        [Fact]
        public async Task GetAsync_AutoBanFields_AreMappedFromServerDto()
        {
            // Arrange: server returns the four auto-ban fields
            string json = """
                {"maxFailedAttempts":5,"lockoutDurationMinutes":15,"blockedCountryCodes":null,
                 "failClosedOnLookupError":false,"updatedAtUtc":"2026-10-10T00:00:00Z",
                 "autoBanEnabled":true,"autoBanDistinctAccountThreshold":4,
                 "autoBanWindowMinutes":90,"autoBanDurationMinutes":2880}
                """;
            var handler = new StubHandler(json);
            var service = new RemoteLockoutPolicyService(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

            // Act
            LockoutPolicySettings settings = await service.GetAsync(CancellationToken.None);

            // Assert
            Assert.True(settings.AutoBanEnabled);
            Assert.Equal(4, settings.AutoBanDistinctAccountThreshold);
            Assert.Equal(90, settings.AutoBanWindowMinutes);
            Assert.Equal(2880, settings.AutoBanDurationMinutes);
        }

        [Fact]
        public async Task UpdateAsync_AutoBanFields_AreForwardedToServer()
        {
            // Arrange
            var handler = new StubHandler("{}");
            var service = new RemoteLockoutPolicyService(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
            var settings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                AutoBanEnabled = true,
                AutoBanDistinctAccountThreshold = 4,
                AutoBanWindowMinutes = 90,
                AutoBanDurationMinutes = 2880,
                UpdatedAtUtc = DateTime.UtcNow
            };

            // Act
            await service.UpdateAsync(settings, CancellationToken.None);

            // Assert: the request body carries all four fields
            Assert.NotNull(handler.LastRequestBody);
            using var doc = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!);
            System.Text.Json.JsonElement root = doc.RootElement;
            Assert.True(root.GetProperty("autoBanEnabled").GetBoolean());
            Assert.Equal(4, root.GetProperty("autoBanDistinctAccountThreshold").GetInt32());
            Assert.Equal(90, root.GetProperty("autoBanWindowMinutes").GetInt32());
            Assert.Equal(2880, root.GetProperty("autoBanDurationMinutes").GetInt32());
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly string _responseJson;

            public StubHandler(string responseJson)
            {
                _responseJson = responseJson;
            }

            public string? LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Content != null)
                {
                    LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
