# AntiSlowPlugin

AntiSlowPlugin is a CounterStrikeSharp plugin for CS2 that blocks slow-walk (Shift) for targeted players.

## Highlights

- Blocks Shift slow-walk in real time
- Supports temporary blocks (in rounds) and permanent blocks
- Full localization support via JSON files
- Lightweight and production-ready for .NET 8 / CounterStrikeSharp

## Requirements

- Counter-Strike 2 dedicated server
- CounterStrikeSharp installed
- .NET 8 SDK (for local build only)

## Installation (From Release)

1. Download the latest release archive.
2. Extract it at the root of your CS2 server.
3. Confirm this path exists after extraction:

```text
addons/counterstrikesharp/plugins/AntiSlowPlugin/
```

4. Restart the server (or reload CounterStrikeSharp plugins).

## Commands

- `css_antislow <player> [rounds] [reason...]`
- `css_unantislow <player>`
- `css_antislowlist`

## Permission

Admins need:

- `@css/kick`

## Localization

Language files are in `lang/`:

- `en.json`
- `fr.json`
- `de.json`
- `es.json`
- `pt-BR.json`
- `ru.json`
- `zh-Hans.json`

## Build

```powershell
dotnet restore
dotnet build -c Release
```

Build output is generated in:

```text
addons/counterstrikesharp/plugins/AntiSlowPlugin/
```

## Release Layout

```text
addons/
  counterstrikesharp/
    plugins/
      AntiSlowPlugin/
        AntiSlowPlugin.dll
        AntiSlowPlugin.deps.json
        lang/
          en.json
          fr.json
          de.json
          es.json
          pt-BR.json
          ru.json
          zh-Hans.json
```

## Compatibility

Current branch is standalone and does not require external CS2-SimpleAdmin API DLLs to compile.

## Author

- NeuTroNBZh

## License

MIT. See LICENSE.
