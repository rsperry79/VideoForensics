using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    public class CallStatusClassifierTests
    {
        private static EndpointDescriptor CreateDescriptor(bool notApplicableWithoutAlarmHub, bool requiresPushRegistration = false) => new()
        {
            Key = "test-endpoint",
            DisplayName = "Test Endpoint",
            Description = "A test endpoint",
            SessionMethod = "Session.Test()",
            HttpMethod = "GET",
            ApiPath = "https://api.example.com/test",
            Invoke = (_, _) => Task.CompletedTask,
            NotApplicableWithoutAlarmHub = notApplicableWithoutAlarmHub,
            RequiresPushRegistration = requiresPushRegistration
        };

        [Fact]
        public void Classify_AlarmOnlyEndpointReturning404_ReturnsNotApplicable()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(true), success: false, new[] { 404 });

            Assert.Equal("not-applicable", status);
        }

        [Fact]
        public void Classify_AlarmOnlyEndpointReturning500_ReturnsFailed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(true), success: false, new[] { 500 });

            Assert.Equal("failed", status);
        }

        [Fact]
        public void Classify_AlarmOnlyEndpointFailingWithNoHttpCalls_ReturnsFailed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(true), success: false, Enumerable.Empty<int>());

            Assert.Equal("failed", status);
        }

        [Fact]
        public void Classify_AlarmOnlyEndpointSucceeding_ReturnsPassed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(true), success: true, new[] { 200 });

            Assert.Equal("passed", status);
        }

        [Fact]
        public void Classify_RegularEndpointReturning404_ReturnsFailed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(false), success: false, new[] { 404 });

            Assert.Equal("failed", status);
        }

        [Fact]
        public void Classify_RegularEndpointSucceeding_ReturnsPassed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(false), success: true, new[] { 200 });

            Assert.Equal("passed", status);
        }

        [Fact]
        public void CallStatusClassifier_RequiresPushRegistration422ClientDeviceRequired_ReturnsNotApplicable()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(false, requiresPushRegistration: true), success: false,
                new[] { (StatusCode: 422, Body: "{\"error\":\"Client Device required\"}") });

            Assert.Equal("not-applicable", status);
        }

        [Fact]
        public void CallStatusClassifier_RegularEndpoint422ClientDeviceRequired_ReturnsFailed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(false), success: false,
                new[] { (StatusCode: 422, Body: "{\"error\":\"Client Device required\"}") });

            Assert.Equal("failed", status);
        }

        [Fact]
        public void CallStatusClassifier_RequiresPushRegistration422OtherBody_ReturnsFailed()
        {
            string status = CallStatusClassifier.Classify(CreateDescriptor(false, requiresPushRegistration: true), success: false,
                new[] { (StatusCode: 422, Body: "{\"error\":\"invalid doorbot\"}") });

            Assert.Equal("failed", status);
        }

        [Fact]
        public void EndpointDescriptor_RequiresPushRegistration_DefaultsToFalse()
        {
            Assert.False(CreateDescriptor(false).RequiresPushRegistration);
        }

        [Theory]
        [InlineData("subscribe-ding-events")]
        [InlineData("unsubscribe-ding-events")]
        [InlineData("subscribe-motion-events")]
        [InlineData("unsubscribe-motion-events")]
        public void EndpointRegistry_PushSubscriptionEndpoint_IsFlaggedRequiresPushRegistration(string key)
        {
            EndpointDescriptor? descriptor = EndpointRegistry.Find(key);

            Assert.NotNull(descriptor);
            Assert.True(descriptor!.RequiresPushRegistration);
        }

        [Fact]
        public void EndpointDescriptor_NotApplicableWithoutAlarmHub_DefaultsToFalse()
        {
            Assert.False(CreateDescriptor(false).NotApplicableWithoutAlarmHub);
            var defaulted = new EndpointDescriptor
            {
                Key = "k",
                DisplayName = "k",
                Description = "k",
                SessionMethod = "k",
                HttpMethod = "GET",
                ApiPath = "k",
                Invoke = (_, _) => Task.CompletedTask
            };
            Assert.False(defaulted.NotApplicableWithoutAlarmHub);
        }

        [Theory]
        [InlineData("location-mode")]
        [InlineData("location-mode-settings")]
        [InlineData("location-mode-sharing")]
        [InlineData("account-monitoring-status")]
        public void EndpointRegistry_AlarmOnlyEndpoint_IsFlaggedNotApplicableWithoutAlarmHub(string key)
        {
            EndpointDescriptor? descriptor = EndpointRegistry.Find(key);

            Assert.NotNull(descriptor);
            Assert.True(descriptor!.NotApplicableWithoutAlarmHub);
        }

        [Theory]
        [InlineData("shared-users")]
        [InlineData("location-events")]
        [InlineData("invitations")]
        public void EndpointRegistry_OtherLocationEndpoint_IsNotFlaggedNotApplicable(string key)
        {
            EndpointDescriptor? descriptor = EndpointRegistry.Find(key);

            Assert.NotNull(descriptor);
            Assert.False(descriptor!.NotApplicableWithoutAlarmHub);
        }

        [Fact]
        public void EndpointRegistry_LocationDetails_IsRemoved()
        {
            Assert.Null(EndpointRegistry.Find("location-details"));
        }
    }
}
