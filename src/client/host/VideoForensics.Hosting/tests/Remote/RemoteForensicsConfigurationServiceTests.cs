using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteForensicsConfigurationServiceTests
    {
        [Fact]
        public async Task LoadConfigurationAsync_ServerSnapshot_MapsAllowlistedSettings()
        {
            var snapshot = new ClientConfigDto(
                EnableForensicAnalysisReports: false,
                EnableSignalAnomalyReports: true,
                EnableChainOfCustodyReports: true,
                EnableEvidenceValidationReports: true,
                EnableAccessControlMonitoring: true,
                EnablePiiRedaction: true,
                ReportOutputFormat: "pdf",
                RedactionLevel: RedactionLevel.Heavy,
                RetentionDaysDefault: 90,
                LogLevel: "Debug",
                MaxConcurrentDownloads: 4,
                DownloadStartDate: "2026-01-01",
                EnableHealthSync: true,
                EnableMdnsAdvertisement: false,
                EnableEmailNotifications: true,
                ConfiguredNetworkTier: NetworkTier.Network,
                InternetServerUrl: "https://example.test",
                EnableLiveView: true,
                LiveViewIdleTimeoutMinutes: 7,
                LiveViewTelemetrySampleIntervalSeconds: 3,
                SustainedModeMaxDurationMinutes: 60,
                LiveViewTelemetryRetentionDays: 10,
                ElevatedPollingWindowMinutes: 11,
                ElevatedPollingIntervalSeconds: 30,
                EnableBitrateCalibration: true,
                CalibrationCheckIntervalMinutes: 20,
                CalibrationSessionDurationSeconds: 15,
                MinCalibrationSamplesPerBucket: 6,
                CalibrationStalenessDays: 21);
            var httpClient = new HttpClient(new StubHandler(JsonSerializer.Serialize(snapshot))) { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);

            IForensicsConfiguration config = await service.LoadConfigurationAsync(string.Empty, CancellationToken.None);

            Assert.False(config.EnableForensicAnalysisReports);
            Assert.Equal("pdf", config.ReportOutputFormat);
            Assert.Equal(RedactionLevel.Heavy, config.RedactionLevel);
            Assert.Equal(60, config.SustainedModeMaxDurationMinutes);
            Assert.Equal("https://example.test", config.InternetServerUrl);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly string _json;

            public StubHandler(string json)
            {
                _json = json;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
                });
            }
        }

        [Fact]
        public async Task SaveConfigurationAsync_Always_ThrowsNotSupportedException()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);
            var config = new ForensicsConfiguration();

            NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await service.SaveConfigurationAsync(config, CancellationToken.None);
            });

            Assert.NotNull(exception);
            Assert.Contains("Not supported on a remote", exception.Message);
        }

        [Fact]
        public async Task SaveConfigurationAsync_WithCancellationToken_StillThrowsNotSupportedException()
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("https://example.test") };
            var service = new RemoteForensicsConfigurationService(httpClient);
            var config = new ForensicsConfiguration();

            _ = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await service.SaveConfigurationAsync(config, new CancellationTokenSource().Token);
            });
        }
    }
}
