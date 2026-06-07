# HlaX64 Governance

HlaX64 uses a lightweight **BDFL** (Benevolent Dictator For Life) model during the 0.x development phase.

## Project Lead

| Role | Responsibility |
|------|----------------|
| **Project lead / BDFL** | Final language design decisions, release approval, security decisions, maintainer appointment |

Current lead: [@megaalive](https://github.com/megaalive)

## Decision Process

### Language changes

1. Open a **Language Proposal** issue (`.github/ISSUE_TEMPLATE/language_proposal.yml`) or start a GitHub Discussion.
2. Describe motivation, syntax, semantics, ABI impact, compatibility, and test plan.
3. Small, uncontroversial fixes may go directly to a PR.
4. Breaking changes require a migration note and language version bump (see [docs/compatibility.md](../compatibility.md)).

### Releases

- Pre-1.0 releases use semver with `-alpha`/`-beta` suffixes when needed.
- Release notes live in [CHANGELOG.md](../../CHANGELOG.md).
- The BDFL approves tagged releases on `main`.

### Security

- Follow [SECURITY.md](SECURITY.md). Do not discuss vulnerabilities in public issues.
- MCP execution boundaries are documented in [docs/mcp-security.md](../mcp-security.md).

## Code Ownership

See [.github/CODEOWNERS](../../.github/CODEOWNERS). The lead reviews changes in core compiler, runtime, CLI, and MCP areas.

## Labels

Use GitHub labels consistently:

| Kind | Examples |
|------|----------|
| **Type** | `type: bug`, `type: feature`, `type: docs`, `type: test`, `type: security` |
| **Area** | `area: parser`, `area: semantic`, `area: IR`, `area: ABI`, `area: runtime`, `area: CLI`, `area: MCP` |
| **Contribution** | `good first issue`, `help wanted`, `needs design`, `breaking change` |

## Becoming a Maintainer

Regular, high-quality contributions over time may lead to expanded review rights. Express interest via GitHub Discussion or by helping with labeled `help wanted` issues.
