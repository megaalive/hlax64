# HlaX64 VS Code Extension

Local development extension (not published to Marketplace yet).

## Install locally

1. Open this folder in VS Code: `editors/vscode/`
2. Run **Developer: Install Extension from Location...** and select this directory.

Or symlink into `.vscode/extensions` for testing.

## Features

- TextMate grammar for `.hla64`
- Snippets: `hello`, `proc`
- Comment toggling via `//`

## Language Server (diagnostics)

Add to `.vscode/settings.json` in the repo root:

```json
{
  "hla64.languageServerPath": "dotnet",
  "hla64.languageServerArgs": ["run", "--project", "src/HlaX64.LanguageServer/HlaX64.LanguageServer.csproj"]
}
```

Or run manually: `dotnet run --project src/HlaX64.LanguageServer`

## Build / run from VS Code

Add to `.vscode/tasks.json` in your project:

```json
{
  "label": "hla64 run",
  "type": "shell",
  "command": "dotnet run --project src/HlaX64.Cli -- run ${file}"
}
```
