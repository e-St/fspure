# Security

## Reporting vulnerabilities

Please **do not** open a public issue for security-sensitive reports.

Email the maintainers via the contact on [GitHub e-St](https://github.com/e-St) or open a **private** security advisory:

**GitHub → Security → Advisories → Report a vulnerability**

(if enabled on this repository)

We aim to acknowledge reports within a reasonable time and coordinate a fix before disclosure.

## Automated controls in this repository

| Control | Where | What it does |
|---------|--------|----------------|
| **Dependabot** | [`.github/dependabot.yml`](../.github/dependabot.yml) | Weekly PRs for GitHub Actions, npm (extension + e2e), NuGet PackageReference projects |
| **Dependency review** | [`.github/workflows/security.yml`](../.github/workflows/security.yml) | On PRs: fail if new dependencies introduce **high**+ severity advisories |
| **CodeQL** | same workflow | Static analysis for **C#** (F#/C# build) and **JavaScript** (extension) |
| **NuGet vulnerable scan** | `scripts/security-audit.sh` | `dotnet list package --vulnerable --include-transitive` on main projects |
| **npm audit** | same script | `npm audit --audit-level=high` for `vscode-extension` and Playwright e2e |
| **Gitleaks** | same workflow | Secret scanning of git history on push/PR/schedule |

### Paket vs Dependabot

`FSharp.PureAnalyzer` and `fspure-collector` use **Paket**. Dependabot does **not** rewrite `paket.dependencies` / `paket.lock`. Those ecosystems are covered by:

- CI `security-audit.sh` (vulnerable package list)
- Manual / planned Paket bumps when advisories appear

PackageReference projects (schema tests, fixtures, samples, BuildTasks) are covered by Dependabot’s NuGet ecosystem.

## Local audit

```bash
# From repo root
bash scripts/security-audit.sh

# Stricter npm (optional)
NPM_AUDIT_LEVEL=moderate bash scripts/security-audit.sh
```

## Release hygiene

- Prefer **Trusted Publishing** to nuget.org (no long-lived API keys) — see [PUBLISHING.md](PUBLISHING.md).
- GitHub Actions tokens: least privilege per workflow (`permissions:` blocks).
- Do not commit secrets, `.nupkg` credentials, or personal access tokens.

## Scope notes

- **OSV / Trivy image scans** of the published `ghcr.io/e-st/fstarter` image live with the image owner (fstarter), not this repo’s app code.
- **VS Code extension** is pure JS with minimal deps; CodeQL + npm audit are the primary scanners.
