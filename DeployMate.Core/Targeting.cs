using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeployMate.Core;

/// <summary>
/// Strongly typed identifier for a deployment target.
/// </summary>
public readonly record struct TargetId(Guid Value)
{
    public static TargetId New() => new TargetId(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Supported transfer protocols.
/// </summary>
public enum Protocol { Ftp, Ftps, Sftp }

/// <summary>
/// Lifecycle status for a deployment run.
/// </summary>
public enum DeployStatus { Idle, Validating, Transferring, PostDeploy, Completed, Canceled, Failed }

/// <summary>
/// Reference to a stored credential in the secure vault.
/// </summary>
public sealed class CredentialRef
{
    public string Kind { get; set; } = "Dpapi"; // e.g., Dpapi
    public string Key { get; set; } = string.Empty; // label/key in vault
}

/// <summary>
/// Per-target configuration.
/// </summary>
public sealed class TargetConfig
{
    public TargetId Id { get; init; } = TargetId.New();
    public string Name { get; set; } = string.Empty;
    public string Environment { get; set; } = "DEV"; // DEV/TEST/PREPROD/PROD
    public string LocalDestination { get; set; } = string.Empty;
    public Protocol Protocol { get; set; } = Protocol.Sftp;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string RemotePath { get; set; } = string.Empty;
    public CredentialRef Credential { get; set; } = new CredentialRef();
    public TransferOptions Transfer { get; set; } = new TransferOptions();
    public HookSet PreDeploy { get; set; } = new HookSet();
    public HookSet PostDeploy { get; set; } = new HookSet();
    public bool DefaultDryRun { get; set; } = true;
    public bool Disabled { get; set; }
}

public sealed class TransferOptions
{
    public string[] Exclusions { get; set; } = Array.Empty<string>();
    public int Concurrency { get; set; } = 4;
    public string ComparisonMode { get; set; } = "SizeTimestamp"; // Timestamp | SizeTimestamp | Checksum
    public RetryPolicyOptions Retry { get; set; } = new RetryPolicyOptions();
    public bool UploadDirectionLocalToRemote { get; set; } = true; // default upload local->remote
}

public sealed class RetryPolicyOptions
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}

public sealed class HookSet
{
    public List<HookConfig> Hooks { get; set; } = new();
}

public enum HookType { Http, PowerShell, Command, StopSessions }

public sealed class HookConfig
{
    public HookType Type { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public bool ContinueOnError { get; set; }
}

public sealed class DeployProgress
{
    public DeployStatus Status { get; init; }
    public double Percent { get; init; }
    public long BytesTransferred { get; init; }
    public long? TotalBytes { get; init; }
    public double? ThroughputMBps { get; init; }
    public string Message { get; init; } = string.Empty;
}

public interface IDeploymentEngine
{
    Task RunAsync(TargetConfig target, bool dryRun, IProgress<DeployProgress> progress, CancellationToken ct);
    Task ValidateAsync(TargetConfig target, CancellationToken ct);
}


