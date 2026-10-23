
using Microsoft.AspNetCore.Components;

using System.Reflection;

using VideoForensics.Ui.Shared.Pages;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Pair.razor (device-level passkey registration via api/v1/pairing) is kept but not routed.
/// Checked via the generated component's RouteAttribute, since bUnit renders components directly
/// and does not exercise the router's route table.
/// </summary>
public class PairPageTests
{
    [Fact]
    public void Pair_RouteRemoved_NotRoutable()
    {
        Assert.Null(typeof(Pair).GetCustomAttribute<RouteAttribute>());
    }
}
