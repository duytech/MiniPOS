# Hooks

Scripts in `.claude/hooks/` that Claude Code runs on its own. The rules they enforce are in `CLAUDE.md`; this file says
which script does what. When a hook reports errors, fix them.

Hooks are wired in `.claude/settings.json`, except `tests-only.mjs`, which is in the frontmatter of
`.claude/agents/tester.md`.

| Script | Runs on | What it does |
| --- | --- | --- |
| `verify-on-edit.mjs` | `Stop`; `PreToolUse` (`Bash\|PowerShell`) before `git commit` | Checks the uncommitted changes. `.cs`: build + the tests that need no DB. `web/src/**/*.ts\|html`: eslint on those files + `ngc` (`tsc` for specs). Before a commit, it blocks the commit when they fail. |
| `check-utf8.mjs` | `Stop`; `PreToolUse` (`Bash\|PowerShell`) before `git commit` | Rejects changed text files that are not UTF-8. |
| `block-claude-coauthor.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks commands with `Co-Authored-By: Claude`. |
| `block-main-branch.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks `git commit` on main and any `git push` that updates main. |
| `require-review-before-push.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks a push when `HEAD` has no review stamp. Also blocks a push that follows a command that can move `HEAD`, such as `git commit` or `git switch`, in the same command. Run by hand with `--stamp`, it writes the stamp. |
| `protect-feature-state.mjs` | `PreToolUse` (`Edit\|Write`) | Blocks hand edits of `state`, `testedAt` and `evidence` in `docs/feature-list.json`. |
| `tests-only.mjs` | `PreToolUse` (`Edit\|Write\|MultiEdit\|NotebookEdit`), `tester` agent only | Blocks edits outside `tests/`. |
| `git-changes.mjs` | Not a hook | Helpers the other scripts import: hook input, git commands in a shell command, uncommitted changes. |
