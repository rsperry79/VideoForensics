namespace VideoForensics.Client.Ui.Shared.Services.Scope;

/// <summary>A provider location offered by the Location filter.</summary>
/// <param name="Id">The location id, matching <c>Device.LocationId</c>.</param>
/// <param name="Name">Operator-facing name; the provider location id when the location has no name.</param>
public sealed record FilterLocation(Guid Id, string Name);
