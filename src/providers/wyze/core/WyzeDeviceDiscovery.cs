using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace VideoForensics.Providers.Wyze;

/// <summary>Discovers Wyze devices on a user's account</summary>
public class WyzeDeviceDiscovery
{
    /// <summary>Retrieves all devices for the authenticated user</summary>
    public async Task<IEnumerable<WyzeDevice>> GetDevicesAsync(WyzeCredentials credentials, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Wyze device discovery is not yet implemented");
    }

    /// <summary>Gets a specific device by ID</summary>
    public async Task<WyzeDevice?> GetDeviceAsync(string deviceId, WyzeCredentials credentials, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Wyze device retrieval is not yet implemented");
    }
}
