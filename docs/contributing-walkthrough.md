# Contributing walkthrough (sample docs PR)

This walkthrough shows a minimal **documentation-only** contribution. The same flow applies to code changes; add `dotnet test` and conformance checks (see [CONTRIBUTING.md](rules/CONTRIBUTING.md)).

## 1. Fork and clone

```bash
git clone https://github.com/YOUR_USER/hlax64.git
cd hlax64
git remote add upstream https://github.com/megaalive/hlax64.git
```

## 2. Branch

```bash
git fetch upstream
git checkout -b docs/my-improvement upstream/main
```

## 3. Edit and verify

Example: fix a typo in `docs/install.md`.

```bash
dotnet build
dotnet test
# If you changed NASM/IR output:
# ./scripts/verify-conformance.sh   (Linux)
# ./scripts/verify-conformance.ps1  (Windows)
```

Optional local hooks (not required):

```bash
# Example: run tests before push (create .git/hooks/pre-push yourself)
dotnet test --no-build
```

## 4. Commit

```bash
git add docs/install.md
git commit -m "docs: clarify Assembly Lab bundle install steps"
```

Use [conventional commits](rules/CONTRIBUTING.md): `feat:`, `fix:`, `docs:`, `test:`, `chore:`.

## 5. Push and open PR

```bash
git push -u origin docs/my-improvement
```

Open a pull request against `main`. The [PR template](../.github/PULL_REQUEST_TEMPLATE.md) checklist applies.

## 6. Review loop

- Address review comments with new commits on the same branch.
- Keep PRs focused — one logical change per PR.
- Link issues: `Fixes #123` in the PR description when applicable.

## Sample PR description (docs-only)

```markdown
## Summary
Clarify Assembly Lab install path on Windows.

## Test plan
- [x] Read rendered markdown
- [x] dotnet build (unchanged code paths)
```

## Next steps

- [CONTRIBUTING.md](rules/CONTRIBUTING.md) — full rules
- [development.md](development.md) — local setup
- [tests/conformance/README.md](../tests/conformance/README.md) — NASM snapshot tests
