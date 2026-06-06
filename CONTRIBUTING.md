# Contributing to HlaX64

## Project Purpose

HlaX64 is an HLA-inspired x64 assembly compiler and toolchain designed for AI agents and humans. It compiles `.hla64` source files into NASM, assembles and links them into native executables (Linux ELF or Windows PE), and provides test, benchmark, MCP, and interop tooling.

## Who Can Contribute

- **Compiler enthusiasts** — lexer, parser, IR, ABI lowering, code generation
- **Assembly/Systems programmers** — runtime library, linker integration, ABI edge cases
- **.NET / C# developers** — CLI, MCP server, test runner, build tooling
- **Documentation writers** — language spec, tutorials, examples
- **AI/ML engineers** — MCP server, agent integration, prompt engineering
- **Students** — good-first-issue tasks, example programs, test cases

## Development Setup

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [NASM](https://nasm.us) (for native execution)
- **Linux x64 target**: `gcc` or `ld` (or WSL2 on Windows)
- **Windows x64 target**: `lld-link` (via LLVM) or MSVC `link.exe`
- **Recommended**: Git, VS Code or Rider

### Clone & Build

```bash
git clone https://github.com/megaalive/hlax64.git
cd hlax64
dotnet build
dotnet test
```

The build produces:
- `src/HlaX64.Cli/bin/Debug/net10.0/HlaX64.Cli.dll` — the `hla64` CLI
- `src/HlaX64.McpServer/bin/Debug/net10.0/HlaX64.McpServer.dll` — MCP server

### Running native executables

After `dotnet build`, run `.hla64` programs:

```bash
dotnet run --project src/HlaX64.Cli -- run examples/00-getting-started/hello.hla64
dotnet run --project src/HlaX64.Cli -- test examples/
dotnet run --project src/HlaX64.Cli -- bench examples/hello.hla64
```

On Windows, the CLI auto-detects WSL2 for building and running ELF binaries.

## Repository Structure

```
src/
  HlaX64.Compiler/       Lexer, Parser, AST, Semantic, IR, TestRunner
  HlaX64.Backend.Nasm/   NASM x64 code emitter
  HlaX64.Cli/            CLI (hla64)
  HlaX64.Runtime/        Runtime library (Linux + Windows)
  HlaX64.McpServer/      MCP server for AI agents
tests/
  HlaX64.Compiler.Tests/ Unit tests (xUnit)
  samples/               Integration test manifests + expected output
benchmarks/              Benchmark manifests
examples/                .hla64 sample programs
docs/                    Language spec, ABI docs, roadmap
```

## Coding Conventions

- C#: file-scoped namespaces, `Primary Constructor` where appropriate
- Follow existing patterns in the file you are editing
- Keep methods small and focused
- Prefer immutability where feasible
- XML doc comments on public APIs
- Test names: `MethodName_Should_ExpectedBehavior`

## Commit and Pull Request Rules

### Commits

- Use conventional commits: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`
- Keep commits atomic (one logical change per commit)
- Write descriptive commit messages

### Pull Requests

1. Create a feature branch from `main`
2. Make your changes
3. Ensure `dotnet build` passes with **0 warnings**
4. Ensure `dotnet test` passes (all tests)
5. Run native integration tests if applicable
6. Update documentation (language spec, examples, etc.)
7. Open a PR against `main`

### PR Checklist (automatically shown in template)

- [ ] Build passes (0 warnings)
- [ ] Unit tests pass
- [ ] Native integration tests pass (if applicable)
- [ ] Documentation updated
- [ ] Examples updated (if applicable)
- [ ] No unrelated refactoring
- [ ] Breaking changes documented

## Adding a Language Feature

Every new language feature must update:

1. Lexer (new tokens if needed)
2. Parser (grammar rules)
3. AST (new node types)
4. Semantic analyzer (validation)
5. IR (if new semantics)
6. ABI lowerer (if ABI-relevant)
7. NASM emitter (code generation)
8. Unit tests (`HlaX64.Compiler.Tests`)
9. Native integration tests (`tests/samples/`)
10. Language specification (`docs/language-spec.md`)
11. Examples (if user-facing)
12. Release notes

## Adding Diagnostics

- Use error codes documented in [docs/diagnostics.md](docs/diagnostics.md): `HLAX000x` (general/semantic), `HLAX002x` (types). Reserved ranges: `HLAX1xxx` lexer/parser, `HLAX3xxx` ABI, `HLAX4xxx` toolchain, `HLAX5xxx` MCP.
- Each diagnostic must have a clear message, source location, and remediation hint
- Document new diagnostics in `docs/diagnostics.md`

## Adding Examples

- Place example programs in `examples/`
- Follow the naming convention: `kebab-case.hla64`
- Include a comment header describing the purpose
- Add a test manifest in `tests/samples/` if the example should be CI-tested

## Updating the Specification

- Language version must be incremented for breaking changes
- Breaking changes require a migration note
- Update `docs/language-spec.md` header (version, date)
- Mark deprecated features clearly

## Definition of Done

A contribution is complete when:

- [ ] Code compiles with 0 warnings
- [ ] All existing tests pass
- [ ] New tests cover the change
- [ ] Documentation is updated
- [ ] No unrelated changes
- [ ] Breaking changes are called out in the PR description
