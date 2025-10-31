using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DeployMate.Core;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface ICredentialVault
{
    Task StoreAsync(string key, string username, string? password, string? privateKeyPath, string? passphrase, CancellationToken ct);
    Task<(string username, string? password, string? privateKeyPath, string? passphrase)> GetAsync(string key, CancellationToken ct);
}

public interface IConfigurationStore
{
    Task SaveTargetsAsync(TargetConfig[] targets, CancellationToken ct);
    Task<TargetConfig[]> LoadTargetsAsync(CancellationToken ct);
    Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct);
    Task<AppSettings> LoadAppSettingsAsync(CancellationToken ct);
    Task ExportAsync(string filePath, CancellationToken ct);
    Task ImportAsync(string filePath, CancellationToken ct);
}

public sealed class AppSettings
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public RetryPolicyOptions DefaultRetry { get; set; } = new RetryPolicyOptions();
    public string[] DefaultExclusions { get; set; } = Array.Empty<string>();
    public int LogRetentionDays { get; set; } = 14;
}

public interface ILogger
{
    void Information(string messageTemplate, params object?[]? propertyValues);
    void Warning(string messageTemplate, params object?[]? propertyValues);
    void Error(Exception ex, string messageTemplate, params object?[]? propertyValues);
    void Debug(string messageTemplate, params object?[]? propertyValues);
}

public interface ITransferClient : IAsyncDisposable
{
    Task TestConnectionAsync(CancellationToken ct);
    Task ValidateRemotePathAsync(string remotePath, CancellationToken ct);
    Task<long> ComputeTotalBytesAsync(string localPath, string remotePath, bool uploadDirectionLocalToRemote, string[] exclusions, CancellationToken ct);
    Task SyncAsync(string localPath, string remotePath, TransferOptions options, bool dryRun, IProgress<DeployProgress> progress, CancellationToken ct);
}

public interface ITransferClientFactory
{
    ITransferClient Create(TargetConfig target, (string username, string? password, string? privateKeyPath, string? passphrase) creds);
}

public interface IHookRunner
{
    Task RunAsync(HookSet hooks, TargetConfig target, CancellationToken ct);
}


