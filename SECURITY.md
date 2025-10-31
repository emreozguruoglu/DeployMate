# Security Policy

## Supported Versions
Security fixes are applied on the `main` branch. If you use a tagged release, prefer the latest.

## Reporting a Vulnerability
Please do **not** file public issues for sensitive security reports.
Instead, create a **private** report via your preferred channel, or label an issue as `security` and mark details as confidential.

Provide:
- Affected version/commit
- Environment (OS, .NET SDK)
- Steps to reproduce
- Potential impact

## Secrets and Storage
- Credentials are encrypted via **DPAPI (CurrentUser)** in `%APPDATA%\DeployMate\vault`.
- Config JSON files store **encrypted** host and sidecar-encrypted ports.
- Never commit vault files or exported packs containing secrets.

## Hardening Recommendations
- Use **key-based SFTP** with passphrase instead of passwords.
- Run DeployMate from a **non-admin** Windows user if possible.
- Restrict file permissions on `%APPDATA%\DeployMate` and `%LOCALAPPDATA%\DeployMate\logs`.
