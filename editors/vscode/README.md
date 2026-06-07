# VS Code Extension

Local development extension (not published to Marketplace yet).

## Install

```bash
cd editors/vscode
npm install
```

1. Open VS Code / Cursor
2. **Developer: Install Extension from Location…** → select `editors/vscode/`

## Features

- TextMate grammar for `.hla64`
- Snippets: `hello`, `proc`
- **Language Server** — diagnostics (incl. bounds warnings), hover, completion, go-to-definition, document symbols, format-on-save via `AstFormatter`, signature help, document highlights/references, semantic tokens (keywords/types/registers/strings), and **Format document** code action

Optional settings:

```json
{
  "hla64.languageServerPath": "dotnet",
  "hla64.languageServerArgs": ["run", "--project", "src/HlaX64.LanguageServer/HlaX64.LanguageServer.csproj"]
}
```

Leave `languageServerArgs` empty to use the path relative to the extension folder.

## Build / run from VS Code

Add to `.vscode/tasks.json`:

```json
{
  "label": "hla64 run",
  "type": "shell",
  "command": "dotnet run --project src/HlaX64.Cli -- run ${file}"
}
```
