using VideoForensics.Core.Logging.Services;

using Xunit;

namespace VideoForensics.Core.Logging.Tests
{
    public class LogRedactorTests
    {
        [Fact]
        public void Redact_BearerToken_RedactsValue()
        {
            // Act
            string? result = LogRedactor.Redact("Authorization: Bearer sk-1234567890abcdef");

            // Assert
            Assert.Contains("[REDACTED]", result);
            Assert.DoesNotContain("sk-1234567890abcdef", result);
        }

        [Fact]
        public void Redact_XStepUpToken_RedactsValue()
        {
            // Act
            string? result = LogRedactor.Redact("X-StepUp-Token: some-long-token-value-123");

            // Assert
            Assert.Contains("[REDACTED]", result);
            Assert.DoesNotContain("some-long-token-value-123", result);
        }

        [Fact]
        public void Redact_PasswordKeyValue_RedactsValue()
        {
            // Act
            string? result = LogRedactor.Redact("password=MySecurePassword123");

            // Assert
            Assert.Contains("password=[REDACTED]", result);
            Assert.DoesNotContain("MySecurePassword123", result);
        }

        [Fact]
        public void Redact_PwdKeyValue_RedactsValue()
        {
            // Act
            string? result = LogRedactor.Redact("pwd=SecretPwd456");

            // Assert
            Assert.Contains("pwd=[REDACTED]", result);
            Assert.DoesNotContain("SecretPwd456", result);
        }

        [Fact]
        public void Redact_TokenKeyValue_RedactsValue()
        {
            // Act
            string? result = LogRedactor.Redact("token=abc123xyz789");

            // Assert
            Assert.Contains("token=[REDACTED]", result);
            Assert.DoesNotContain("abc123xyz789", result);
        }

        [Fact]
        public void Redact_SecretKeyValue_RedactsValue()
        {
            // Act
            string? result = LogRedactor.Redact("secret=MyBigSecret");

            // Assert
            Assert.Contains("secret=[REDACTED]", result);
            Assert.DoesNotContain("MyBigSecret", result);
        }

        [Fact]
        public void Redact_CaseInsensitive_Password()
        {
            // Act
            string? result = LogRedactor.Redact("PASSWORD=test123");

            // Assert
            Assert.Contains("password=[REDACTED]", result);
            Assert.DoesNotContain("test123", result);
        }

        [Fact]
        public void Redact_CaseInsensitive_Token()
        {
            // Act
            string? result = LogRedactor.Redact("TOKEN=secrettoken");

            // Assert
            Assert.Contains("token=[REDACTED]", result);
            Assert.DoesNotContain("secrettoken", result);
        }

        [Fact]
        public void Redact_NormalText_Untouched()
        {
            // Act
            string? result = LogRedactor.Redact("User successfully logged in");

            // Assert
            Assert.Equal("User successfully logged in", result);
        }

        [Fact]
        public void Redact_Null_ReturnsNull()
        {
            // Act
            string? result = LogRedactor.Redact(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Redact_Empty_ReturnsEmpty()
        {
            // Act
            string? result = LogRedactor.Redact(string.Empty);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Redact_MultiplePatterns_AllRedacted()
        {
            // Act
            string? result = LogRedactor.Redact("Bearer token123 and password=secret and X-StepUp-Token: xyz");

            // Assert
            Assert.DoesNotContain("token123", result);
            Assert.DoesNotContain("secret", result);
            Assert.DoesNotContain("xyz", result);
            Assert.Contains("[REDACTED]", result);
        }

        [Fact]
        public void Redact_BearerToken_CaseInsensitive()
        {
            // Act
            string? result = LogRedactor.Redact("bearer sk-token-123");

            // Assert
            Assert.Contains("[REDACTED]", result);
            Assert.DoesNotContain("sk-token-123", result);
        }

        [Fact]
        public void Redact_XStepUpToken_CaseInsensitive()
        {
            // Act
            string? result = LogRedactor.Redact("x-stepup-token: my-step-up-value");

            // Assert
            Assert.Contains("[REDACTED]", result);
            Assert.DoesNotContain("my-step-up-value", result);
        }

        [Fact]
        public void Redact_PasswordWithSpecialCharacters()
        {
            // Act
            string? result = LogRedactor.Redact("password=P@$$w0rd!#%&");

            // Assert
            Assert.Contains("password=[REDACTED]", result);
            Assert.DoesNotContain("P@$$w0rd!#%&", result);
        }

        [Fact]
        public void Redact_TokenWithQueryString()
        {
            // Act
            string? result = LogRedactor.Redact("url?token=abc123&other=value");

            // Assert
            Assert.Contains("token=[REDACTED]", result);
            Assert.Contains("other=value", result);
            Assert.DoesNotContain("abc123", result);
        }
    }
}
