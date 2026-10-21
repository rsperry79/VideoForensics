using Fido2NetLib.Objects;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;
using Xunit;

namespace VideoForensics.WebApp.Tests.Api;

/// <summary>Username sign-in offers approved, active passkeys, most recently used first, and keeps all of them.</summary>
public class UsernameAllowListTests
{
    private static OperatorCredential Passkey(byte id, bool approved = true, bool active = true, DateTime? lastUsed = null) => new()
    {
        Id = Guid.NewGuid(),
        WebAuthnCredentialId = Convert.ToBase64String(new[] { id }),
        IsApproved = approved,
        RevokedAtUtc = active ? null : new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        Label = "passkey",
        WebAuthnPublicKey = new byte[] { 0 },
        LastUsedAtUtc = lastUsed,
    };

    private static List<byte> Ids(IEnumerable<PublicKeyCredentialDescriptor> list) =>
        list.Select(d => d.Id[0]).ToList();

    [Fact]
    public void MostRecentlyUsedPasskey_IsOfferedFirst()
    {
        OperatorCredential old = Passkey(1, lastUsed: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        OperatorCredential recent = Passkey(2, lastUsed: new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new byte[] { 2, 1 }, Ids(OperatorAuthEndpoints.UsernameAllowList(new[] { old, recent })));
    }

    [Fact]
    public void NeverUsedPasskey_SortsAfterUsedOnes()
    {
        OperatorCredential never = Passkey(1, lastUsed: null);
        OperatorCredential used = Passkey(2, lastUsed: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new byte[] { 2, 1 }, Ids(OperatorAuthEndpoints.UsernameAllowList(new[] { never, used })));
    }

    [Fact]
    public void UnapprovedAndInactivePasskeys_AreExcluded()
    {
        OperatorCredential good = Passkey(1);
        OperatorCredential pending = Passkey(2, approved: false);
        OperatorCredential retired = Passkey(3, active: false);

        Assert.Equal(new byte[] { 1 }, Ids(OperatorAuthEndpoints.UsernameAllowList(new[] { good, pending, retired })));
    }

    [Fact]
    public void EveryApprovedPasskey_IsKeptSoAnOperatorIsNeverLockedOut()
    {
        var all = Enumerable.Range(1, 4).Select(i => Passkey((byte)i, lastUsed: new DateTime(2026, 10, i, 0, 0, 0, DateTimeKind.Utc))).ToList();

        Assert.Equal(4, OperatorAuthEndpoints.UsernameAllowList(all).Count);
    }
}
