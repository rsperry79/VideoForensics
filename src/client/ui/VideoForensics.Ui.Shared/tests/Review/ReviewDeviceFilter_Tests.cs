using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review page's camera visibility rules: which cameras the Provider and Account filters leave, and how selected cameras are clamped.</summary>
public class ReviewDeviceFilter_Tests
{
    private static readonly Guid RingAccount = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WyzeAccount = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RingFront = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RingBack = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid WyzeGarage = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid Unowned = new("aaaaaaaa-0000-0000-0000-000000000004");

    private static readonly Guid Porch = new("33333333-3333-3333-3333-333333333331");
    private static readonly Guid Garage = new("33333333-3333-3333-3333-333333333332");

    private static readonly Guid[] All = [RingFront, RingBack, WyzeGarage, Unowned];

    private static readonly List<DeviceOwnership> Ownership =
    [
        new(RingFront, RingAccount, "Ring", Porch),
        new(RingBack, RingAccount, "Ring", Garage),
        new(WyzeGarage, WyzeAccount, "Wyze", Garage),
    ];

    [Fact]
    public void ReviewDeviceFilter_ProviderFilter_KeepsOnlyThatProvidersDevices()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, "Wyze", null);

        Assert.Equal([WyzeGarage], visible);
    }

    [Fact]
    public void ReviewDeviceFilter_AccountFilter_KeepsOnlyThatAccountsDevices()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, null, RingAccount);

        Assert.Equal([RingFront, RingBack], visible);
    }

    [Fact]
    public void ReviewDeviceFilter_ProviderAndAccount_KeepsDevicesMatchingBoth()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, "Ring", RingAccount);

        Assert.Equal([RingFront, RingBack], visible);
    }

    [Fact]
    public void ReviewDeviceFilter_ProviderAndAccountDisagree_KeepsNothing()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, "Wyze", RingAccount);

        Assert.Empty(visible);
    }

    [Fact]
    public void ReviewDeviceFilter_NoFilter_KeepsEveryDeviceInOrder()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, null, null);

        Assert.Equal(All, visible);
    }

    [Fact]
    public void ReviewDeviceFilter_NoOwnershipData_KeepsEveryDeviceEvenWhenFiltered()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, null, "Wyze", null);

        Assert.Equal(All, visible);
    }

    [Fact]
    public void ReviewDeviceFilter_FilterSet_HidesDevicesWithoutAnOwnershipRow()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, "Ring", null);

        Assert.DoesNotContain(Unowned, visible);
    }

    [Fact]
    public void ReviewDeviceFilter_LocationFilter_KeepsOnlyDevicesAtThatLocation()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, null, null, Garage);

        Assert.Equal([RingBack, WyzeGarage], visible);
    }

    [Fact]
    public void ReviewDeviceFilter_LocationAndProvider_KeepsDevicesMatchingBoth()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, Ownership, "Ring", null, Garage);

        Assert.Equal([RingBack], visible);
    }

    [Fact]
    public void ReviewDeviceFilter_NoOwnershipDataWithLocation_KeepsEveryDevice()
    {
        IReadOnlyList<Guid> visible = ReviewDeviceFilter.VisibleDeviceIds(All, null, null, null, Garage);

        Assert.Equal(All, visible);
    }

    [Fact]
    public void ReviewDeviceFilter_ClampCameras_DropsInvisibleCamerasAndKeepsOrder()
    {
        IReadOnlyList<Guid> clamped = ReviewDeviceFilter.ClampCameras([WyzeGarage, RingBack, Unowned, RingFront], [RingFront, WyzeGarage]);

        Assert.Equal([WyzeGarage, RingFront], clamped);
    }

    [Fact]
    public void ReviewDeviceFilter_ClampCameras_NothingVisibleLeftReturnsEmpty()
    {
        IReadOnlyList<Guid> clamped = ReviewDeviceFilter.ClampCameras([RingBack], [WyzeGarage]);

        Assert.Empty(clamped);
    }
}
