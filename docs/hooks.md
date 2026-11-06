# Hooks

Scripts in `.claude/hooks/` that Claude Code runs on its own. The rules they enforce are in `CLAUDE.md`, and the rule of
`tests-only.mjs` is in `.claude/agents/tester.md`; this file says which script does what. When a hook reports errors,
fix them.

Hooks are wired in `.claude/settings.json`, except `tests-only.mjs`, which is in the frontmatter of
`.claude/agents/tester.md`.

| Script | Runs on | What it does |
| --- | --- | --- |
| `verify-on-edit.mjs` | `Stop`; `PreToolUse` (`Bash\|PowerShell`) before `git commit` | Checks the uncommitted changes. `.cs`, `.csproj`, `.props`: build + the tests that need no DB. `web/src/**/*.ts\|html`: eslint on those files + `ngc` (`tsc` for specs). Before a commit, it blocks the commit when they fail. |
| `check-utf8.mjs` | `Stop`; `PreToolUse` (`Bash\|PowerShell`) before `git commit` | Rejects changed `.cs`, `.csproj`, `.props`, `.ts`, `.html`, `.css`, `.json`, `.md` and `.mjs` files that are not UTF-8. Other file types are not checked. |
| `block-claude-coauthor.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks `git commit`, `git merge`, `gh api`, and `gh pr` or `gh issue` with `create`, `edit`, `comment`, `review`, `merge`, `close` or `reopen`, when the command text or message file has a `Co-Authored-By` trailer with an email in `<...>` that names Claude or `noreply@anthropic.com`. Other subcommands are not checked. Tests: `block-claude-coauthor.test.mjs`. |
| `block-main-branch.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks `git commit` on main and a `git push` that updates main. It reads the git commands with `gitCommands`, so its limits are the parser limits in `docs/DECISIONS.md`. Tests: `block-main-branch.test.mjs`. |
| `protect-feature-state.mjs` | `PreToolUse` (`Edit\|Write`) | Blocks hand edits of `state`, `testedAt` and `evidence` in `docs/feature-list.json`, and an edit that leaves that file as invalid JSON. |
| `tests-only.mjs` | `PreToolUse` (`Edit\|Write\|MultiEdit\|NotebookEdit`), `tester` agent only | Blocks edits outside `tests/`. |
| `git-changes.mjs` | Not a hook | Helpers for hook input, git commands in a shell command and uncommitted changes. `block-claude-coauthor.mjs` does not import it. |
| `*.test.mjs` | Not a hook | `node:test` tests that run a hook on sample commands and check its exit code. CI runs them. The command is in the Verification section of `CLAUDE.md`. Other hooks have no tests. |
