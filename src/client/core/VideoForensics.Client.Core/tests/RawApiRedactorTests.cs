using System.Text.Json;

using VideoForensics.Client.Core.Tools;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class RawApiRedactorTests
    {
        [Fact]
        public void RedactUrl_WithAccessToken_RedactsValue()
        {
            string url = "https://example.com/api?access_token=secret123&other=value";
            string result = RawApiRedactor.RedactUrl(url);
            Assert.Contains("access_token=REDACTED", result);
            Assert.Contains("other=value", result);
            Assert.DoesNotContain("secret123", result);
        }

        [Fact]
        public void RedactUrl_WithMultipleSensitiveParams_RedactsAll()
        {
            string url = "https://example.com/api?api_key=key123&refresh_token=refresh456&data=normal";
            string result = RawApiRedactor.RedactUrl(url);
            Assert.Contains("api_key=REDACTED", result);
            Assert.Contains("refresh_token=REDACTED", result);
            Assert.Contains("data=normal", result);
        }

        [Fact]
        public void RedactUrl_CaseInsensitive_RedactsMixedCase()
        {
            string url = "https://example.com/api?Access_Token=secret&AUTH=token";
            string result = RawApiRedactor.RedactUrl(url);
            Assert.Contains("Access_Token=REDACTED", result);
            Assert.Contains("AUTH=REDACTED", result);
        }

        [Fact]
        public void RedactUrl_NoQuery_ReturnsSame()
        {
            string url = "https://example.com/api";
            string result = RawApiRedactor.RedactUrl(url);
            Assert.Equal(url, result);
        }

        [Fact]
        public void RedactUrl_Malformed_ReturnsSame()
        {
            string url = "not a url at all";
            string result = RawApiRedactor.RedactUrl(url);
            Assert.Equal(url, result);
        }

        [Fact]
        public void RedactUrl_EmptyString_ReturnsEmpty()
        {
            string result = RawApiRedactor.RedactUrl("");
            Assert.Equal("", result);
        }

        [Fact]
        public void RedactUrl_Null_ReturnsNull()
        {
            string? result = RawApiRedactor.RedactUrl(null!);
            Assert.Null(result);
        }

        [Fact]
        public void RedactBody_ValidJson_RedactsFields()
        {
            string body = """{"username":"user","password":"secret123","data":"safe"}""";
            string result = RawApiRedactor.RedactBody(body);

            var doc = JsonDocument.Parse(result);
            Assert.Equal("user", doc.RootElement.GetProperty("username").GetString());
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("password").GetString());
            Assert.Equal("safe", doc.RootElement.GetProperty("data").GetString());
        }

        [Fact]
        public void RedactBody_NestedJson_RedactsNestedFields()
        {
            string body = """{"user":{"name":"john","token":"secret"},"safe":{"data":"ok"}}""";
            string result = RawApiRedactor.RedactBody(body);

            var doc = JsonDocument.Parse(result);
            Assert.Equal("john", doc.RootElement.GetProperty("user").GetProperty("name").GetString());
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("user").GetProperty("token").GetString());
            Assert.Equal("ok", doc.RootElement.GetProperty("safe").GetProperty("data").GetString());
        }

        [Fact]
        public void RedactBody_ArrayOfObjects_RedactsFieldsInArray()
        {
            string body = """{"items":[{"id":1,"api_key":"key1"},{"id":2,"api_key":"key2"}]}""";
            string result = RawApiRedactor.RedactBody(body);

            var doc = JsonDocument.Parse(result);
            var items = doc.RootElement.GetProperty("items");
            int index = 0;
            foreach (JsonElement item in items.EnumerateArray())
            {
                index++;
                Assert.Equal(index, item.GetProperty("id").GetInt32());
                Assert.Equal("REDACTED", item.GetProperty("api_key").GetString());
            }
        }

        [Fact]
        public void RedactBody_CaseInsensitive_RedactsAllVariations()
        {
            string body = """{"Password":"secret","PASSWORD":"secret2","authorization":"bearer token","Authorization":"bearer"}""";
            string result = RawApiRedactor.RedactBody(body);

            var doc = JsonDocument.Parse(result);
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("Password").GetString());
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("PASSWORD").GetString());
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("authorization").GetString());
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("Authorization").GetString());
        }

        [Fact]
        public void RedactBody_NotJson_ReturnsSame()
        {
            string body = "This is plain text, not JSON";
            string result = RawApiRedactor.RedactBody(body);
            Assert.Equal(body, result);
        }

        [Fact]
        public void RedactBody_Null_ReturnsEmpty()
        {
            string result = RawApiRedactor.RedactBody(null);
            Assert.Equal("", result);
        }

        [Fact]
        public void RedactBody_Empty_ReturnsEmpty()
        {
            string result = RawApiRedactor.RedactBody("");
            Assert.Equal("", result);
        }

        [Fact]
        public void RedactBody_SessionTokenField_RedactsIt()
        {
            string body = """{"session_token":"abc123","user":"john"}""";
            string result = RawApiRedactor.RedactBody(body);

            var doc = JsonDocument.Parse(result);
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("session_token").GetString());
            Assert.Equal("john", doc.RootElement.GetProperty("user").GetString());
        }

        [Fact]
        public void RedactBody_HardwareIdField_RedactsIt()
        {
            string body = """{"hardware_id":"hw-123","info":"safe"}""";
            string result = RawApiRedactor.RedactBody(body);

            var doc = JsonDocument.Parse(result);
            Assert.Equal("REDACTED", doc.RootElement.GetProperty("hardware_id").GetString());
            Assert.Equal("safe", doc.RootElement.GetProperty("info").GetString());
        }
    }
}
