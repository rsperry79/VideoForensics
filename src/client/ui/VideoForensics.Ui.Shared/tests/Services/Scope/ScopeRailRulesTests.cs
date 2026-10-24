using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class ScopeRailRulesTests
{
    private static ProviderAccount MakeAccount(string providerName, Guid? id = null)
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProviderName = providerName,
            LinkedUtc = DateTime.UtcNow,
        };
    }

    private static Device MakeDevice(Guid? id = null)
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = Guid.NewGuid().ToString(),
            Name = "Camera",
            Type = "camera",
        };
    }

    private static ForensicScope MakeScope(
        IReadOnlyList<Guid>? deviceIds = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null,
        bool matchesNoDevices = false)
    {
        return new(deviceIds ?? [], from ?? new DateTime(2026, 9, 1), to ?? new DateTime(2026, 9, 30), search, matchesNoDevices);
    }

    // ---- Visible devices -------------------------------------------------

    [Fact]
    public void VisibleDevices_NoFilters_ReturnsAllDevices()
    {
        Device[] devices = new[] { MakeDevice(), MakeDevice() };

        IReadOnlyList<Device> visible = ScopeRailRules.VisibleDevices(devices, Array.Empty<DeviceOwnership>(), null, null);

        Assert.Equal(devices.Length, visible.Count);
    }

    [Fact]
    public void VisibleDevices_ProviderFilter_DelegatesToScopeDeviceFilter()
    {
        Device ringDevice = MakeDevice();
        Device wyzeDevice = MakeDevice();
        DeviceOwnership[] ownership = new[]
        {
            new DeviceOwnership(ringDevice.Id, Guid.NewGuid(), "Ring"),
            new DeviceOwnership(wyzeDevice.Id, Guid.NewGuid(), "Wyze"),
        };

        IReadOnlyList<Device> visible = ScopeRailRules.VisibleDevices(new[] { ringDevice, wyzeDevice }, ownership, "Ring", null);

        Device only = Assert.Single(visible);
        Assert.Equal(ringDevice.Id, only.Id);
    }

    // ---- Provider pick ---------------------------------------------------

    [Fact]
    public void ApplyProviderPick_WhitespaceValue_MeansAllProviders()
    {
        ProviderAccount[] accounts = new[] { MakeAccount("Ring") };

        (string? provider, Guid? _) = ScopeRailRules.ApplyProviderPick(accounts, null, "   ");

        Assert.Null(provider);
    }

    [Fact]
    public void ApplyProviderPick_EmptyValue_MeansAllProviders()
    {
        (string? provider, Guid? _) = ScopeRailRules.ApplyProviderPick(Array.Empty<ProviderAccount>(), null, string.Empty);

        Assert.Null(provider);
    }

    [Fact]
    public void ApplyProviderPick_ProviderValue_IsReturnedAsIs()
    {
        (string? provider, Guid? _) = ScopeRailRules.ApplyProviderPick(Array.Empty<ProviderAccount>(), null, "Ring");

        Assert.Equal("Ring", provider);
    }

    [Fact]
    public void ApplyProviderPick_SelectedAccountOutsideNewProvider_ClearsAccountFilter()
    {
        ProviderAccount wyze = MakeAccount("Wyze");
        ProviderAccount ring = MakeAccount("Ring");
        ProviderAccount[] accounts = new[] { wyze, ring };

        (string? provider, Guid? account) = ScopeRailRules.ApplyProviderPick(accounts, wyze.Id, "Ring");

        Assert.Equal("Ring", provider);
        Assert.Null(account);
    }

    [Fact]
    public void ApplyProviderPick_SelectedAccountInNewProvider_KeepsAccountFilter()
    {
        ProviderAccount ring = MakeAccount("Ring");
        ProviderAccount[] accounts = new[] { ring };

        (string _, Guid? account) = ScopeRailRules.ApplyProviderPick(accounts, ring.Id, "Ring");

        Assert.Equal(ring.Id, account);
    }

    [Fact]
    public void ApplyProviderPick_AllProvidersWithAccountSelected_KeepsAccountFilter()
    {
        ProviderAccount ring = MakeAccount("Ring");

        (string? provider, Guid? account) = ScopeRailRules.ApplyProviderPick(new[] { ring }, ring.Id, string.Empty);

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
        Assert.Null(ScopeRailRules.ParseAccountPick(string.Empty));
        Assert.Null(ScopeRailRules.ParseAccountPick(null));
    }

    // ---- ApplyFilters ----------------------------------------------------

    [Fact]
    public void ApplyFilters_ProviderFilterActive_SetsDeviceIdsToVisibleAndClearsNoMatch()
    {
        var visibleA = Guid.NewGuid();
        var visibleB = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [Guid.NewGuid()]);

        ForensicScope result = ScopeRailRules.ApplyFilters(current, new[] { visibleA, visibleB }, "Ring", null);

        Assert.Equal(new[] { visibleA, visibleB }, result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ApplyFilters_AccountFilterActiveWithNoVisibleDevices_SetsMatchesNoDevices()
    {
        ForensicScope current = MakeScope();

        ForensicScope result = ScopeRailRules.ApplyFilters(current, Array.Empty<Guid>(), null, Guid.NewGuid());

        Assert.Empty(result.DeviceIds);
        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ApplyFilters_NoFilters_ClearsNoMatchAndKeepsDeviceIds()
    {
        var kept = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [kept], matchesNoDevices: true);

        ForensicScope result = ScopeRailRules.ApplyFilters(current, new[] { Guid.NewGuid() }, null, null);

        Assert.Equal(new[] { kept }, result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ApplyFilters_PreservesDatesAndSearch()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);
        ForensicScope current = MakeScope(from: from, to: to, search: "door");

        ForensicScope result = ScopeRailRules.ApplyFilters(current, new[] { Guid.NewGuid() }, "Ring", null);

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
        ForensicScope current = MakeScope(deviceIds: [existing]);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, added, true, null, null);

        Assert.Equal(new[] { existing, added }, result.DeviceIds);
    }

    [Fact]
    public void ToggleDevice_CheckedAlreadySelectedDevice_DoesNotDuplicate()
    {
        var id = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [id]);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, id, true, null, null);

        Assert.Equal(new[] { id }, result.DeviceIds);
    }

    [Fact]
    public void ToggleDevice_UncheckedDevice_RemovesId()
    {
        var keep = Guid.NewGuid();
        var drop = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [keep, drop]);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, drop, false, null, null);

        Assert.Equal(new[] { keep }, result.DeviceIds);
    }

    [Fact]
    public void ToggleDevice_UncheckLastDeviceUnderProviderFilter_SetsMatchesNoDevices()
    {
        var id = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [id]);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, id, false, "Ring", null);

        Assert.Empty(result.DeviceIds);
        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ToggleDevice_UncheckLastDeviceUnderAccountFilter_SetsMatchesNoDevices()
    {
        var id = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [id]);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, id, false, null, Guid.NewGuid());

        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ToggleDevice_UncheckLastDeviceWithoutFilter_LeavesMatchesNoDevicesFalse()
    {
        var id = Guid.NewGuid();
        ForensicScope current = MakeScope(deviceIds: [id]);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, id, false, null, null);

        Assert.Empty(result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ToggleDevice_CheckUnderFilter_ClearsMatchesNoDevices()
    {
        var id = Guid.NewGuid();
        ForensicScope current = MakeScope(matchesNoDevices: true);

        ForensicScope result = ScopeRailRules.ToggleDevice(current, id, true, "Ring", null);

        Assert.Equal(new[] { id }, result.DeviceIds);
        Assert.False(result.MatchesNoDevices);
    }

    // ---- Date input ------------------------------------------------------

    [Fact]
    public void TryParseDateInput_IsoDate_ReturnsUtcDate()
    {
        bool parsed = ScopeRailRules.TryParseDateInput("2026-09-10", out DateTime date);

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
        bool parsed = ScopeRailRules.TryParseDateInput(text, out DateTime date);

        Assert.False(parsed);
        Assert.Equal(default, date);
    }

    [Fact]
    public void SetFromDate_PickedDate_StoresFromUtcAsIs()
    {
        ForensicScope current = MakeScope();

        ForensicScope result = ScopeRailRules.SetFromDate(current, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 9, 10), result.FromUtc);
    }

    [Fact]
    public void SetToDate_PickedDate_StoresExclusiveUpperBound()
    {
        ForensicScope current = MakeScope();

        ForensicScope result = ScopeRailRules.SetToDate(current, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 9, 11), result.ToUtc);
    }

    [Fact]
    public void SetToDate_ThenFormatToDate_RoundTripsPickedDay()
    {
        var picked = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        ForensicScope scope = ScopeRailRules.SetToDate(MakeScope(), picked);

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
        ForensicScope current = MakeScope(deviceIds: [Guid.NewGuid()], search: "x");

        ForensicScope result = ScopeRailRules.ApplyQuickRange(current, today, 7);

        Assert.Equal(new DateTime(2026, 9, 17), result.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), result.ToUtc);
        Assert.Equal(current.DeviceIds, result.DeviceIds);
        Assert.Equal("x", result.Search);
    }

    [Fact]
    public void ApplyQuickRange_OneDayBack_SpansYesterdayThroughTomorrow()
    {
        var today = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        ForensicScope result = ScopeRailRules.ApplyQuickRange(MakeScope(), today, 1);

        Assert.Equal(new DateTime(2026, 9, 23), result.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), result.ToUtc);
    }

    [Fact]
    public void ApplyQuickRange_ThirtyDaysBack_SpansThirtyDays()
    {
        var today = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        ForensicScope result = ScopeRailRules.ApplyQuickRange(MakeScope(), today, 30);

        Assert.Equal(new DateTime(2026, 8, 25), result.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), result.ToUtc);
    }

    // ---- Search ----------------------------------------------------------

    [Fact]
    public void NormalizeSearch_WhitespaceOnly_ReturnsNull()
    {
        Assert.Null(ScopeRailRules.NormalizeSearch("   "));
        Assert.Null(ScopeRailRules.NormalizeSearch(string.Empty));
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
        ForensicScope current = MakeScope(search: "old");

        ForensicScope result = ScopeRailRules.ApplySearch(current, "  ");

        Assert.Null(result.Search);
    }

    [Fact]
    public void ApplySearch_TextDraft_SetsSearch()
    {
        ForensicScope result = ScopeRailRules.ApplySearch(MakeScope(), "porch");

        Assert.Equal("porch", result.Search);
    }

    // ---- Orphaned no-match flag -----------------------------------------

    [Fact]
    public void ClearOrphanedNoMatch_NoMatchWithBothFiltersNull_ClearsFlag()
    {
        ForensicScope current = MakeScope(matchesNoDevices: true);

        ForensicScope result = ScopeRailRules.ClearOrphanedNoMatch(current, null, null);

        Assert.False(result.MatchesNoDevices);
    }

    [Fact]
    public void ClearOrphanedNoMatch_NoMatchWithProviderFilter_KeepsFlag()
    {
        ForensicScope current = MakeScope(matchesNoDevices: true);

        ForensicScope result = ScopeRailRules.ClearOrphanedNoMatch(current, "Ring", null);

        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ClearOrphanedNoMatch_NoMatchWithAccountFilter_KeepsFlag()
    {
        ForensicScope current = MakeScope(matchesNoDevices: true);

        ForensicScope result = ScopeRailRules.ClearOrphanedNoMatch(current, null, Guid.NewGuid());

        Assert.True(result.MatchesNoDevices);
    }

    [Fact]
    public void ClearOrphanedNoMatch_FlagAlreadyFalse_ReturnsScopeUnchanged()
    {
        ForensicScope current = MakeScope();

        ForensicScope result = ScopeRailRules.ClearOrphanedNoMatch(current, null, null);

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
