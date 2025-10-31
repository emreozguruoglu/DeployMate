# Architecture

## Overview

DeployMate separates concerns into **UI**, **domain**, **infrastructure**, and **integration** layers.

```
DeployMate.App (WinForms, .NET 9.0-windows)
  └─ Uses Engine + Config Store + Vault + HookRunner + TransferClientFactory

DeployMate.Core (.NET 8.0)
  ├─ DeploymentEngine (IDeploymentEngine)
  ├─ Domain models (TargetConfig, TransferOptions, HookSet, etc.)
  ├─ Abstractions (ITransferClient, ITransferClientFactory, IConfigurationStore, ICredentialVault, ILogger, IHookRunner)
  └─ AppSettings, retry policies, progress DTOs

DeployMate.Storage (.NET 8.0)
  ├─ JsonConfigurationStore (targets/settings persistence in %APPDATA%)
  └─ DpapiCredentialVault (vault files in %APPDATA%\DeployMate\vault)

DeployMate.Transfer (.NET 8.0)
  ├─ SftpTransferClient (SSH.NET)
  └─ FtpTransferClient (stub; replace with FluentFTP)

DeployMate.Hooks (.NET 8.0)
  └─ HookRunner (HTTP, PowerShell, Command, StopSessions placeholder)

DeployMate.Logging (.NET 8.0)
  └─ Serilog adapter (file sink; compact JSON)
```

## Data Flow

1. **UI** loads targets and settings from `IConfigurationStore`.
2. User starts one or more deployments (optionally **Dry Run**).
3. `DeploymentEngine`:
   - Validates credentials via `ICredentialVault` and tests remote connectivity using `ITransferClient` from `ITransferClientFactory`.
   - Executes **pre-deploy hooks**.
   - Performs **file sync** with progress reporting.
   - Executes **post-deploy hooks**.
4. **Logger** records structured events to disk; UI provides a log viewer.
5. **Storage** persists changes; sensitive fields (host/port + vault) are DPAPI-encrypted.

## Security Notes

- Secrets are **never** written in plaintext JSON. The vault binds secrets to the **CurrentUser** DPAPI scope.
- JSON stores **encrypted** `host` and a **sidecar** file for encrypted `port` values per `TargetId`.
- Import/Export uses the same protections, enabling safe sharing between user profiles on the **same machine**. For cross-user sharing, re-enter credentials.

## Extensibility Points

- Implement new protocols by adding an `ITransferClient` and wiring it in `ITransferClientFactory`.
- Add new **HookType** and corresponding execution path in `HookRunner`.
- Enhance **comparison modes** (e.g., full hashing / rsync-like diffs).
- Plug additional sinks or telemetry in `Logging` project.
