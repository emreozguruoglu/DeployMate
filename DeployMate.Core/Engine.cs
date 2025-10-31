using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeployMate.Core;

public sealed class DeploymentEngine : IDeploymentEngine
{
    private readonly ICredentialVault _vault;
    private readonly ITransferClientFactory _transferFactory;
    private readonly IHookRunner _hookRunner;
    private readonly ILogger _log;

    public DeploymentEngine(ICredentialVault vault, ITransferClientFactory transferFactory, IHookRunner hookRunner, ILogger log)
    {
        _vault = vault;
        _transferFactory = transferFactory;
        _hookRunner = hookRunner;
        _log = log;
    }

    public async Task ValidateAsync(TargetConfig target, CancellationToken ct)
    {
        var creds = await _vault.GetAsync(target.Credential.Key, ct);
        await using var client = _transferFactory.Create(target, creds);
        await client.TestConnectionAsync(ct);
        await client.ValidateRemotePathAsync(target.RemotePath, ct);
    }

    public async Task RunAsync(TargetConfig target, bool dryRun, IProgress<DeployProgress> progress, CancellationToken ct)
    {
        progress.Report(new DeployProgress { Status = DeployStatus.Validating, Percent = 0, Message = "Validating" });

        if (dryRun)
        {
            // Dry-run mode: simulate without credentials or remote connections
            progress.Report(new DeployProgress { Status = DeployStatus.Validating, Percent = 10, Message = "Analyzing changes (dry run)" });
            await Task.Delay(300, ct);
            progress.Report(new DeployProgress { Status = DeployStatus.Transferring, Percent = 40, Message = "Would transfer files..." });
            await Task.Delay(500, ct);
            progress.Report(new DeployProgress { Status = DeployStatus.PostDeploy, Percent = 80, Message = "Would run post-deploy hooks..." });
            await Task.Delay(300, ct);
            progress.Report(new DeployProgress { Status = DeployStatus.Completed, Percent = 100, Message = "Dry run completed" });
            return;
        }

        var creds = await _vault.GetAsync(target.Credential.Key, ct);
        await using var client = _transferFactory.Create(target, creds);
        await client.TestConnectionAsync(ct);

        progress.Report(new DeployProgress { Status = DeployStatus.Validating, Percent = 10, Message = "Pre-deploy hooks" });
        await _hookRunner.RunAsync(target.PreDeploy, target, ct);

        progress.Report(new DeployProgress { Status = DeployStatus.Transferring, Percent = 20, Message = "Transferring" });
        await client.SyncAsync(target.LocalDestination, target.RemotePath, target.Transfer, dryRun, progress, ct);

        progress.Report(new DeployProgress { Status = DeployStatus.PostDeploy, Percent = 90, Message = "Post-deploy hooks" });
        await _hookRunner.RunAsync(target.PostDeploy, target, ct);

        progress.Report(new DeployProgress { Status = DeployStatus.Completed, Percent = 100, Message = "Completed" });
        _log.Information("Deployment completed for {Target}", target.Name);
    }
}


