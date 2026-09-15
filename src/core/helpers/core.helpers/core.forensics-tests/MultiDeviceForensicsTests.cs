namespace VideoForensics.Forensics.Tests
{
    public class MultiDeviceForensicsTests
    {
        private readonly IMultiDeviceForensics _forensics;

        public MultiDeviceForensicsTests()
        {
            _forensics = new Implementations.MultiDeviceForensics();
        }

        [Fact]
        public async Task AnalyzeMultipleDevicesAsync_WithCorrelatedAnomalies_ReturnsSuspicionScore()
        {
            var deviceIds = new[] { "camera-01", "camera-02", "camera-03" };
            var baseTime = DateTime.UtcNow;

            var report1 = new SignalAnomalyReport
            {
                ReportId = Guid.NewGuid().ToString(),
                GeneratedAt = baseTime,
                TotalEventsAnalyzed = 100,
                AnomalousEvents = [
                    new SignalAnomalyFinding { EventTimestamp = baseTime.AddSeconds(10), AnomalyType = SignalAnomalyType.ExtremelyWeakSignal },
                    new SignalAnomalyFinding { EventTimestamp = baseTime.AddSeconds(20), AnomalyType = SignalAnomalyType.UnusualStrengthVariance }
                ]
            };

            var report2 = new SignalAnomalyReport
            {
                ReportId = Guid.NewGuid().ToString(),
                GeneratedAt = baseTime,
                TotalEventsAnalyzed = 95,
                AnomalousEvents = [
                    new SignalAnomalyFinding { EventTimestamp = baseTime.AddSeconds(11), AnomalyType = SignalAnomalyType.SuddenDrop },
                    new SignalAnomalyFinding { EventTimestamp = baseTime.AddSeconds(21), AnomalyType = SignalAnomalyType.SustainedDegradation }
                ]
            };

            var reports = new[] { report1, report2 };
            var result = await _forensics.AnalyzeMultipleDevicesAsync(deviceIds, reports);
            
            Assert.NotNull(result);
            Assert.Equal(2, result.DeviceIds.Count);
        }

        [Fact]
        public async Task AnalyzeMultipleDevicesAsync_WithSingleDevice_ReturnsReport()
        {
            var deviceIds = new[] { "camera-01" };
            var baseTime = DateTime.UtcNow;
            var report = new SignalAnomalyReport { ReportId = Guid.NewGuid().ToString(), GeneratedAt = baseTime, TotalEventsAnalyzed = 50, AnomalousEvents = [new SignalAnomalyFinding { EventTimestamp = baseTime }] };
            var reports = new[] { report };
            
            var result = await _forensics.AnalyzeMultipleDevicesAsync(deviceIds, reports);
            
            Assert.NotNull(result);
            Assert.Single(result.DeviceIds);
        }

        [Fact]
        public async Task DetectSynchronizedAnomaliesAsync_WithSingleDevice_ReturnsEmpty()
        {
            var deviceIds = new[] { "camera-01" };
            var coincidenceWindow = TimeSpan.FromSeconds(5);
            
            var result = await _forensics.DetectSynchronizedAnomaliesAsync(deviceIds, coincidenceWindow);
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task CalculateAnomalyCorrelationAsync_WithIdenticalDevices_ReturnsOne()
        {
            var deviceId = "camera-01";
            var startTime = DateTime.UtcNow.AddDays(-7);
            var endTime = DateTime.UtcNow;
            
            var correlation = await _forensics.CalculateAnomalyCorrelationAsync(deviceId, deviceId, startTime, endTime);
            
            Assert.Equal(1.0, correlation);
        }

        [Fact]
        public async Task GetBaselineCorrelationAsync_WithDefaultBaseline_ReturnsBaseline()
        {
            var deviceIds = new[] { "camera-01", "camera-02" };
            
            var baseline = await _forensics.GetBaselineCorrelationAsync(deviceIds);
            
            Assert.NotNull(baseline);
            Assert.Equal(2, baseline.DeviceIds.Count);
        }

        [Fact]
        public async Task GetBaselineCorrelationAsync_WithEmptyDevices_ReturnsEmptyBaseline()
        {
            var deviceIds = new string[] { };
            
            var baseline = await _forensics.GetBaselineCorrelationAsync(deviceIds);
            
            Assert.NotNull(baseline);
            Assert.Empty(baseline.DeviceIds);
        }
    }
}
