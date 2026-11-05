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
| `block-claude-coauthor.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks `git commit`, `gh api`, and `gh pr` or `gh issue` with `create`, `edit`, `comment` or `review`, when the command text or message file has a `Co-Authored-By` trailer with an email in `<...>` that names Claude or `noreply@anthropic.com`. Other subcommands, such as `gh pr merge`, are not checked. |
| `block-main-branch.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks `git commit` on main and a `git push` that updates main. It reads the command text and needs `git` as a separate word, so `(git push origin main)` is not blocked. |
| `require-review-before-push.mjs` | `PreToolUse` (`Bash\|PowerShell`) | Blocks a push when `HEAD` has no review stamp. Also blocks a push that follows a command that can move `HEAD`, such as `git commit` or `git switch`, in the same command. Run by hand with `--stamp`, it writes the stamp. |
| `protect-feature-state.mjs` | `PreToolUse` (`Edit\|Write`) | Blocks hand edits of `state`, `testedAt` and `evidence` in `docs/feature-list.json`, and an edit that leaves that file as invalid JSON. |
| `tests-only.mjs` | `PreToolUse` (`Edit\|Write\|MultiEdit\|NotebookEdit`), `tester` agent only | Blocks edits outside `tests/`. |
| `git-changes.mjs` | Not a hook | Helpers for hook input, git commands in a shell command and uncommitted changes. `block-claude-coauthor.mjs` and `block-main-branch.mjs` do not import it. |
