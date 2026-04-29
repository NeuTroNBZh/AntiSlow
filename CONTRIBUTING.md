# Contributing

Thanks for your interest in contributing to AntiSlowPlugin.

## Development Setup

1. Install .NET 8 SDK.
2. Clone the repository.
3. Build locally:

```powershell
dotnet restore
dotnet build -c Release
```

## Pull Request Guidelines

1. Create a feature branch from `main`.
2. Keep changes focused and documented.
3. Update `CHANGELOG.md` when relevant.
4. Ensure build passes before opening the PR.

## Code Style

- Keep code simple and explicit.
- Add comments only where logic is non-obvious.
- Preserve existing naming and architecture conventions.

## Reporting Bugs

Please include:

- Server environment and CounterStrikeSharp version
- Plugin version
- Steps to reproduce
- Expected behavior and actual behavior
- Relevant logs
