using System.Reactive.Subjects;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Core.Api.Contracts;

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
