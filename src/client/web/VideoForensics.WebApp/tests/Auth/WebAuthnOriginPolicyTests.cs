using Fido2NetLib;

using Microsoft.AspNetCore.Http;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests.Auth
{
    public class WebAuthnOriginPolicyTests
    {
        private static HttpContext Request(string scheme, string host)
        {
            var context = new DefaultHttpContext();
            context.Request.Scheme = scheme;
            context.Request.Host = new HostString(host);
            return context;
        }

        private static Fido2Configuration Fido2() => new() { Origins = new HashSet<string>(StringComparer.Ordinal) };

        [Fact]
        public void WebAuthnOriginPolicy_Apply_RequestOnConfiguredInternetHost_AddsOrigin()
        {
            var configuration = Fido2();
            var forensics = new ForensicsConfiguration { InternetServerUrl = "https://video.example.com" };

            WebAuthnOriginPolicy.Apply(Request("https", "video.example.com"), configuration, forensics);

            Assert.Contains("https://video.example.com", configuration.Origins);
        }

        [Fact]
        public void WebAuthnOriginPolicy_Apply_RequestOnOtherHost_DoesNotAddOrigin()
        {
            var configuration = Fido2();
            var forensics = new ForensicsConfiguration { InternetServerUrl = "https://video.example.com" };

            WebAuthnOriginPolicy.Apply(Request("https", "attacker.example.net"), configuration, forensics);

            Assert.DoesNotContain("https://attacker.example.net", configuration.Origins);
        }

        [Fact]
        public void WebAuthnOriginPolicy_Apply_ConfiguredHostWithDifferentCase_AddsOrigin()
        {
            var configuration = Fido2();
            var forensics = new ForensicsConfiguration { InternetServerUrl = "https://Video.Example.com" };

            WebAuthnOriginPolicy.Apply(Request("https", "video.example.com"), configuration, forensics);

            Assert.Contains("https://video.example.com", configuration.Origins);
        }

        [Fact]
        public void WebAuthnOriginPolicy_Apply_ConfiguredInternetAddress_SetsRpIdOnEveryRequest()
        {
            var configuration = Fido2();
            configuration.RPID = "localhost";
            var forensics = new ForensicsConfiguration { InternetServerUrl = "https://video.example.com" };

            WebAuthnOriginPolicy.Apply(Request("https", "localhost"), configuration, forensics);

            Assert.Equal("video.example.com", configuration.RPID);
        }

        [Fact]
        public void WebAuthnOriginPolicy_Apply_NoInternetAddress_LeavesRpIdUnchanged()
        {
            var configuration = Fido2();
            configuration.RPID = "localhost";
            var forensics = new ForensicsConfiguration { InternetServerUrl = null };

            WebAuthnOriginPolicy.Apply(Request("https", "localhost"), configuration, forensics);

            Assert.Equal("localhost", configuration.RPID);
        }
    }
}
