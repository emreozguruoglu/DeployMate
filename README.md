# DeployMate

A modern **Windows** deployment desktop app for teams that still do **manual file deployments**. DeployMate lets you define multiple **targets** (e.g., DEV/TEST/PREPROD/PROD or parallel customer environments), customize **hooks** before/after transfer, and **securely store credentials**. You can start deployments **in parallel**, toggle **dry-run**, and keep everything versioned in a single JSON config.

> Built with **.NET 9.0 (Windows Forms UI)** and **.NET 8.0 class libraries**.

---

## ✨ Highlights

- **Multi-target, parallel deploys** — run selected or all targets concurrently.
- **SFTP (via SSH.NET)** and (stubbed) FTP client — abstraction for future providers.
- **Hooks** before/after deploy: HTTP webhook, PowerShell, command, and a placeholder for session cleanup.
- **Config & Vault**: targets + app settings persisted under `%APPDATA%\DeployMate`, logs under `%LOCALAPPDATA%\DeployMate\logs`.
- **Sensitive data encrypted** with **DPAPI** (CurrentUser scope).
- **Dark/Light theme**, keyboard shortcuts, logs viewer, import/export config pack.
- **Polly-ready retry options** exposed in model (used by engine/clients as they evolve).
- **Unit tests**: xUnit + coverlet (basic suite).

---

## 🧩 Solution Layout

```
DeployMate.sln
├─ DeployMate.App             # WinForms UI (.NET 9.0-windows)
├─ DeployMate.Core            # Domain interfaces, engine, config models (.NET 8.0)
├─ DeployMate.Transfer        # SFTP/FTP transfer clients (.NET 8.0)
├─ DeployMate.Hooks           # HTTP/PowerShell/Command hooks (.NET 8.0)
├─ DeployMate.Logging         # Serilog adapter (.NET 8.0)
├─ DeployMate.Storage         # DPAPI vault + JSON config store (.NET 8.0)
└─ DeployMate.Tests           # xUnit tests
```

Key packages:
- **SSH.NET** for SFTP
- **FluentFTP** (present, FTP client currently a lightweight stub)
- **Serilog** for structured logging
- **System.Security.Cryptography.ProtectedData** for DPAPI
- **xUnit / coverlet** for tests
- **Polly** exposed in models for retries

---

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- .NET 9 SDK for the UI project + .NET 8 SDK for libraries
- PowerShell 5+ (for PowerShell hooks)

### Build & run
```powershell
git clone <your-repo-url>.git
cd DeployMate
dotnet build
# Run the WinForms app:
dotnet run --project .\DeployMate.App\DeployMate.App.csproj
```

> First launch seeds a **disabled demo target** (SFTP) for orientation. Open **Settings** and **Targets** to configure real environments.

---

## 🖥️ UI Quick Tour

- **Top menu**
  - **File** → Open/Save/Save As (configuration JSON under `%APPDATA%\DeployMate`), Import/Export config pack
  - **Actions** → +Add target, Start Selected (Ctrl+F6), Start All (F5), Stop Selected (Ctrl+F7), Stop All (F5)
  - **View** → Toggle **Dark** mode (Ctrl+D)
  - **Tools** → **Settings** (timeouts, retry policy, default exclusions, log retention), **Logs** viewer
- **Target cards**
  - Each card shows name, environment, protocol, host, remote path, local destination.
  - Start/Cancel buttons, **Dry Run** toggle, **Edit**, **Duplicate**, **Delete**, **Select** checkbox for bulk run.

---

## 🛠️ Configuration

Configuration is stored as JSON under `%APPDATA%\DeployMate`:

- `targets.json` → main list of targets (host string encrypted)
- `targets.ports.json` → sidecar with encrypted port values
- `appsettings.json` → global defaults (timeouts, retries, exclusions, log retention)

> Use **File → Import/Export** to back up or share **both** targets and settings as a single pack file. Sensitive fields remain encrypted with **DPAPI** and bound to the **current Windows user profile**.

### Target model (excerpt)

```jsonc
{
  "id": "GUID",
  "name": "App Server 1",
  "environment": "PREPROD",
  "localDestination": "C:\\builds\\MyApp\\Release",
  "protocol": "Sftp",               // Sftp | Ftp (stub)
  "host": "enc:BASE64...",          // DPAPI-protected in file
  "port": 22,                       // encrypted separately in sidecar file
  "remotePath": "/var/www/app",
  "credential": { "kind": "Dpapi", "key": "prod-sftp" },
  "transfer": {
    "exclusions": ["*.pdb", "node_modules/**"],
    "concurrency": 4,
    "comparisonMode": "SizeTimestamp",    // Timestamp | SizeTimestamp | Checksum
    "retry": { "maxRetries": 3, "baseDelay": "00:00:01" },
    "uploadDirectionLocalToRemote": true
  },
  "preDeploy": { "hooks": [] },
  "postDeploy": { "hooks": [] },
  "defaultDryRun": true,
  "disabled": false
}
```

### App settings (excerpt)

```jsonc
{
  "defaultTimeout": "00:00:30",
  "defaultRetry": { "maxRetries": 3, "baseDelay": "00:00:01" },
  "defaultExclusions": ["*.log", "TestResults/**"],
  "logRetentionDays": 14
}
```

---

## 🔐 Credentials & Security

DeployMate uses a **DPAPI-backed credential vault** at:
```
%APPDATA%\DeployMate\vault\<key>.bin
```
Each file stores `username`, optional `password`, optional `privateKeyPath` and `passphrase`, **encrypted** with **DPAPI (CurrentUser)**. Only the **same Windows user account** can decrypt them.

**Do not commit** these files to source control.

> In JSON, `credential.key` is just a label that maps to a vault file. The actual secrets never live in plain text JSON.

---

## 🧵 Hooks

You can run **hooks** before and/or after the transfer:

- **HTTP**: send webhooks.
  - `Parameters`: `Method` (default POST), `Url`, optional `Body` (supports `{TargetName}` substitution), optional headers via `Header:NAME` keys.
- **PowerShell**: run a local `.ps1` with arguments.
  - `Parameters`: `Script`, optional `Args`, optional `WorkingDir`.
- **Command**: run an arbitrary executable.
  - `Parameters`: `File`, optional `Args`, optional `WorkingDir`.
- **StopSessions**: placeholder for session cleanup logic (extend as needed).

Each hook supports `Timeout` and `ContinueOnError`.

---

## 📦 Transfers

- **SFTP** (recommended): SSH.NET client (password or key-based auth). Validates remote path.
- **FTP**: currently a lightweight stub to unblock UI/flow; replace with a full **FluentFTP** implementation as needed.
- **Comparison modes**: `Timestamp`, `SizeTimestamp`, `Checksum` (model-defined; implementation can be extended).
- **Progress**: bytes, throughput, and human-readable messages are reported back to the UI.

> Set `uploadDirectionLocalToRemote = true` for the typical "copy from local build output to remote server" flow.

---

## ⌨️ Shortcuts

- **Ctrl+N** add target
- **F5 / Ctrl+F6** start (all / selected)
- **Ctrl+F7** stop selected
- **Ctrl+D** toggle dark theme
- **Ctrl+S / Ctrl+Shift+S** save / save as
- **Ctrl+,** open Settings
- **Ctrl+L** open Logs

---

## 🧪 Testing

```powershell
dotnet test
```
- Uses **xUnit** with **Microsoft.NET.Test.Sdk**
- Coverage via **coverlet.collector**

---

## 🗂️ Logs

- Rolling JSON logs written to:
  ```
  %LOCALAPPDATA%\DeployMate\logs
  ```
- View inside the app via **Tools → Logs** or tail with your favorite viewer.

---

## 🤝 Contributing

Please see **CONTRIBUTING.md**, **CODE_OF_CONDUCT.md**, and **SECURITY.md** for details on how to file issues, propose changes, and report vulnerabilities.

---

## 📄 License

This project is released under the **MIT License**. See **LICENSE** for details.
