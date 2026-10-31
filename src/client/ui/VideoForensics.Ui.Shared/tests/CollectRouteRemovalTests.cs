using Microsoft.AspNetCore.Components;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Guards the removal of the manual Collect pages. Downloads run only through account sync, so no
/// Razor page in the Ui.Shared assembly may still claim the old Collect routes.
/// </summary>
public class Collect_Routes_Tests
{
    private static readonly string[] RemovedRoutes = ["/collect/videos", "/collect/snapshots"];

    [Fact]
    public void AnyPage_CollectRoute_NotRegistered()
    {
        List<string> registered = typeof(Routes).Assembly
            .GetTypes()
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false).Cast<RouteAttribute>())
            .Select(attribute => attribute.Template)
            .Where(template => RemovedRoutes.Any(removed => template.StartsWith(removed, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(registered);
    }
}
