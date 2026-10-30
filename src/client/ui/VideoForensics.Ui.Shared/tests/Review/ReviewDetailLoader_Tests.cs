using Moq;

using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review detail loader: detail chunking, jamming merged across cameras, and anomaly filtering and ordering.</summary>
public class ReviewDetailLoader_Tests
{
    private static readonly DateTime From = new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(1);

    private static ReviewDetailLoader CreateLoader(
        Mock<IEventDetailRepository>? details = null,
        Mock<IJammingRepository>? jamming = null,
        Mock<ISignalAnalysisService>? signals = null)
    {
        return new ReviewDetailLoader(
            (details ?? new Mock<IEventDetailRepository>()).Object,
            (jamming ?? new Mock<IJammingRepository>()).Object,
            (signals ?? new Mock<ISignalAnalysisService>()).Object);
    }

    private static JammingIncidentRecord Incident(Guid device, DateTime startUtc) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = device,
        StartUtc = startUtc,
        EndUtc = startUtc.AddMinutes(5),
        Confidence = JammingConfidenceLevel.High,
        Source = JammingIncidentSource.AutoDetected,
    };

    private static SignalDeviceAnalysis Camera(Guid device, params SignalDeviationWindow[] deviations) => new(
        device,
        "Camera",
        "Ring",
        "provider-" + device.ToString("N")[..8],
        SignalStats.Compute([]),
        SignalStats.Compute([]),
        [],
        [],
        deviations,
        SignalCalendar.Compute([], From, To),
        SignalBuckets.Compute([], From, To, SignalBucket.Hour),
        SignalBuckets.Compute([], From, To, SignalBucket.Day),
        SignalBuckets.Compute([], From, To, SignalBucket.Week));

    [Fact]
    public async Task ReviewDetailLoader_ManyEventIds_QueriesInChunksOfTwoHundred()
    {
        // Arrange
        var sizes = new List<int>();
        var details = new Mock<IEventDetailRepository>();
        _ = details
            .Setup(r => r.GetDetailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
            {
                sizes.Add(ids.Count);
                IReadOnlyList<EventDetail> rows = ids.Select(id => new EventDetail(id, null, [], [], [], [])).ToList();
                return Task.FromResult(rows);
            });
        var loader = CreateLoader(details);
        Guid[] ids = Enumerable.Range(0, 450).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        IReadOnlyDictionary<Guid, EventDetail> result = await loader.LoadEventDetailsAsync(ids, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { 200, 200, 50 }, sizes);
        Assert.Equal(450, result.Count);
        Assert.True(result.ContainsKey(ids[449]));
    }

    [Fact]
    public async Task ReviewDetailLoader_EmptyEventIds_ReturnsEmptyWithoutCallingTheRepository()
    {
        // Arrange
        var details = new Mock<IEventDetailRepository>();
        var loader = CreateLoader(details);

        // Act
        IReadOnlyDictionary<Guid, EventDetail> result = await loader.LoadEventDetailsAsync([], CancellationToken.None);

        // Assert
        Assert.Empty(result);
        details.Verify(r => r.GetDetailsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewDetailLoader_Jamming_QueriesEachDeviceAndMergesNewestFirst()
    {
        // Arrange
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        var jamming = new Mock<IJammingRepository>();
        var asked = new List<Guid?>();
        _ = jamming
            .Setup(r => r.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns((Guid? device, DateTime? _, DateTime? _, CancellationToken _) =>
            {
                asked.Add(device);
                IReadOnlyList<JammingIncidentRecord> rows = device == first
                    ? [Incident(first, From.AddHours(2)), Incident(first, From.AddHours(6))]
                    : [Incident(second, From.AddHours(4))];
                return Task.FromResult(rows);
            });
        var loader = CreateLoader(jamming: jamming);

        // Act
        IReadOnlyList<JammingIncidentRecord> result = await loader.LoadJammingAsync([first, second], From, To, CancellationToken.None);

        // Assert
        Assert.Equal(new Guid?[] { first, second }, asked);
        Assert.Equal(new[] { From.AddHours(6), From.AddHours(4), From.AddHours(2) }, result.Select(r => r.StartUtc).ToArray());
    }

    [Fact]
    public async Task ReviewDetailLoader_JammingWithNoDevices_ReturnsEmptyWithoutCalls()
    {
        // Arrange
        var jamming = new Mock<IJammingRepository>();
        var loader = CreateLoader(jamming: jamming);

        // Act
        IReadOnlyList<JammingIncidentRecord> result = await loader.LoadJammingAsync([], From, To, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        jamming.Verify(r => r.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewDetailLoader_Anomalies_LeavesOutTheNormalLevel()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        var signals = new Mock<ISignalAnalysisService>();
        _ = signals
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new SignalAnalysis([Camera(
                device,
                new SignalDeviationWindow(From.AddHours(1), SignalDeviationLevel.OneSigma, 2, -80),
                new SignalDeviationWindow(From.AddHours(2), SignalDeviationLevel.None, 0, -50),
                new SignalDeviationWindow(From.AddHours(3), SignalDeviationLevel.TwoSigma, 1, -95))])));
        var loader = CreateLoader(signals: signals);

        // Act
        IReadOnlyList<ReviewAnomaly> result = await loader.LoadAnomaliesAsync([device], From, To, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, a => a.Level == SignalDeviationLevel.None);
    }

    [Fact]
    public async Task ReviewDetailLoader_Anomalies_OrdersNewestFirstAcrossCamerasAndSpansOneHour()
    {
        // Arrange
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        var signals = new Mock<ISignalAnalysisService>();
        _ = signals
            .Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new SignalAnalysis(
            [
                Camera(first,
                    new SignalDeviationWindow(From.AddHours(1), SignalDeviationLevel.OneSigma, 1, -80),
                    new SignalDeviationWindow(From.AddHours(3), SignalDeviationLevel.TwoSigma, 1, -95)),
                Camera(second, new SignalDeviationWindow(From.AddHours(4), SignalDeviationLevel.OneSigma, 1, -82)),
            ])));
        var loader = CreateLoader(signals: signals);

        // Act
        IReadOnlyList<ReviewAnomaly> result = await loader.LoadAnomaliesAsync([first, second], From, To, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { From.AddHours(4), From.AddHours(3), From.AddHours(1) }, result.Select(a => a.StartUtc).ToArray());
        Assert.Equal(new[] { second, first, first }, result.Select(a => a.DeviceId).ToArray());
        Assert.All(result, a => Assert.Equal(a.StartUtc.AddHours(1), a.EndUtc));
    }
}
