# GammaKey

project: GammaKey
Install nothing here; skills and plugins are user scope.

> Vault: `C:\Dev\Workspace\Vault` | Wiki: `C:\Dev\Workspace\Vault\wiki\projects\GammaKey.md`

## Commands
- Build: `[command]`
- Test: `[command]`
- Lint: `[command]`
- Dev: `[command]`

## Stack
TBD. Run /setupdotclaude from this repo to detect the framework and fill in the commands above.

## Architecture


## Key Decisions


## Conventions
- Check `../../vault/wiki/patterns/` before writing new solutions
- Commit messages must be descriptive â€” they auto-document into the vault
- Good: `fix LockService timeout on concurrent submissions`
- Poor: `fix`, `update`, `wip`

## Agents available globally
`@code-reviewer` Â· `@security-reviewer` Â· `@performance-reviewer`
`@frontend-designer` Â· `@doc-reviewer`

## Skills available globally
`/ship` Â· `/pr-review` Â· `/debug-fix` Â· `/refactor` Â· `/tdd`
`/test-writer` Â· `/explain` Â· `/context-budget` Â· `/setupdotclaude`

## BMAD
The bmad-* skills are shared at the workspace level, but this project has its own engine at
`Projects/GammaKey/_bmad/`. When running ANY bmad-* skill for this project, resolve `{project-root}`
to THIS folder (`Projects/GammaKey`), never the workspace root. All BMAD artifacts must land in
`Projects/GammaKey/_bmad-output/`. If a BMAD run ever writes to a workspace-root `_bmad-output/`,
project-root was resolved wrong: stop and re-point it here. Config and scripts resolve from
`Projects/GammaKey/_bmad/` (output_folder is pinned to `_bmad-output` in `_bmad/custom/config.toml`).

