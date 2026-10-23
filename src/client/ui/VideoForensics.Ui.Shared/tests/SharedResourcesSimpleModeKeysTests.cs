using Microsoft.Extensions.Localization;
using Xunit;

namespace VideoForensics.Ui.Shared.Tests;

public class SharedResourcesSimpleModeKeysTests
{
    // Every key the Simple Mode UI, the plain-language formatter and the user-menu mode toggle look up.
    // A missing key would silently render as its raw key name, so assert each one resolves.
    private static readonly string[] Keys =
    [
        "SimpleStandardView",
        "SimpleHomeHeading",
        "SimpleHomeLoading",
        "SimpleHomeLoadError",
        "SimpleHomeRetry",
        "SimpleHomeNoActivity",
        "SimpleHomeOlderNote",
        "SimpleHomeBlockedUnavailable",
        "SimpleHomeEvidenceHeading",
        "SimpleHomeNoEvidence",
        "SimpleHomeEvidenceOpen",
        "SimpleHomeEvidenceUnavailable",
        "SimpleDayToday",
        "SimpleDayYesterday",
        "SimpleEvidenceLabel",
        "SimpleEvidenceSnapshot",
        "SimpleEvidenceVideo",
        "PlainDeviceFallback",
        "PlainDeviceNamed",
        "PlainEventDetected",
        "PlainEventMotion",
        "PlainEventPerson",
        "PlainEventPackage",
        "PlainEventGeneric",
        "PlainJammingBlocked",
        "PlainDurationSecond",
        "PlainDurationSeconds",
        "PlainDurationMinute",
        "PlainDurationMinutes",
        "PlainDurationHour",
        "PlainDurationHours",
        "PlainDurationHoursAndMinute",
        "PlainDurationHoursAndMinutes",
        "PlainDurationDay",
        "PlainDurationDays",
        "Switch to Simple view",
        "Switch to Standard view",
        "SignOut",
    ];

    [Fact]
    public void SharedResources_SimpleModeKeys_AllResolveToEnglishText()
    {
        IStringLocalizer localizer = TestLocalizer.Create();

        var missing = Keys.Where(k => localizer[k].ResourceNotFound).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void SharedResources_PlainLanguageTemplates_FormatWithTheirArguments()
    {
        IStringLocalizer localizer = TestLocalizer.Create();

        Assert.Equal("Your door detected motion.", localizer["PlainEventMotion", "Your door"].Value);
        Assert.Equal("Your door was blocked for 5 minutes.", localizer["PlainJammingBlocked", "Your door", localizer["PlainDurationMinutes", 5].Value].Value);
    }
}
