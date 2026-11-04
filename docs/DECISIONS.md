# Decisions

Important design decisions: what, why, when. Newest first.

## 2026-11-04: Claude's pushes need a reviewer run first (#82)
- **What:** `.claude/hooks/require-review-before-push.mjs` blocks a `git push` when `HEAD` has no review stamp. The
  stamp is `reviewed-sha` under the git dir, so it is never committed and each worktree has its own. The main session
  writes it with `--stamp` after the `reviewer` returns and its findings are handled.
- **Why:** PR #81 showed the reviewer takes about 2 minutes and catches errors CodeRabbit cannot, and running it by
  hand is easy to forget.
- **Every push, docs and tests too:** PR #81 was docs-only and the reviewer still found errors.
- **Open blockers:** no stamp while a `blocker` is open, unless the user waives it. This is a rule for the main session
  in `CLAUDE.md`; the hook only compares SHAs.
- **`--stamp`, not a shell redirect:** `>` in Windows PowerShell 5.1 can write a BOM or UTF-16, and the SHA would not
  match.
- **Same-command moves are blocked:** the hook runs before the whole command, so in `git commit && git push` it would
  check the old SHA and let the new commit through. The push has to be its own command. A move in any folder counts,
  even in another repository: `cd web && git commit && cd .. && git push` names one repository with two paths, so
  comparing paths let it through. `git -C ../other commit && git push` is a false block, the cheaper mistake.
- **Limits:** a reminder, not a hard gate, because Claude writes the stamp. A push typed in a terminal is not checked;
  a git `pre-push` hook calling `claude -p --agent reviewer` was considered and not chosen. Only `HEAD` is compared, so
  `git push origin other-branch` is not looked at. Deletes and dry runs pass, because they send no new code. The hook
  reads the command text, so it can miss a push, e.g. `bash -c "git push"`, `/usr/bin/git push`, `git --git-dir x push`
  or a git alias, `git push origin :a (Get-Branch)`, and can read the wrong folder after `Push-Location` or `popd`. It
  can also block by mistake, e.g. `echo git push` or the combined flags in `git push -nu`. A full shell parser is not
  worth it for a reminder.
- **Brackets depend on the shell:** a `cd` inside `(x)` or `$(x)` ends with the bracket in a POSIX shell and stays in
  PowerShell, so the hook reads `tool_name`.
- **No test file:** the other hooks have none and CI runs none for hooks. It was checked with sample inputs and a real
  push, as the PR describes.

## 2026-11-03: README is in English; the journal and roadmap stay in Vietnamese (#80)
- **What:** `README.md` explains the harness in English and links to `docs/journal.md` and `roadmap.md` (Vietnamese)
  for the day-by-day story. Its results section quotes numbers from the journal, not new ones.
- **Why:** the README is what a CV reader and an English-speaking interviewer open first, and `CLAUDE.md`, the agents
  and `.coderabbit.yaml` are already in English. The journal is a personal learning log; translating it would double
  the upkeep.

## 2026-11-03: The app ships as two images on GHCR; Docker Desktop runs them with a compose profile (#77)
- **What:** `src/Pos.Api/Dockerfile` and `web/Dockerfile` (nginx serves the Angular build and proxies `/api` to the
  API). `api` and `web` are in the `app` profile of `docker-compose.yml`. `.github/workflows/cd.yml` builds both on
  every PR and, after CI passes on `main` (`workflow_run`), pushes them to GHCR as `latest` and `sha-<short>`.
- **Why:** roadmap Week 4, CD exercise. GHCR is free with `GITHUB_TOKEN` and needs no runner on this machine; only a
  commit whose quality gate passed gets an image.
- **Profile, not a second compose file:** plain `docker compose up -d` (CLAUDE.md, `init.sh`,
  `scripts/services.mjs`) still starts only `db`.
- **Migrations:** `Database:MigrateOnStartup` (default false) makes a non-Development host migrate on startup. The
  container sets it and runs as Production, so OpenAPI and developer error pages stay off.

## 2026-11-03: A spec, not a lint rule, catches money arithmetic in the frontend (#70)
- **What:** a page that shows money has a Vitest spec that collects every `$x.xx` on screen and checks each one is a
  value the API sent, with amounts picked so that no sum or difference of two is another. First one:
  `order-detail.spec.ts`. The `add-angular-feature` skill lists it as a required test.
- **Why:** bug PR #66 showed "before tip" as `total - tip` and Vitest, lint and E2E passed. A lint rule would have to
  guess money fields by name; the spec checks what the user actually sees.

## 2026-11-03: A 500 says nothing about the exception, stays logged, and both are tested (#70)
- **What:** `UnexpectedErrorTests` replaces a service with one that throws and checks that the 500 body is a plain
  ProblemDetails (no exception type, message, inner exception or stack frame) and that one `Error` log entry carries
  the exception. `ApiExceptionHandler` keeps returning `false` for exceptions it does not map, so
  `ExceptionHandlerMiddleware` writes the response and logs. New feature entry F25.
- **Why:** Week 4 Day 3 bug PR #64 put `exception.ToString()` in every 500 and CI stayed green. In .NET 10 an
  `IExceptionHandler` that returns `true` can suppress the middleware's error log, so a fix that handles 500s in our
  handler could silently lose the log; the log test catches that.

## 2026-11-03: Both PR reviewers check security and tests; CodeRabbit stays `chill` (#71)
- **What:** `.claude/agents/reviewer.md` got a Security check (nothing a client receives exposes exception details,
  stack traces, SQL or server paths; a 500 returns a generic message), a "test edited to match the code" check and a
  "tests for changed behavior" check. The agent rates security a request can reach and bent tests
  `blocker`. `.coderabbit.yaml` got the same points as path instructions, rated Major. Its `profile` stays `chill`.
  These are review severities only: the merge gate is still the required checks `dotnet`, `web`, `e2e`.
- **Why:** Week 4 Day 3 (#62): the reviewer agent rated a stack trace in every 500 only `should-fix`, and CodeRabbit
  rarely named the missing test that let a bug through CI. Keeping the profile unchanged means the Day 3 and Day 4
  runs differ only in the instructions, so a change in the findings can be traced to them.
- **Not decided:** today `ApiExceptionHandler` returns `false` for an unmapped exception, so the middleware writes the
  500. If a handler ever returns `true` for it (as bug PR #64 did), .NET 10 can skip the middleware's error log.
  Whether 500s must stay logged, and tested, is left to issue #70.

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
