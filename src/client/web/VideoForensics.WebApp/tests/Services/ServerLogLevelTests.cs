using Microsoft.Extensions.Logging;

using Serilog.Events;

using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services;

public class ServerLogLevelTests
{
    [Fact]
    public void ServerLogLevel_DebugSetting_ConsoleAndPipeAtDebugFilesNotStoredBelowInformation()
    {
        var level = new ServerLogLevel();

        level.Apply("Debug");

        Assert.Equal(LogLevel.Debug, level.BufferLevel.Minimum);
        Assert.Equal(LogEventLevel.Debug, level.Switch.MinimumLevel);
        Assert.False(ServerLogLevel.StoresInFiles(LogEventLevel.Debug));
        Assert.True(ServerLogLevel.StoresInFiles(LogEventLevel.Information));
    }

    [Fact]
    public void ServerLogLevel_TraceSetting_ConsoleAndPipeAtTraceFilesNotStoredBelowInformation()
    {
        var level = new ServerLogLevel();

        level.Apply("Trace");

        Assert.Equal(LogLevel.Trace, level.BufferLevel.Minimum);
        Assert.Equal(LogEventLevel.Verbose, level.Switch.MinimumLevel);
        Assert.False(ServerLogLevel.StoresInFiles(LogEventLevel.Verbose));
        Assert.False(ServerLogLevel.StoresInFiles(LogEventLevel.Debug));
    }

    [Fact]
    public void ServerLogLevel_ErrorSetting_ConsoleAndPipeAndFilesAtError()
    {
        var level = new ServerLogLevel();

        level.Apply("Error");

        Assert.Equal(LogLevel.Error, level.BufferLevel.Minimum);
        Assert.Equal(LogEventLevel.Error, level.Switch.MinimumLevel);
        Assert.True(ServerLogLevel.StoresInFiles(LogEventLevel.Error));
    }

    [Fact]
    public void ServerLogLevel_InformationSetting_ConsoleAndPipeAndFilesAtInformation()
    {
        var level = new ServerLogLevel();

        level.Apply("Information");

        Assert.Equal(LogLevel.Information, level.BufferLevel.Minimum);
        Assert.Equal(LogEventLevel.Information, level.Switch.MinimumLevel);
        Assert.True(ServerLogLevel.StoresInFiles(LogEventLevel.Information));
    }
}
