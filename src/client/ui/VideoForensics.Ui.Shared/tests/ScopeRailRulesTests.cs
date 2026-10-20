namespace VideoForensics.Ui.Shared.Tests;

using VideoForensics.Data.Common.Entities;
using Xunit;
using VideoForensics.Ui.Shared.Services.Scope;
using Device = VideoForensics.Data.Common.Entities.Device;

public class ScopeRailRulesTests
{
    private static ProviderAccount MakeAccount(string providerName, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        ProviderName = providerName,
        LinkedUtc = DateTime.UtcNow,
    };

    private static Device MakeDevice(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        ProviderDeviceId = Guid.NewGuid().ToString(),
        Name = "Camera",
        Type = "camera",
    };

    private static ForensicScope MakeScope(
        IReadOnlyList<Guid>? deviceIds = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null,
        bool matchesNoDevices = false) =>
        new(deviceIds ?? new List<Guid>(), from ?? new DateTime(2026, 9, 1), to ?? new DateTime(2026, 9, 30), search, matchesNoDevices);

    // ---- Visible devices -------------------------------------------------

    [Fact]
    public void VisibleDevices_NoFilters_ReturnsAllDevices()
    {
        var devices = new[] { MakeDevice(), MakeDevice() };

        var visible = ScopeRailRules.VisibleDevices(devices, Array.Empty<DeviceOwnership>(), null, null);

        Assert.Equal(devices.Length, visible.Count);
    }

    [Fact]
    public void VisibleDevices_ProviderFilter_DelegatesToScopeDeviceFilter()
    {
        var ringDevice = MakeDevice();
        var wyzeDevice = MakeDevice();
        var ownership = new[]
        {
            new DeviceOwnership(ringDevice.Id, Guid.NewGuid(), "Ring"),
            new DeviceOwnership(wyzeDevice.Id, Guid.NewGuid(), "Wyze"),
        };

        var visible = ScopeRailRules.VisibleDevices(new[] { ringDevice, wyzeDevice }, ownership, "Ring", null);

        var only = Assert.Single(visible);
        Assert.Equal(ringDevice.Id, only.Id);
    }

    // ---- Provider pick ---------------------------------------------------

    [Fact]
    public void ApplyProviderPick_WhitespaceValue_MeansAllProviders()
    {
        var accounts = new[] { MakeAccount("Ring") };

        var (provider, _) = ScopeRailRules.ApplyProviderPick(accounts, null, "   ");

        Assert.Null(provider);
    }

    [Fact]
    public void ApplyProviderPick_EmptyValue_MeansAllProviders()
    {
        var (provider, _) = ScopeRailRules.ApplyProviderPick(Array.Empty<ProviderAccount>(), null, "");

        Assert.Null(provider);
    }

    [Fact]
    public void ApplyProviderPick_ProviderValue_IsReturnedAsIs()
    {
        var (provider, _) = ScopeRailRules.ApplyProviderPick(Array.Empty<ProviderAccount>(), null, "Ring");

        Assert.Equal("Ring", provider);
    }

    [Fact]
    public void ApplyProviderPick_SelectedAccountOutsideNewProvider_ClearsAccountFilter()
    {
        var wyze = MakeAccount("Wyze");
        var ring = MakeAccount("Ring");
        var accounts = new[] { wyze, ring };

        var (provider, account) = ScopeRailRules.ApplyProviderPick(accounts, wyze.Id, "Ring");

        Assert.Equal("Ring", provider);
        Assert.Null(account);
    }

    [Fact]
    public void ApplyProviderPick_SelectedAccountInNewProvider_KeepsAccountFilter()
    {
        var ring = MakeAccount("Ring");
        var accounts = new[] { ring };

        var (_, account) = ScopeRailRules.ApplyProviderPick(accounts, ring.Id, "Ring");

        Assert.Equal(ring.Id, account);
    }

    [Fact]
    public void ApplyProviderPick_AllProvidersWithAccountSelected_KeepsAccountFilter()
    {
        var ring = MakeAccount("Ring");

        var (provider, account) = ScopeRailRules.ApplyProviderPick(new[] { ring }, ring.Id, "");

        Assert.Null(provider);
        Assert.Equal(ring.Id, account);
    }

    // ---- Account pick ----------------------------------------------------

    [Fact]
    public void ParseAccountPick_GuidValue_ReturnsGuid()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, ScopeRailRules.ParseAccountPick(id.ToString()));
    }

    [Fact]
    public void ParseAccountPick_NonGuidValue_ReturnsNull()
    {
        Assert.Null(ScopeRailRules.ParseAccountPick("not-a-guid"));
    }

    [Fact]
    public void ParseAccountPick_EmptyValue_ReturnsNull()
    {
        Assert.Null(ScopeRailRules.ParseAccountPick(""));
        Assert.Null(ScopeRailRules.ParseAccountPick(null));
    }

    // ---- ApplyFilters ----------------------------------------------------

    [Fact]
    public void ApplyFilters_ProviderFilterActive_SetsDeviceIdsToVisibleAndClearsNoMatch()
    {
        var visibleA = Guid.NewGuid();
        var visibleB = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { Guid.NewGuid() });

        var result = ScopeRailRules.ApplyFilters(current, new[] { visibleA, visibleB }, "Ring", null);

        Assert.Equal(new[] { visibleA, visibleB }, result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ApplyFilters_AccountFilterActiveWithNoVisibleDevices_SetsMatchesNoDevices()
    {
        var current = MakeScope();

        var result = ScopeRailRules.ApplyFilters(current, Array.Empty<Guid>(), null, Guid.NewGuid());

        Assert.Empty(result.DeviceIds);
        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ApplyFilters_NoFilters_ClearsNoMatchAndKeepsDeviceIds()
    {
        var kept = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { kept }, matchesNoDevices: true);

        var result = ScopeRailRules.ApplyFilters(current, new[] { Guid.NewGuid() }, null, null);

        Assert.Equal(new[] { kept }, result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ApplyFilters_PreservesDatesAndSearch()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);
        var current = MakeScope(from: from, to: to, search: "door");

        var result = ScopeRailRules.ApplyFilters(current, new[] { Guid.NewGuid() }, "Ring", null);

        Assert.Equal(from, result.FromUtc);
        Assert.Equal(to, result.ToUtc);
        Assert.Equal("door", result.Search);
    }

    // ---- Device toggle ---------------------------------------------------

    [Fact]
    public void ToggleDevice_CheckedNewDevice_AppendsId()
    {
        var existing = Guid.NewGuid();
        var added = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { existing });

        var result = ScopeRailRules.ToggleDevice(current, added, true, null, null);

        Assert.Equal(new[] { existing, added }, result.DeviceIds);
    }

    [Fact]
    public void ToggleDevice_CheckedAlreadySelectedDevice_DoesNotDuplicate()
    {
        var id = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { id });

        var result = ScopeRailRules.ToggleDevice(current, id, true, null, null);

        Assert.Equal(new[] { id }, result.DeviceIds);
    }

    [Fact]
    public void ToggleDevice_UncheckedDevice_RemovesId()
    {
        var keep = Guid.NewGuid();
        var drop = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { keep, drop });

        var result = ScopeRailRules.ToggleDevice(current, drop, false, null, null);

        Assert.Equal(new[] { keep }, result.DeviceIds);
    }

    [Fact]
    public void ToggleDevice_UncheckLastDeviceUnderProviderFilter_SetsMatchesNoDevices()
    {
        var id = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { id });

        var result = ScopeRailRules.ToggleDevice(current, id, false, "Ring", null);

        Assert.Empty(result.DeviceIds);
        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ToggleDevice_UncheckLastDeviceUnderAccountFilter_SetsMatchesNoDevices()
    {
        var id = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { id });

        var result = ScopeRailRules.ToggleDevice(current, id, false, null, Guid.NewGuid());

        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ToggleDevice_UncheckLastDeviceWithoutFilter_LeavesMatchesNoDevicesFalse()
    {
        var id = Guid.NewGuid();
        var current = MakeScope(deviceIds: new List<Guid> { id });

        var result = ScopeRailRules.ToggleDevice(current, id, false, null, null);

        Assert.Empty(result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ToggleDevice_CheckUnderFilter_ClearsMatchesNoDevices()
    {
        var id = Guid.NewGuid();
        var current = MakeScope(matchesNoDevices: true);

        var result = ScopeRailRules.ToggleDevice(current, id, true, "Ring", null);

        Assert.Equal(new[] { id }, result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    // ---- Date input ------------------------------------------------------

    [Fact]
    public void TryParseDateInput_IsoDate_ReturnsUtcDate()
    {
        var parsed = ScopeRailRules.TryParseDateInput("2026-09-10", out var date);

        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 9, 10), date);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("10/09/2026")]
    [InlineData("2026-13-01")]
    public void TryParseDateInput_InvalidText_ReturnsFalse(string? text)
    {
        var parsed = ScopeRailRules.TryParseDateInput(text, out var date);

        Assert.False(parsed);
        Assert.Equal(default, date);
    }

    [Fact]
    public void SetFromDate_PickedDate_StoresFromUtcAsIs()
    {
        var current = MakeScope();

        var result = ScopeRailRules.SetFromDate(current, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 9, 10), result.FromUtc);
    }

    [Fact]
    public void SetToDate_PickedDate_StoresExclusiveUpperBound()
    {
        var current = MakeScope();

        var result = ScopeRailRules.SetToDate(current, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 9, 11), result.ToUtc);
    }

    [Fact]
    public void SetToDate_ThenFormatToDate_RoundTripsPickedDay()
    {
        var picked = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var scope = ScopeRailRules.SetToDate(MakeScope(), picked);

        Assert.Equal("2026-09-10", ScopeRailRules.FormatToDate(scope.ToUtc));
    }

    [Fact]
    public void FormatFromDate_FromUtc_ShowsInclusiveDay()
    {
        Assert.Equal("2026-09-01", ScopeRailRules.FormatFromDate(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void FormatFromDate_FromUtcWithTimeOfDay_ShowsDateOnly()
    {
        Assert.Equal("2026-09-01", ScopeRailRules.FormatFromDate(new DateTime(2026, 9, 1, 15, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void FormatToDate_ToUtc_ShowsPreviousDay()
    {
        Assert.Equal("2026-09-30", ScopeRailRules.FormatToDate(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    // ---- Quick ranges ----------------------------------------------------

    [Fact]
    public void ApplyQuickRange_SevenDaysBack_SpansFromTodayMinusSevenToTomorrowExclusive()
    {
        var today = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        var current = MakeScope(deviceIds: new List<Guid> { Guid.NewGuid() }, search: "x");

        var result = ScopeRailRules.ApplyQuickRange(current, today, 7);

        Assert.Equal(new DateTime(2026, 9, 17), result.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), result.ToUtc);
        Assert.Equal(current.DeviceIds, result.DeviceIds);
        Assert.Equal("x", result.Search);
    }

    [Fact]
    public void ApplyQuickRange_OneDayBack_SpansYesterdayThroughTomorrow()
    {
        var today = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        var result = ScopeRailRules.ApplyQuickRange(MakeScope(), today, 1);

        Assert.Equal(new DateTime(2026, 9, 23), result.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), result.ToUtc);
    }

    [Fact]
    public void ApplyQuickRange_ThirtyDaysBack_SpansThirtyDays()
    {
        var today = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        var result = ScopeRailRules.ApplyQuickRange(MakeScope(), today, 30);

        Assert.Equal(new DateTime(2026, 8, 25), result.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), result.ToUtc);
    }

    // ---- Search ----------------------------------------------------------

    [Fact]
    public void NormalizeSearch_WhitespaceOnly_ReturnsNull()
    {
        Assert.Null(ScopeRailRules.NormalizeSearch("   "));
        Assert.Null(ScopeRailRules.NormalizeSearch(""));
        Assert.Null(ScopeRailRules.NormalizeSearch(null));
    }

    [Fact]
    public void NormalizeSearch_TextValue_ReturnsTextUnchanged()
    {
        Assert.Equal(" door ", ScopeRailRules.NormalizeSearch(" door "));
    }

    [Fact]
    public void ApplySearch_WhitespaceDraft_SetsSearchToNull()
    {
        var current = MakeScope(search: "old");

        var result = ScopeRailRules.ApplySearch(current, "  ");

        Assert.Null(result.Search);
    }

    [Fact]
    public void ApplySearch_TextDraft_SetsSearch()
    {
        var result = ScopeRailRules.ApplySearch(MakeScope(), "porch");

        Assert.Equal("porch", result.Search);
    }

    // ---- Orphaned no-match flag -----------------------------------------

    [Fact]
    public void ClearOrphanedNoMatch_NoMatchWithBothFiltersNull_ClearsFlag()
    {
        var current = MakeScope(matchesNoDevices: true);

        var result = ScopeRailRules.ClearOrphanedNoMatch(current, null, null);

        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ClearOrphanedNoMatch_NoMatchWithProviderFilter_KeepsFlag()
    {
        var current = MakeScope(matchesNoDevices: true);

        var result = ScopeRailRules.ClearOrphanedNoMatch(current, "Ring", null);

        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ClearOrphanedNoMatch_NoMatchWithAccountFilter_KeepsFlag()
    {
        var current = MakeScope(matchesNoDevices: true);

        var result = ScopeRailRules.ClearOrphanedNoMatch(current, null, Guid.NewGuid());

        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ClearOrphanedNoMatch_FlagAlreadyFalse_ReturnsScopeUnchanged()
    {
        var current = MakeScope();

        var result = ScopeRailRules.ClearOrphanedNoMatch(current, null, null);

        Assert.Equal(current, result);
    }

    // ---- Save scope to case gating --------------------------------------

    [Fact]
    public void ShowSaveScopeToCase_NoActiveCase_ReturnsFalse()
    {
        Assert.False(ScopeRailRules.ShowSaveScopeToCase(false, true, OperatorRole.Admin, false));
    }

    [Fact]
    public void ShowSaveScopeToCase_ScopeMatchesCase_ReturnsFalse()
    {
        Assert.False(ScopeRailRules.ShowSaveScopeToCase(true, false, OperatorRole.Admin, false));
    }

    [Fact]
    public void ShowSaveScopeToCase_RoleNull_ReturnsFalse()
    {
        Assert.False(ScopeRailRules.ShowSaveScopeToCase(true, true, null, false));
    }

    [Fact]
    public void ShowSaveScopeToCase_ReadOnlyRole_ReturnsFalse()
    {
        Assert.False(ScopeRailRules.ShowSaveScopeToCase(true, true, OperatorRole.ReadOnly, false));
    }

    [Fact]
    public void ShowSaveScopeToCase_MatchesNoDevices_ReturnsFalse()
    {
        Assert.False(ScopeRailRules.ShowSaveScopeToCase(true, true, OperatorRole.Admin, true));
    }

    [Theory]
    [InlineData(OperatorRole.Review)]
    [InlineData(OperatorRole.Admin)]
    [InlineData(OperatorRole.SuperAdmin)]
    public void ShowSaveScopeToCase_ReviewOrHigherWithDifferingScope_ReturnsTrue(OperatorRole role)
    {
        Assert.True(ScopeRailRules.ShowSaveScopeToCase(true, true, role, false));
    }
}
