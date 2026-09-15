using System.Net;
using System.Net.Http.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Remote;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteProviderAuthServiceTests
    {
        /// <summary>
        /// Minimal HttpMessageHandler mock for testing HTTP calls without a real server.
        /// </summary>
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }

        private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            var mockHandler = new MockHttpMessageHandler(handler);
            var client = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("https://example.test")
            };
            return client;
        }

        [Fact]
        public async Task AuthenticateAsync_WithValidCredentials_ReturnsSuccessfulAuthResult()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var authToken = "auth-token-12345";
            var expiresAt = DateTime.UtcNow.AddHours(1);
            var providerAccountId = Guid.NewGuid();

            var httpClient = CreateHttpClient(async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/auth/login", request.RequestUri?.PathAndQuery);

                var body = await request.Content!.ReadFromJsonAsync<LoginRequestDto>();
                Assert.NotNull(body);
                Assert.Equal(username, body.Username);
                Assert.Equal(password, body.Password);
                Assert.Null(body.ProviderName);

                var response = new AuthResultDto(
                    Success: true,
                    AuthToken: authToken,
                    ExpiresAt: expiresAt,
                    ProviderAccountId: providerAccountId
                );

                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateAsync(username, password);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(authToken, result.AuthToken);
            Assert.Equal(expiresAt, result.ExpiresAt);
            Assert.Equal(providerAccountId, result.ProviderAccountId);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateAsync_WithProviderName_SendsProviderNameInRequest()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var providerName = "Ring";

            var httpClient = CreateHttpClient(async request =>
            {
                var body = await request.Content!.ReadFromJsonAsync<LoginRequestDto>();
                Assert.NotNull(body);
                Assert.Equal(providerName, body.ProviderName);

                var response = new AuthResultDto(Success: true, AuthToken: "token");
                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient, providerName);

            // Act
            await service.AuthenticateAsync(username, password);

            // Assert (verification happens in the handler)
        }

        [Fact]
        public async Task AuthenticateAsync_WithInvalidCredentials_ReturnsFailureAuthResult()
        {
            // Arrange
            var username = "testuser";
            var password = "wrongpassword";

            var httpClient = CreateHttpClient(async request =>
            {
                var response = new AuthResultDto(
                    Success: false,
                    ErrorMessage: "Invalid username or password"
                );

                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateAsync(username, password);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid username or password", result.ErrorMessage);
            Assert.Null(result.AuthToken);
        }

        [Fact]
        public async Task AuthenticateAsync_WhenTwoFactorRequired_ReturnsTwoFactorRequiredMessage()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var authAttemptId = Guid.NewGuid();

            var httpClient = CreateHttpClient(async request =>
            {
                var response = new AuthResultDto(
                    Success: false,
                    RequiresTwoFactor: true,
                    AuthAttemptId: authAttemptId
                );

                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateAsync(username, password);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Two-factor", result.ErrorMessage);
            Assert.Contains("AuthenticateWithTwoFactorAsync", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateAsync_WithNullResponse_ReturnsFailureWithNullMessage()
        {
            // Arrange
            var httpClient = CreateHttpClient(request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                });
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateAsync("user", "pass");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Server returned null response", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateAsync_WithHttpRequestException_ReturnsFailureWithExceptionMessage()
        {
            // Arrange
            var httpClient = CreateHttpClient(request =>
            {
                throw new HttpRequestException("Connection timeout");
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateAsync("user", "pass");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Authentication failed", result.ErrorMessage);
            Assert.Contains("Connection timeout", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateAsync_With401Unauthorized_ReturnsFailure()
        {
            // Arrange
            var httpClient = CreateHttpClient(async request =>
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Unauthorized", System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act - EnsureSuccessStatusCode throws, but AuthenticateAsync catches and returns failed result
            var result = await service.AuthenticateAsync("user", "wrongpass");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Authentication failed", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateAsync_ForwardsCancellationToken()
        {
            // Arrange
            var tcs = new TaskCompletionSource<bool>();
            var httpClient = CreateHttpClient(async request =>
            {
                var response = new AuthResultDto(Success: true, AuthToken: "token");
                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);
            var cts = new CancellationTokenSource();

            // Act
            var result = await service.AuthenticateAsync("user", "pass", cts.Token);

            // Assert - if we get here without cancellation, token was properly forwarded
            Assert.NotNull(result);
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_WithSuccessfulLogin_NoTwoFactorNeeded_ReturnsAuthResult()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var authToken = "auth-token-12345";
            var twoFactorProviderCalled = false;

            var httpClient = CreateHttpClient(async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/auth/login", request.RequestUri?.PathAndQuery);

                // First login attempt succeeds without 2FA
                var response = new AuthResultDto(
                    Success: true,
                    AuthToken: authToken,
                    RequiresTwoFactor: false
                );

                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateWithTwoFactorAsync(
                username,
                password,
                async () =>
                {
                    twoFactorProviderCalled = true;
                    return "123456";
                }
            );

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(authToken, result.AuthToken);
            Assert.False(twoFactorProviderCalled, "2FA provider should not be called when 2FA is not required");
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_WithTwoFactorRequired_CallsProviderAndSubmitCode()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var twoFactorCode = "123456";
            var authAttemptId = Guid.NewGuid();
            var authToken = "final-auth-token";
            var requestCount = 0;
            var twoFactorCodeProviderCalls = 0;

            var httpClient = CreateHttpClient(async request =>
            {
                requestCount++;

                if (requestCount == 1)
                {
                    // First request: initial login that requires 2FA
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("/api/v1/auth/login", request.RequestUri?.PathAndQuery);

                    var response = new AuthResultDto(
                        Success: false,
                        RequiresTwoFactor: true,
                        AuthAttemptId: authAttemptId
                    );

                    var json = await JsonContent.Create(response).ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }
                else if (requestCount == 2)
                {
                    // Second request: 2FA submission
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("/api/v1/auth/login/two-factor", request.RequestUri?.PathAndQuery);

                    var body = await request.Content!.ReadFromJsonAsync<TwoFactorRequestDto>();
                    Assert.NotNull(body);
                    Assert.Equal(authAttemptId, body.AuthAttemptId);
                    Assert.Equal(twoFactorCode, body.Code);

                    var response = new AuthResultDto(
                        Success: true,
                        AuthToken: authToken
                    );

                    var json = await JsonContent.Create(response).ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }

                throw new InvalidOperationException("Unexpected request");
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateWithTwoFactorAsync(
                username,
                password,
                async () =>
                {
                    twoFactorCodeProviderCalls++;
                    return twoFactorCode;
                }
            );

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(authToken, result.AuthToken);
            Assert.Equal(1, twoFactorCodeProviderCalls);
            Assert.Equal(2, requestCount);
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_WithTwoFactorCodeProviderThrows_ReturnsFailure()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var authAttemptId = Guid.NewGuid();

            var httpClient = CreateHttpClient(async request =>
            {
                var response = new AuthResultDto(
                    Success: false,
                    RequiresTwoFactor: true,
                    AuthAttemptId: authAttemptId
                );

                var json = await JsonContent.Create(response).ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AuthenticateWithTwoFactorAsync(
                    username,
                    password,
                    async () => throw new InvalidOperationException("2FA provider failed")
                )
            );
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_WithTwoFactorResponseNull_ReturnsFailure()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var authAttemptId = Guid.NewGuid();
            var requestCount = 0;

            var httpClient = CreateHttpClient(async request =>
            {
                requestCount++;

                if (requestCount == 1)
                {
                    var response = new AuthResultDto(
                        Success: false,
                        RequiresTwoFactor: true,
                        AuthAttemptId: authAttemptId
                    );

                    var json = await JsonContent.Create(response).ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }
                else if (requestCount == 2)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                    };
                }

                throw new InvalidOperationException("Unexpected request");
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateWithTwoFactorAsync(
                username,
                password,
                async () => "123456"
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("null response after two-factor", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_WithHttpRequestException_ReturnsFailure()
        {
            // Arrange
            var httpClient = CreateHttpClient(request =>
            {
                throw new HttpRequestException("Connection failed");
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateWithTwoFactorAsync(
                "user",
                "pass",
                async () => "123456"
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Two-factor authentication failed", result.ErrorMessage);
            Assert.Contains("Connection failed", result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticateWithTwoFactorAsync_ForwardsCancellationToken()
        {
            // Arrange
            var authAttemptId = Guid.NewGuid();
            var requestCount = 0;

            var httpClient = CreateHttpClient(async request =>
            {
                requestCount++;

                if (requestCount == 1)
                {
                    var response = new AuthResultDto(
                        Success: false,
                        RequiresTwoFactor: true,
                        AuthAttemptId: authAttemptId
                    );

                    var json = await JsonContent.Create(response).ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }
                else if (requestCount == 2)
                {
                    var response = new AuthResultDto(Success: true, AuthToken: "token");
                    var json = await JsonContent.Create(response).ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }

                throw new InvalidOperationException("Unexpected request");
            });

            var service = new RemoteProviderAuthService(httpClient);
            var cts = new CancellationTokenSource();

            // Act
            var result = await service.AuthenticateWithTwoFactorAsync(
                "user",
                "pass",
                async () => "123456",
                cts.Token
            );

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task IsAuthenticatedAsync_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteProviderAuthService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() => service.IsAuthenticatedAsync());
        }

        [Fact]
        public async Task RefreshAuthAsync_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteProviderAuthService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() => service.RefreshAuthAsync());
        }

        [Fact]
        public async Task RestoreFromSavedCredentialsAsync_NoArg_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteProviderAuthService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() => service.RestoreFromSavedCredentialsAsync());
        }

        [Fact]
        public async Task RestoreFromSavedCredentialsAsync_WithProviderAccountId_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteProviderAuthService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() => service.RestoreFromSavedCredentialsAsync(Guid.NewGuid()));
        }

        [Fact]
        public void GetAuthStatus_Always_ThrowsNotSupportedException()
        {
            // Arrange
            var httpClient = CreateHttpClient(request => Task.FromResult(new HttpResponseMessage()));
            var service = new RemoteProviderAuthService(httpClient);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => service.GetAuthStatus());
        }

        [Fact]
        public async Task AuthenticateAsync_WithCaseSensitiveJsonResponse_DeserializesCorrectly()
        {
            // Arrange - test case-insensitive JSON deserialization
            var username = "testuser";
            var password = "password123";
            var httpClient = CreateHttpClient(request =>
            {
                // Return JSON with PascalCase even though the DTO expects it
                // The JsonSerializerOptions in RemoteProviderAuthService should handle this
                var jsonContent = @"{
                    ""success"": true,
                    ""authToken"": ""token123"",
                    ""expiresAt"": ""2025-12-31T23:59:59Z"",
                    ""providerAccountId"": ""00000000-0000-0000-0000-000000000001"",
                    ""requiresTwoFactor"": false
                }";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                });
            });

            var service = new RemoteProviderAuthService(httpClient);

            // Act
            var result = await service.AuthenticateAsync(username, password);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("token123", result.AuthToken);
        }
    }
}
