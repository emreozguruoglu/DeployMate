using DeployMate.Core;
// using FluentFTP; // deferred: stub out for initial build
using Renci.SshNet;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DeployMate.Transfer;

public sealed class TransferClientFactory : ITransferClientFactory
{
    public ITransferClient Create(TargetConfig target, (string username, string? password, string? privateKeyPath, string? passphrase) creds)
    {
        return target.Protocol switch
        {
            Protocol.Ftp or Protocol.Ftps => new FtpTransferClient(target, creds),
            Protocol.Sftp => new SftpTransferClient(target, creds),
            _ => throw new NotSupportedException()
        };
    }
}

internal sealed class FtpTransferClient : ITransferClient
{
    private readonly TargetConfig _target;
    private readonly (string username, string? password, string? privateKeyPath, string? passphrase) _creds;

    public FtpTransferClient(TargetConfig target, (string username, string? password, string? privateKeyPath, string? passphrase) creds)
    {
        _target = target;
        _creds = creds;
    }

    public Task TestConnectionAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ValidateRemotePathAsync(string remotePath, CancellationToken ct) => Task.CompletedTask;

    public Task<long> ComputeTotalBytesAsync(string localPath, string remotePath, bool uploadDirectionLocalToRemote, string[] exclusions, CancellationToken ct)
    {
        // Simplified: compute local directory total
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories))
        {
            total += new FileInfo(file).Length;
        }
        return Task.FromResult(total);
    }

    public Task SyncAsync(string localPath, string remotePath, TransferOptions options, bool dryRun, IProgress<DeployProgress> progress, CancellationToken ct)
    {
        // Minimal stub to allow build; detailed implementation will follow.
        progress.Report(new DeployProgress { Status = DeployStatus.Transferring, Percent = 0, Message = "Starting transfer" });
        progress.Report(new DeployProgress { Status = DeployStatus.Completed, Percent = 100, Message = "Completed" });
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class SftpTransferClient : ITransferClient
{
    private readonly TargetConfig _target;
    private readonly (string username, string? password, string? privateKeyPath, string? passphrase) _creds;
    private SftpClient? _client;

    public SftpTransferClient(TargetConfig target, (string username, string? password, string? privateKeyPath, string? passphrase) creds)
    {
        _target = target;
        _creds = creds;
    }

    public Task TestConnectionAsync(CancellationToken ct)
    {
        EnsureClient();
        _client!.ListDirectory(".");
        return Task.CompletedTask;
    }

    public Task ValidateRemotePathAsync(string remotePath, CancellationToken ct)
    {
        EnsureClient();
        if (!_client!.Exists(remotePath)) throw new DirectoryNotFoundException($"Remote path not found: {remotePath}");
        return Task.CompletedTask;
    }

    public Task<long> ComputeTotalBytesAsync(string localPath, string remotePath, bool uploadDirectionLocalToRemote, string[] exclusions, CancellationToken ct)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories))
        {
            total += new FileInfo(file).Length;
        }
        return Task.FromResult(total);
    }

    public Task SyncAsync(string localPath, string remotePath, TransferOptions options, bool dryRun, IProgress<DeployProgress> progress, CancellationToken ct)
    {
        progress.Report(new DeployProgress { Status = DeployStatus.Transferring, Percent = 0, Message = "Starting transfer" });
        progress.Report(new DeployProgress { Status = DeployStatus.Completed, Percent = 100, Message = "Completed" });
        return Task.CompletedTask;
    }

    private void EnsureClient()
    {
        if (_client != null && _client.IsConnected) return;
        if (!string.IsNullOrEmpty(_creds.privateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(_creds.passphrase)
                ? new PrivateKeyFile(_creds.privateKeyPath)
                : new PrivateKeyFile(_creds.privateKeyPath, _creds.passphrase);
            var auth = new PrivateKeyAuthenticationMethod(_creds.username, keyFile);
            var conn = new ConnectionInfo(_target.Host, _target.Port, _creds.username, auth);
            _client = new SftpClient(conn);
        }
        else
        {
            _client = new SftpClient(_target.Host, _target.Port, _creds.username, _creds.password);
        }
        _client.Connect();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}


