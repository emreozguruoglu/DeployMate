# Contributing

Thanks for considering a contribution!

## Ways to Help
- Report bugs and request features via **Issues**.
- Improve docs (README, examples).
- Add tests (unit/integration).
- Implement new transfer providers (e.g., full FTP/FTPS, WebDAV) or new hooks.

## Development Setup
```bash
git clone <repo-url>
cd DeployMate
dotnet build
dotnet test
```

## Branch & PR
- Use a feature branch: `feature/<topic>` or `fix/<topic>`.
- Keep PRs focused and small where possible.
- Include **tests** and **docs** for new behavior.
- PR description checklist:
  - [ ] Motivation & context
  - [ ] Implementation notes
  - [ ] Tests
  - [ ] Docs updated
  - [ ] Breaking changes (if any)

## Commit Style
- Conventional-ish messages are appreciated: `feat: ...`, `fix: ...`, `docs: ...`, `refactor: ...`

## Code Style
- C# 12+, Nullable **enabled**.
- Prefer async/await, `IAsyncDisposable` for network clients.
- Avoid leaking secrets in logs.
- Respect `EditorConfig` if present.

## Releases
- We recommend GitHub Actions workflows for build/test artifacts and draft releases.
- Version using semver: `MAJOR.MINOR.PATCH`.
