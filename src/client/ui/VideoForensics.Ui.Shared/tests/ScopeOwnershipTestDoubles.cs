
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Registers the provider-account, location and user repositories that <c>ScopeRail</c> resolves for its
/// Provider and Account filters. Tests that render the rail (directly or via a page) call this once.
/// </summary>
internal static class ScopeOwnershipTestDoubles
{
    /// <summary>Registers repositories that report no accounts, so every device stays visible.</summary>
    public static void RegisterEmpty(IServiceCollection services)
    {
        Register(services, Array.Empty<ProviderAccount>(), new Dictionary<Guid, IReadOnlyList<Location>>());
    }

    /// <summary>Registers repositories returning the given accounts and each account's linked locations.</summary>
    public static void Register(
        IServiceCollection services,
        IReadOnlyList<ProviderAccount> accounts,
        IReadOnlyDictionary<Guid, IReadOnlyList<Location>> locationsByAccount)
    {
        var accountRepository = new Mock<IProviderAccountRepository>();
        _ = accountRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts.Select(a => a.ToDto()).ToList());

        var locationRepository = new Mock<ILocationRepository>();
        _ = locationRepository
            .Setup(r => r.ListByProviderAccountIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid accountId, CancellationToken _) =>
                locationsByAccount.TryGetValue(accountId, out IReadOnlyList<Location>? locations) ? (IReadOnlyList<LocationRecordDto>)locations.Select(l => l.ToDto()).ToList() : Array.Empty<LocationRecordDto>());

        // No user row: the rail falls back to the provider name for each account's label.
        var userRepository = new Mock<IUserRepository>();
        _ = userRepository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);

        _ = services.AddScoped(_ => accountRepository.Object);
        _ = services.AddScoped(_ => locationRepository.Object);
        _ = services.AddScoped(_ => userRepository.Object);
        _ = services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Scope.DeviceOwnershipService>();
    }
}
