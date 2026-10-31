using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Clip loader double. Records each call; <see cref="Responder"/> decides the result, so a test can hold a call open.</summary>
public sealed class FakeReviewClipLoader : IReviewClipLoader
{
    private readonly List<LoadCall> _calls = [];

    /// <summary>Every load call, in order.</summary>
    public IReadOnlyList<LoadCall> Calls => _calls;

    /// <summary>Produces the result of a call. Defaults to an empty, untruncated set.</summary>
    public Func<LoadCall, CancellationToken, Task<ReviewClipSet>> Responder { get; set; } = (_, _) => Task.FromResult(new ReviewClipSet([], false));

    /// <inheritdoc />
    public Task<ReviewClipSet> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var call = new LoadCall(deviceIds.ToList(), fromUtc, toUtc);
        _calls.Add(call);
        return Responder(call, ct);
    }

    /// <summary>One recorded load call.</summary>
    public sealed record LoadCall(IReadOnlyList<Guid> DeviceIds, DateTime FromUtc, DateTime ToUtc);
}

/// <summary>Ticketed-URL double. Records each batch; URLs carry the batch number so a refresh is visible.</summary>
public sealed class FakeMediaContentUrlProvider : IMediaContentUrlProvider
{
    private readonly List<IReadOnlyList<Guid>> _requests = [];

    /// <summary>Every batch requested, in order.</summary>
    public IReadOnlyList<IReadOnlyList<Guid>> Requests => _requests;

    /// <summary>Ids the provider leaves out of its answer.</summary>
    public HashSet<Guid> Omitted { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, string>> GetContentUrlsAsync(IReadOnlyCollection<Guid> mediaItemIds, CancellationToken ct)
    {
        _requests.Add(mediaItemIds.ToList());
        int batch = _requests.Count;
        Dictionary<Guid, string> urls = mediaItemIds
            .Where(id => !Omitted.Contains(id))
            .ToDictionary(id => id, id => $"/api/v1/media/{id:D}/content?ticket=ticket-{batch}");
        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(urls);
    }
}

/// <summary>Evidence change feed double. Tests push changes through <see cref="Changes"/>.</summary>
public sealed class FakeEvidenceChangedSource : IEvidenceChangedSource
{
    /// <summary>The subject tests push changes into.</summary>
    public Subject<EvidenceChangedDto> Changes { get; } = new();

    /// <inheritdoc />
    public IObservable<EvidenceChangedDto> EvidenceChanged => Changes;
}

/// <summary>Event loader double. Records each page request; <see cref="Responder"/> decides the result, so a test can hold a call open.</summary>
public sealed class FakeReviewEventLoader : IReviewEventLoader
{
    private readonly List<EventLoadCall> _calls = [];

    /// <summary>Every page request, in order.</summary>
    public IReadOnlyList<EventLoadCall> Calls => _calls;

    /// <summary>Produces the result of a call. Defaults to an empty page.</summary>
    public Func<EventLoadCall, CancellationToken, Task<EventPage>> Responder { get; set; } = (_, _) => Task.FromResult(new EventPage([], 0));

    /// <inheritdoc />
    public Task<EventPage> LoadAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, string? eventType, int skip, int take, CancellationToken ct)
    {
        var call = new EventLoadCall(deviceIds.ToList(), fromUtc, toUtc, skip, take, eventType);
        _calls.Add(call);
        return Responder(call, ct);
    }

    /// <summary>One recorded page request.</summary>
    public sealed record EventLoadCall(IReadOnlyList<Guid> DeviceIds, DateTime FromUtc, DateTime ToUtc, int Skip, int Take, string? EventType = null);
}

/// <summary>Device health change feed double. Tests push changes through <see cref="Changes"/>.</summary>
public sealed class FakeDeviceHealthChangedSource : IDeviceHealthChangedSource
{
    /// <summary>The subject tests push changes into.</summary>
    public Subject<DeviceHealthChangedDto> Changes { get; } = new();

    /// <inheritdoc />
    public IObservable<DeviceHealthChangedDto> DeviceHealthChanged => Changes;
}

/// <summary>Detail loader double. Records each call; the responders decide the results, so a test can hold a call open.</summary>
public sealed class FakeReviewDetailLoader : IReviewDetailLoader
{
    private readonly List<IReadOnlyList<Guid>> _detailCalls = [];
    private readonly List<WindowLoadCall> _jammingCalls = [];
    private readonly List<WindowLoadCall> _anomalyCalls = [];

    /// <summary>The event ids of every detail call, in order.</summary>
    public IReadOnlyList<IReadOnlyList<Guid>> DetailCalls => _detailCalls;

    /// <summary>Every jamming load, in order.</summary>
    public IReadOnlyList<WindowLoadCall> JammingCalls => _jammingCalls;

    /// <summary>Every anomaly load, in order.</summary>
    public IReadOnlyList<WindowLoadCall> AnomalyCalls => _anomalyCalls;

    /// <summary>Produces the detail rows for a call. Defaults to none.</summary>
    public Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, EventDetail>>> DetailResponder { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyDictionary<Guid, EventDetail>>(new Dictionary<Guid, EventDetail>());

    /// <summary>Produces the jamming incidents for a call. Defaults to none.</summary>
    public Func<WindowLoadCall, CancellationToken, Task<IReadOnlyList<JammingIncidentRecord>>> JammingResponder { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<JammingIncidentRecord>>([]);

    /// <summary>Produces the anomalies for a call. Defaults to none.</summary>
    public Func<WindowLoadCall, CancellationToken, Task<IReadOnlyList<ReviewAnomaly>>> AnomalyResponder { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<ReviewAnomaly>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, EventDetail>> LoadEventDetailsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct)
    {
        _detailCalls.Add(eventIds.ToList());
        return DetailResponder(eventIds, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JammingIncidentRecord>> LoadJammingAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var call = new WindowLoadCall(deviceIds.ToList(), fromUtc, toUtc);
        _jammingCalls.Add(call);
        return JammingResponder(call, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ReviewAnomaly>> LoadAnomaliesAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var call = new WindowLoadCall(deviceIds.ToList(), fromUtc, toUtc);
        _anomalyCalls.Add(call);
        return AnomalyResponder(call, ct);
    }

    /// <summary>One recorded jamming or anomaly load.</summary>
    public sealed record WindowLoadCall(IReadOnlyList<Guid> DeviceIds, DateTime FromUtc, DateTime ToUtc);
}
