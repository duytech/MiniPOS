# Decisions

Important design decisions: what, why, when. Newest first.

## 2026-11-03: Both PR reviewers check security and tests; CodeRabbit stays `chill` (#71)
- **What:** `.claude/agents/reviewer.md` got a Security check (nothing a client receives exposes exception details,
  stack traces, SQL or server paths; a 500 returns a generic message), a "test edited to match the code" check and a
  "tests for changed behavior" check. Security a request can reach and bent tests are `blocker`. `.coderabbit.yaml`
  got the same points as path instructions. Its `profile` stays `chill`.
- **Why:** Week 4 Day 3 (#62): the reviewer agent rated a stack trace in every 500 only `should-fix`, and CodeRabbit
  rarely named the missing test that let a bug through CI. Keeping the profile unchanged means the Day 3 and Day 4
  runs differ only in the instructions, so a change in the findings can be traced to them.
- **Not decided:** whether 500s must stay logged when `ApiExceptionHandler` handles them (.NET 10 can suppress the
  middleware's log); left to issue #70.

## 2026-11-02: Race tests hold a row lock until every request is waiting (#67)
- **What:** the concurrency tests that must prove a 409 (parallel pays, parallel or mixed split PUT/DELETE) send
  their requests through `OrderRace` (`tests/Pos.Api.IntegrationTests/OrderRace.cs`). The test locks the order row
  `FOR NO KEY UPDATE`, starts the requests, waits until all of them are blocked on that lock (`pg_blocking_pids`,
  followed through waiters), then rolls back. Every request has loaded the same order version, so exactly one write
  wins and the others fail the `xmin` check: the tests now assert one accepted request and only 409 for the rest.
- **Why:** releasing the requests together was not enough. When the first pay committed before the others loaded the
  order, they got 422 (order not open), no 409 appeared, and the "requests really raced" check failed CI on an
  unrelated PR. Retrying the race would only make the flake rarer; accepting 422 as proof would not show the token is
  checked.
- **Trade-off:** in these tests no request loads the order after another committed, so the stale-load answers (422
  for a split that leaves out a new line, 404 for a split already deleted) come from the non-concurrent contract tests.
  The other race tests keep the plain barrier and still cover those mixed interleavings.

## 2026-11-02: CodeRabbit reviews every PR (#58)
- **What:** the CodeRabbit GitHub App is installed on this repo only, and `.coderabbit.yaml` points it at
  `CLAUDE.md` (read by default), `docs/business-rules.md` and `docs/architecture.md`, with per-path instructions for
  money, concurrency, API errors and tests. Migrations, the lock file and the journal are skipped.
- **Why:** roadmap Week 4 Day 2. CodeRabbit is free for public repos and its prompt lives in the repo, so Day 4 can
  tune it and compare runs. Claude Code Action and PR-Agent need a paid API key.
- **Not a gate:** its review is advice; the required checks stay `dotnet`, `web`, `e2e`.

## 2026-11-02: The verifier starts the services a step needs (#54)
- **What:** when a step `requires` db or e2e and it is down, `scripts/verify-features.mjs` starts it
  (`scripts/services.mjs`): Docker Desktop if the daemon is down, `docker compose up -d db`, the `pos_e2e` database if
  missing, the API with `tests/e2e/start-api.ps1`, the web app with `npm start --prefix web`. At the end, or on Ctrl+C,
  it stops only what it started; services already running and Docker Desktop are left as they are. `--no-start` keeps
  the old behavior (skip, `blocked`).
- **Why:** checking only meant three terminals of setup before an e2e feature could pass, so it stayed `blocked`.
- **Limit:** an API already running on 5191 is used as is, even when it is on the dev database and not `pos_e2e`.

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
