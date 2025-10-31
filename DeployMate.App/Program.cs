using DeployMate.Core;
using DeployMate.Hooks;
using DeployMate.Logging;
using DeployMate.Storage;

namespace DeployMate.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var logger = LoggingSetup.CreateFileLogger("DeployMate");
        var vault = new DpapiCredentialVault();
        var config = new JsonConfigurationStore();
        var hooks = new HookRunner();
        var transferFactory = new DeployMate.Transfer.TransferClientFactory();
        var engine = new DeploymentEngine(vault, transferFactory, hooks, logger);

        Application.Run(new Shell(engine, config, logger));
    }    
}