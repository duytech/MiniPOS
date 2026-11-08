# Decisions

Important design decisions: what, why, when. Newest first.

## 2026-11-08: Validation failures go into the message; controllers return Result (#144)
- **What:** `ValidationError` and `error.fields` are gone. A validation failure is `Error.Validation(messages)`: code
  `ValidationFailed`, message every failure message joined with a space (`"'Quantity' must be greater than '0'."`).
  `Result<T>` now serializes as the envelope (`isSuccess`, `data`, `error { code, message }`), so controllers return it
  as it is and `ApiResponse` / `ToResponse` are deleted. Requests rejected before the handler build the same `Result`
  in `InvalidModelStateResponseFactory`.
- **Why:** the owner's decision. `ApiResponse` only existed to copy `Result` and to write `fields`, which the serializer
  cannot see on a subclass of `Error`; the web app never read `fields`.
- **Trade-off, accepted:** a client can no longer tell which field failed without parsing the message. The API JSON
  shape now follows `Result` and `Error`: renaming one of their properties changes the API.

## 2026-11-08: Result returns null for the unused side (#142)
- **What:** `Result<T>.Data` is null on a failure and `Result.Error` / `Result<T>.Error` is null on a success, instead of
  throwing `InvalidOperationException`. A success may carry null `Data` (`Result.Ok<string?>(null)` is allowed), so
  `IsSuccess` only promises a non-null `Error` on a failure (`[MemberNotNullWhen(false, nameof(Error))]`), not a non-null
  `Data` on a success.
- **Why:** the owner's decision: reading a property should not throw, and a success with no data is fine.
- **Trade-off, accepted:** reading the wrong side no longer fails at once; it gives null. Code that knows a success has
  data (the split handlers, tests) reads it with `!`. For a value type `T`, `Data` on a failure is `default` (a failed
  `Result<bool>` has `false`), so use `bool?` when null matters.

## 2026-11-08: Mapped Domain entities hold only properties (#140)
- **What:** `Order`, `OrderLine`, `MenuItem`, `SavedSplitPart` and `SavedSplitLine` are `{ get; set; }` properties only.
  Each use case's logic moved from the entity into its handler (e.g. `Order.Pay` → `PayOrderHandler`). Pricing and
  splitting stay in Domain as static `OrderPricing` and `OrderSplitting`, so totals are still computed in one place in
  Domain; the rules several handlers share are in static `OrderRules`. Value objects and records not mapped to tables
  (`Money`, `Percentage`, `OrderDiscount`, `OrderTotals`, `SplitPart`) keep their members. No API or schema change.
- **Why:** the owner's decision.
- **Trade-off, accepted:** the entities no longer protect themselves: any code can set `Status` or `Quantity` without the
  rules. The rules are kept by the handlers, checked by handler tests (`OrderActions` runs the real handlers on fakes),
  and `EntityShapeTests` fails when an entity gains a method, computed property, field or constructor with parameters.
- **Tests:** use-case and rule tests moved from `Pos.Domain.Tests` to `Pos.Application.Tests`; the two "line of another
  order" tests became "unknown line is not found", since a handler looks a line up by id on the loaded order.

## 2026-11-07: No OpenAPI endpoint (#119)
- **What:** removed `AddOpenApi`, `MapOpenApi` and the `Microsoft.AspNetCore.OpenApi` package.
- **Why:** no test, client or launch profile used the endpoint, and `src/Pos.Api/Pos.Api.http` covers calling the API by
  hand.

## 2026-11-07: Pos.slnx keeps its hand-kept file lists (#117)
- **What:** `Pos.slnx` keeps every `<File>` entry that #96 added, and gains the missing
  `.claude/hooks/verify-on-edit.test.mjs`.
- **The user's choice:** the files stay visible in Visual Studio. The issue's other options, keeping only the root files
  or removing all `<File>` entries, were not taken.
- **Limit:** `.slnx` has no wildcards and nothing checks for drift, so a new file needs its own entry by hand and the list
  can fall behind again. `dotnet build` does not read these entries, so it does not catch that.

## 2026-11-07: init.sh shares the Docker Desktop start, not the db check (#114)
- **What:** `init.sh` calls `ensureDockerDaemon` from `scripts/services.mjs` and deleted its own copy, so the wait is now
  300 s, not 120 s. It keeps `docker compose up -d --wait`.
- **The user's choice, not in the issue:** the issue named `ensure('db', true)`. That treats any open port 5433 as up, so
  `init.sh` would report OK for another process on the port, not wait for a container that is still starting, and not
  apply compose changes.

## 2026-11-07: One git status parser for the hooks (#113)
- **What:** `changedFiles` in `git-changes.mjs` reads `git status -z --no-renames`, and `verify-on-edit.mjs` uses it with
  `deleted: true`. `block-claude-coauthor.mjs` imports `readHookInput`. `check-utf8.test.mjs` runs the UTF-8 hook on a temp repo.
- **Why:** without `-z` git quotes a non-ASCII name, so `check-utf8.mjs` could not read the file and blocked the commit.
- **The user's choice, not in the issue:** `changedFiles` also drops `AD` and `MD` entries (staged, then deleted from the
  working tree), which were reported as not UTF-8. `UD` and `DU` stay, because a conflict file is still on disk.
- **The user's choice, from the CodeRabbit review:** `changedFiles` throws when `git status` fails, and reads up to 64 MiB
  (the default 1 MiB cut the list without an error). `check-utf8.mjs` then blocks, and `verify-on-edit.mjs` runs the checks.
  A broken `git` therefore also stops Claude from ending a turn, through the `Stop` hook, until `git` works again.

## 2026-11-06: verify-on-edit runs every check, only before git push (#103)
- **What:** `verify-on-edit.mjs` no longer runs on `Stop` or before `git commit`. Before `git push` it runs build, all
  .NET tests, verifier script tests, web lint, build and Vitest, then E2E types and Playwright, and starts the services
  they need. `check-utf8.mjs` stays on `Stop` and before `git commit`.
- **Why:** the old hook ran only the no-DB tests, so integration, concurrency, Vitest and E2E tests ran only when someone
  remembered them, or in CI after the push.
- **The user's decision, against the issue:** uncommitted files do not block the push. They count as changes when the hook
  decides whether code is pushed, and the checks run on them. The hook runs once, before the whole command, so in
  `git commit -a && git push` the code is not yet in a commit.
- **Choices not in the issue:**
  - The hook stops at the first failed check, because a later check says little after an earlier one fails.
  - The hook has its own limit of 30 minutes on the checks (`VERIFY_CHECKS_LIMIT_MS`), and blocks when it is reached.
- **Timeout, measured:** a `PreToolUse` hook that passes its timeout does not block: in a temp repo with `claude -p`
  (2.1.312), a hook that exits 2 after 30 s with `timeout: 3` let the push through, and the same hook exiting 2 before its
  timeout blocked it. So the timeout must never be what ends the hook. A full passing run took 222 s, with Docker Desktop
  and all services started cold. The timeout in `.claude/settings.json` is 2760 s: the 30-minute limit, up to 15 minutes
  of service starts (Docker, Postgres, API and web app at 5 minutes each) and 1 minute to stop them.
- **Limits:**
  - Build, analyzers and tests no longer run automatically during a turn or at commit, so an error can first show at push.
  - The checks read the working tree, not what is pushed. An uncommitted fix can make them pass while the pushed commits
    still fail, and an uncommitted bug can block a push whose commits are correct. CI checks the pushed commits.
  - Only pushes made through Claude Code are checked. CI stays the final gate.
  - The hook checks the current `HEAD`, even when the command pushes another branch. `git push --delete` also runs the
    checks when the current branch has unpushed code.
  - Commands that `gitCommands` misses (`bash -c "git push"`, see #100) are not checked.
  - A command that brings in commits and pushes in one call passes unchecked when the tree is clean and the commits so
    far change only docs: `git merge other && git push`, `git pull && git push`, `git cherry-pick x && git push`.
  - If the hook is killed from outside (the user interrupts it), services it started keep running. At its own limit,
    the child processes of the running check finish on their own.

## 2026-11-06: block-main-branch uses gitCommands; two hooks have tests (#100)
- **What:** `block-main-branch.mjs` drops its own parser and reads `gitCommands` from `git-changes.mjs`. `gitCommands` now
  joins a line continuation (`\` in a POSIX shell, a backtick in PowerShell), skips the value of `--git-dir`,
  `--work-tree`, `--namespace`, `--config-env` and `--attr-source`, and returns `gitDir`. The hook reads the branch of the
  repository `--git-dir` names. `block-claude-coauthor.mjs` also checks `git merge`, `gh pr|issue merge|close|reopen` and a
  line continuation. `block-main-branch.test.mjs` and `block-claude-coauthor.test.mjs` run each hook on sample commands,
  and the `e2e` job runs them next to the verifier script tests.
- **Why:** the forms in #98 exited 0, and nothing tested a hook. Two parsers meant a fix reached only one of them. On 44
  sample commands, `gitCommands` found every git command the old hook blocked. Commit detection in `verify-on-edit.mjs`
  and `check-utf8.mjs` gets the two parser fixes too.
- **The user's choice, not in the issue:** `git \<newline> commit`, `gh pr|issue reopen --comment` and the branch of the
  repository `--git-dir` names.
- **Limits, accepted by the user on this task:** `block-main-branch.mjs` now has the parser limits of #82 and #91 below.
  It missed these forms before this change too: `bash -c "git push origin main"`, `/usr/bin/git push`, `$GIT push`, nested
  `$(...)` in double quotes. `cd other & git commit` reads the wrong folder in a POSIX shell, because a backgrounded `cd`
  does not stay. `GIT_DIR=x git push` still reads the current folder. `block-claude-coauthor.mjs` misses a flag before the
  verb (`gh pr --repo x merge`). False blocks: `echo git push origin main` on the main-branch hook, and an `echo` that
  has a publishing command name and a trailer on the co-author hook.

## 2026-11-05: The reviewer runs only when asked (#98)
- **What:** the rule to run the `reviewer` agent before every push and `.claude/hooks/require-review-before-push.mjs`
  are removed. `.claude/agents/reviewer.md` stays, and the agent runs when the user asks. This replaces the push gate of
  #82 and #85 and the stamp rule of #88 below. The severity rules of #88 for the reviewer still apply when it runs.
- **Why:** the user's decision.

## 2026-11-05: Hook details live in docs/hooks.md (#98)
- **What:** script paths, events and what each hook checks moved from `CLAUDE.md` to `docs/hooks.md`, which also covers
  the scripts `CLAUDE.md` did not name. The rules and steps stayed in `CLAUDE.md`: no Claude co-author, no commit or
  push to `main`, UTF-8 text files, what the hooks do not run.
- **Why:** the details were spread over three sections of `CLAUDE.md`, and the agent needs them only when a hook blocks
  it or changes. The rules stayed because `CLAUDE.md` loads every session and other docs load only when read, so a rule
  moved out could be missed. The reviewer also takes its rules from `CLAUDE.md`.
- **Limits, accepted by the user on this PR until a separate task fixes them:** both exit 0 instead of 2, and both were
  there before this PR. `block-main-branch.mjs` does not see `(git push origin main)`, because `git` must be a separate
  word. `block-claude-coauthor.mjs` does not check `gh pr merge --body`, and that body becomes a commit on `main`.
  Closed by #100.

## 2026-11-04: The verifier refuses a running API (#84)
- **What:** when something already answers on `E2E_API_URL`, `scripts/services.mjs` refuses it and starts nothing;
  steps that require `e2e` are skipped, the feature is `blocked`, and the reason is printed. A running web app is still
  reused: it holds no data. With `--no-start`, `e2e` steps are now always `blocked`. The API it starts counts as up only
  when its own log says it bound the port, so a process that takes the port during startup is not used either.
  `tests/e2e/start-api.ps1` now adds `Database=pos_e2e` as the last key instead of replacing text: the last key wins in
  Npgsql, so a secret with `DB=`, `Database =` or no key cannot leave the API on another database.
- **Why:** a reused API on the dev database made E2E steps write to dev data. The API does not expose its database, and a
  route for it was declined (the user's choice): no API change. So only an API the verifier starts itself is known to be
  on `pos_e2e`.
- **Limit:** the web proxy always targets 5191. With `E2E_API_URL` set to another port, the check looks at that port and
  not at the API the tests reach. With `E2E_BASE_URL` on the compose `app` web (8080), that reused web app reaches the
  compose API on `POSTGRES_DB`.

## 2026-11-04: CI checks the Postgres guards in docker-compose.yml (#93)
- **What:** the `dotnet` job starts by parsing `docker compose config` and fails unless `db` publishes a port and each has
  `host_ip: 127.0.0.1`. It also fails unless `docker compose --env-file .env.example config` stops on the blank
  `POSTGRES_PASSWORD`.
- **Why:** CI starts Postgres with `docker run`, so removing a guard from #83 left every check green.
- **In `dotnet`, not a new job:** a new job counts only after someone adds it to the required checks of `main`. `dotnet`
  is required already and has `docker` and no default working directory. `web` runs in `web/`, and `e2e` is the slowest.
- **Password only in one command:** a variable set in the shell wins over `--env-file`, so setting it for the whole step
  would make compose accept the blank `.env.example`. `docker compose config` prints the password, so its output goes only
  to `jq` or `/dev/null`.
- **Limit:** the step checks the two guards as written. `network_mode: host` on `db` keeps the ports in the config while
  Docker ignores them, so it stays green.

## 2026-11-04: Postgres listens on 127.0.0.1 and has no default password (#83)
- **What:** `docker-compose.yml` publishes Postgres as `127.0.0.1:5433:5432`. `POSTGRES_PASSWORD` is blank in
  `.env.example`, and `${POSTGRES_PASSWORD:?...}` stops compose while it is unset or blank. The README connection strings
  use `Host=127.0.0.1`.
- **Why:** the port was open on every host interface, and the password that the README copied to `.env` is public in
  the repo.
- **Blank, not `change-me`:** any text placeholder is a known password that works if the reader skips the step.
- **`127.0.0.1`, not `localhost`:** with no listener on `::1`, Windows takes about 2 s to refuse each new connection
  before Npgsql tries IPv4. The integration tests took 41-45 s with `localhost` and 8 s with `127.0.0.1`.
- **Limit:** an existing `.env`, volume or user secret is not changed. Each machine that set up before this change keeps
  the old password until it is changed there, and a `Host=localhost` secret keeps working but stays slow on Windows.
- **Limit, password alphabet:** the password goes unescaped into `ConnectionStrings__Pos` in `docker-compose.yml` and
  into the README user-secrets commands, so a `;` ends its value and the API cannot connect. Compose cannot check the
  alphabet or escape the value, and nothing validates it, so the rule is only written down: letters and digits, in the
  README and `.env.example`. Accepted by the user on PR #92, after CodeRabbit reported it.

## 2026-11-04: Reviewer rates wrong output as should-fix; only a full-range approve allows the stamp (#88)
- **What:** in `.claude/agents/reviewer.md`, a finding with a concrete wrong output is at least `should-fix`, however
  rare the input, and `note` is only for something that is not wrong. Findings are grouped by root cause and each gives
  likelihood, fix size, origin (commit, base branch, and for a wrong output regression or existing gap), evidence (traced or run) and the forms
  a suggested fix would change. A design finding names a simpler design. A verdict on less than the full range is
  labelled `partial`. A new Harness check covers hooks and scripts. In `CLAUDE.md`, only a full-range `approve` allows
  the stamp, a wrong output is fixed or moved to limits only with the user's OK, and a traced finding is reproduced with a
  sample input before the fix.
- **Why:** on PR #85 the reviewer saw `(cd other); git push` checking the wrong repo and rated it a `note`. The main
  session moved it to limits without checking the fix size, and the last two approvals covered one commit each.
  CodeRabbit then reported it, and the fix took about 5 lines. Later rounds found bugs caused by the previous fix
  (`9d4cad3`, `5a00359`), and the main session, not the reviewer, raised the text parser as the problem.
- **Replaces the `blocker` waiver in #82:** a waived finding no longer skips the verdict. One with a concrete wrong
  output is recorded in the limits here, so it leaves a trace and later runs read it; any other is named in the reviewer's
  prompt. Either way it gets one line and does not count. So a stamp always follows a full-range approve, and a waiver
  costs one more run.
- **The hook's block message changed, nothing else in it:** it stated the #82 rule at the moment the main session
  decides whether to stamp. No push moves between allowed and blocked.
- **Design finding is a `note`:** it has no wrong output of its own, and the findings it groups still count.
- **An unlisted form in an accepted group counts as accepted:** the #82 groups say they list examples, not every form, so
  rating each unlisted form again would reopen what the user accepted.
- **The full range is the committed range:** a push does not send uncommitted changes, so a review of that range is not
  partial on a dirty tree, and they cannot clear a finding in `HEAD`. When the reviewer does read them, as in a run before
  a commit, they count against the verdict.
- **Not done:** the reviewer stays read-only, so its cases stay traced. The hook still compares only SHAs, so the
  full-range rule is one the main session follows. The 96 sample hook inputs from PR #85 are in a scratch script and not
  committed, so the next change starts without them. Committing them as tests that CI runs needs its own decision.
  #100 added tests for two other hooks; these inputs stay uncommitted.

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
  `git push origin other-branch` is not looked at. Deletes and dry runs pass, because they send no new code. The hook is
  called from the main checkout: on a branch without the script, it fails to start and the push goes ahead. The stamp
  holds one SHA, so reviewing a second branch and returning to the first blocks that push until it is stamped again.
- **Parser limits, accepted by the user on PR #85:** the hook reads the command text, and a full shell parser is not
  worth it for a reminder. Commit detection for `verify-on-edit.mjs` and `check-utf8.mjs` shares the same parser and the
  same misses. Examples per group, not a full list:
  - Command text the parser does not open: `bash -c "git push"`, `eval "git push"`, `Start-Process git "push origin x"`,
    `${x:-$(git push)}`, `x="$(git commit -m "m")"`, and apostrophes that pair across commands (`# don't`).
  - Subcommand lost: `git -C (Split-Path (Get-Location)) push`, `git -C {a} commit`. `git --git-dir x push`,
    `git --work-tree x push` and a line continuation between `git` and `push` are closed by #100.
  - Other command names: `/usr/bin/git push`, `git.cmd push`, `$GIT push`, an alias such as `git p`,
    `git subtree push`, `hub push`.
  - `HEAD` moved by something not on the list: `git ci`, `gh pr checkout 5`, `npm version patch`,
    `git update-ref HEAD <sha>`, `git bisect good`.
  - A push read as a delete or a dry run: `git push origin :a (Get-Branch)`, `git push origin :a {b,c}`,
    `git push -o -n origin x`.
  - Wrong folder: `if cd other; then git push; fi`, `then cd x`, `command cd`, `Push-Location`, `popd`, `cd -`, `chdir`,
    `cd (Join-Path .. other)`, `cd other & git push`, `cd other | cat`, `GIT_DIR=x git push`.
  - False blocks: values the hook cannot expand (`cd "$CLAUDE_PROJECT_DIR" && git push`, `git -C $(pwd) push`,
    `cd ~/x && git push`), `git checkout -- f && git push`, `git reset HEAD f; git push`,
    `git push origin :foo > out.txt`, `git push origin +:foo`, `git push -nu`, `echo git push`, a `# git push` line.
- **More parser limits, accepted by the user on PR #91:** each one runs the push but the parser misses it, and a fix
  needs a nesting-aware or quote-aware parser:
  - PowerShell: ``echo "$(echo `$(git push))"``. Inside `$(x)`, a backtick does not stop the inner bracket from running.
    The `git commit` spelling is new in PR #91: `main` read it as a commit only because commit detection used POSIX
    rules. PowerShell's rules also fix the simpler ``echo "\$(git commit -m x)"``, so the user accepted this trade.
  - An escaped quote or backtick that pairs across commands: `echo "a\"" && git push && echo "b"`,
    ``echo \` && echo `git push` ``.
  - A bracket or backtick inside a substitution: `echo "$(git push origin $(git branch --show-current))"` reads only
    `branch`, ``echo "$(echo `git push`)"`` reads nothing.
- **Brackets depend on the shell:** a `cd` inside `(x)` or `$(x)` ends with the bracket in a POSIX shell and stays in
  PowerShell, so the hook reads `tool_name`. The docs show `tool_name` only for `Bash`; `PowerShell` follows from the
  `Bash|PowerShell` matcher and was not seen in a real input.
- **No test file:** the other hooks had none and CI ran none for hooks. It was checked with sample inputs and a real
  push, as the PR describes. Since #100, `block-main-branch.mjs` and `block-claude-coauthor.mjs` have tests in CI.

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
  container sets it and runs as Production, so developer error pages stay off.

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
  Closed by #84.

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

## 2026-11-07: Result pattern and HTTP 200 for every expected answer (#138)
- **What:** Domain, handlers and `IUnitOfWork` return `Result` / `Result<T>` (`IsSuccess`, `Data`, `Error` with `Code` and
  `Message`) instead of throwing `DomainException`, `NotFoundException`, `ConflictException` or `ValidationException`.
  The API answers HTTP 200 with `{ isSuccess, data, error }` for all of them, including requests rejected before the
  handler. `ErrorCode` is the project's own enum. Unexpected errors stay a 500 ProblemDetails with no details (#70).
  `[ProducesResponseType]` is gone: every action now answers one shape, and the OpenAPI endpoint is gone since #119.
- **Why:** the owner's decision: expected errors show in the signature, and clients branch on one code instead of an
  HTTP status.
- **Trade-off, accepted:** monitoring, proxies and clients that count 4xx no longer see expected failures; a test that
  only checks the status passes on a failure, so tests check `isSuccess` or the code. POST no longer answers 201 with
  `Location`.
- **Guard:** with exceptions a broken rule could not be missed. Now a handler that ignores `order.Pay(...)` would save and
  answer success, and the compiler says nothing, so `ResultUsageTests` fails when a `Result` is thrown away. Its limit: a
  `Result` stored in a variable that is never read is not found.
- **Limit:** a missing route, a wrong method or a wrong Content-Type (404, 405, 415) stays the framework's answer: it
  never reaches a controller.

## 2026-11-07: An empty GUID on a route is 404 (#109)
- **What:** route ids are not validated. The all-zero GUID on a route is 404, like any other unknown id. The 7 validators
  that only checked a route id are deleted, and a use case has a validator only when its request has shape to check.
  Empty ids in the request body stay 400.
- **Why:** the same id got 400 on routes with a validator and 404 on `GET /api/orders/{id}`. No order or line has that
  id, so 404 is correct and the rule added nothing. A missing body field arrives as an empty GUID, so that is a bad
  request.
