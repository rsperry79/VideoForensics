namespace VideoForensics.Ui.Shared.Tests.Services.QueryApi;

using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Services.QueryApi;
using Xunit;

public class QueryApiFilterState_Tests
{
    [Fact]
    public void QueryApiFilterState_SetLocationId_RaisesOnChange()
    {
        IQueryApiFilterState state = new QueryApiFilterState();
        var raised = 0;
        state.OnChange += () => raised++;

        state.SetLocationId("loc-1");

        Assert.Equal("loc-1", state.LocationId);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void QueryApiFilterState_SetDeviceId_RaisesOnChange()
    {
        IQueryApiFilterState state = new QueryApiFilterState();
        var raised = 0;
        state.OnChange += () => raised++;

        state.SetDeviceId("dev-1");

        Assert.Equal("dev-1", state.DeviceId);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void QueryApiFilterState_SetSameLocationIdTwice_RaisesOnChangeOnce()
    {
        IQueryApiFilterState state = new QueryApiFilterState();
        var raised = 0;
        state.OnChange += () => raised++;

        state.SetLocationId("loc-1");
        state.SetLocationId("loc-1");

        Assert.Equal(1, raised);
    }
}
