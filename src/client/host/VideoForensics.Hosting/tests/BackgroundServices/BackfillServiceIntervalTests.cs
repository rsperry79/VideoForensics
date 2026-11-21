using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices;

public class BackfillServiceIntervalTests
{
    private static IServiceScopeFactory ScopeFactory() =>
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public void JammingAnalysisBackfillService_DefaultConfig_Uses15Minutes()
    {
        var service = new JammingAnalysisBackfillService(ScopeFactory(), NullLogger<JammingAnalysisBackfillService>.Instance, config: new ForensicsConfiguration());

        Assert.Equal(TimeSpan.FromMinutes(15), service.CurrentInterval);
    }

    [Fact]
    public void JammingAnalysisBackfillService_ConfigChanged_UsesNewInterval()
    {
        var config = new ForensicsConfiguration();
        var service = new JammingAnalysisBackfillService(ScopeFactory(), NullLogger<JammingAnalysisBackfillService>.Instance, config: config);

        config.JammingIntervalMinutes = 7;

        Assert.Equal(TimeSpan.FromMinutes(7), service.CurrentInterval);
    }

    [Fact]
    public void MediaFlagBackfillService_DefaultConfig_Uses15Minutes()
    {
        var service = new MediaFlagBackfillService(ScopeFactory(), NullLogger<MediaFlagBackfillService>.Instance, config: new ForensicsConfiguration());

        Assert.Equal(TimeSpan.FromMinutes(15), service.CurrentInterval);
    }

    [Fact]
    public void MediaFlagBackfillService_ConfigChanged_UsesNewInterval()
    {
        var config = new ForensicsConfiguration();
        var service = new MediaFlagBackfillService(ScopeFactory(), NullLogger<MediaFlagBackfillService>.Instance, config: config);

        config.FlagIntervalMinutes = 30;

        Assert.Equal(TimeSpan.FromMinutes(30), service.CurrentInterval);
    }

    [Fact]
    public void MediaCodecBackfillService_DefaultConfig_Uses15Minutes()
    {
        var service = new MediaCodecBackfillService(ScopeFactory(), NullLogger<MediaCodecBackfillService>.Instance, config: new ForensicsConfiguration());

        Assert.Equal(TimeSpan.FromMinutes(15), service.CurrentInterval);
    }

    [Fact]
    public void MediaCodecBackfillService_ConfigChanged_UsesNewInterval()
    {
        var config = new ForensicsConfiguration();
        var service = new MediaCodecBackfillService(ScopeFactory(), NullLogger<MediaCodecBackfillService>.Instance, config: config);

        config.CodecIntervalMinutes = 45;

        Assert.Equal(TimeSpan.FromMinutes(45), service.CurrentInterval);
    }
}
