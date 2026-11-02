# Decisions

Important design decisions: what, why, when. Newest first.

## 2026-11-02: Feature list is JSON with a verifier (#52)
- **What:** `docs/feature-list.md` became `docs/feature-list.json`. Each feature has a behavior, verification steps
  (commands, optional `cwd` and `requires`), `state` (`not_started` / `active` / `blocked` / `passing`), `testedAt`
  (`null` until verification first runs) and `evidence`. `scripts/verify-features.mjs` runs the steps and is the only
  thing that sets `state`, `testedAt` and `evidence`; `.claude/hooks/protect-feature-state.mjs` blocks hand edits.
  Business rules moved, unchanged, to `docs/business-rules.md`.
- **Why:** a prose list says what exists but nothing checks it. A list with a command and a state per feature lets a
  script prove each feature still passes, and an agent cannot mark a feature done without its tests passing
  (learn-harness-engineering, lecture 08).
- **Requires per step, not per feature:** a web feature runs its Vitest specs even when the E2E stack is down; it is
  `blocked`, not `passing`, until its E2E specs also ran.
