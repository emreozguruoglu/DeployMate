using DeployMateCoreLogger = DeployMate.Core.ILogger;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace DeployMate.Logging;

public static class LoggingSetup
{
    public static DeployMateCoreLogger CreateFileLogger(string appName)
    {
        string logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "logs");
        Directory.CreateDirectory(logsDir);
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logsDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(new Serilog.Formatting.Compact.CompactJsonFormatter(), Path.Combine(logsDir, "deploymate.json"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, shared: true)
            .CreateLogger();
        return new SerilogAdapter(logger);
    }
}

internal sealed class SerilogAdapter : DeployMateCoreLogger, IDisposable
{
    private readonly Serilog.ILogger _logger;
    public SerilogAdapter(Serilog.ILogger logger) => _logger = logger;

    public void Information(string messageTemplate, params object?[]? propertyValues) => _logger.Information(messageTemplate, propertyValues);
    public void Warning(string messageTemplate, params object?[]? propertyValues) => _logger.Warning(messageTemplate, propertyValues);
    public void Error(Exception ex, string messageTemplate, params object?[]? propertyValues) => _logger.Error(ex, messageTemplate, propertyValues);
    public void Debug(string messageTemplate, params object?[]? propertyValues) => _logger.Debug(messageTemplate, propertyValues);

    public void Dispose()
    {
        ( _logger as IDisposable )?.Dispose();
    }
}


