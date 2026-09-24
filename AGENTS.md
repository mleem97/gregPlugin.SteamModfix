# AGENTS.md — Notes for AI agents (gregPlugin.SteamModfix)

Repo: https://github.com/mleem97/gregPlugin.SteamModfix · License: MIT · Version: see `VERSION` (0.1.0).

MelonPlugin for Data Center (`DataCenter-SteamPlugin/`). Early-loading Steam
workshop plugin shim: workshop mod discovery and loading (`WorkshopModLoader`,
`Discovery`, `Sources`, `Staging`), configuration (`Configuration`),
diagnostics (`Diagnostics`), integration shims (`Integration`).

## Duties

1. **Read first:** `README.md`, `QUICKSTART.md`, `docs/INDEX.md` (if present),
   `CONTRIBUTING.md` — only then make changes.
2. **Do not commit secrets** (keys, tokens, `.env`). Use keys only via environment variables.
3. **Preserve history:** no `push --force`, no history rewrite without instruction.
4. **Verify changes:** before reporting done, build and test whatever the repo
   provides (`QUICKSTART.md`, `scripts/`, `tests/`).
5. **Keep docs in sync:** for new features update `README.md` + `docs/` + `CHANGELOG.md` (Unreleased).
6. **Conventions:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), one logical change per commit.
7. **When unsure:** stop and ask instead of guessing — especially for deletes, migrations, CI.

## Build and references

- Project root is `DataCenter-SteamPlugin/` (solution `DataCenter-SteamPlugin.sln`).
- This is a **plugin** (`: MelonPlugin`): deploys to `Data Center/Plugins/`,
  loads before mods, and must never depend on mod load order.
- `references/` holds symlinks into the Steam install. Never commit
  `references/*.dll`, `bin/`, or `obj/`.

## Hard rules

- **Early-init ordering is load-bearing.** `StartupBootstrap` must run before
  `MelonLoader.Core.Initialize` for same-launch support. Never regress this.
- **Never break third-party init.** External plugin/mod handling is
  try/caught; probe failures log one line and idle out.
- Security: `SECURITY.md` applies; workshop payloads stay inside staging
  roots (no path traversal, no writes outside).

## Layout

- `DataCenter-SteamPlugin/` — plugin sources (`StartupBootstrap.cs`,
  `WorkshopModLoader.cs`, `Configuration/`, `Diagnostics/`, `Discovery/`,
  `Integration/`, `Sources/`, `Staging/`).
- `scripts/`, `tests/`, `examples/`, `docs/` — tooling and docs.
