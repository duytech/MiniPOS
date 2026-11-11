# Feature verification

How `docs/feature-list.json` and `scripts/verify-features.mjs` are used. This text was in `CLAUDE.md` until it was
taken out of the default instructions.

Read `docs/feature-list.json` before adding a feature: existing features (API routes, web pages) with how each is
verified. Add or update the feature's entry when you add or change one, with `verification` steps that run its tests.
- `state`, `testedAt` and `evidence` are set only by `node scripts/verify-features.mjs [id ...]` (it runs the steps).
  `.claude/hooks/protect-feature-state.mjs` blocks hand edits when it is registered in `.claude/settings.json`; it is
  not registered now. A new entry starts `not_started`, with `testedAt` and `evidence` `null`. The verifier starts the
  services a step `requires` when they are down (Docker Desktop, Postgres, the API on `pos_e2e`, the web app) and stops
  at the end only what it started; `--no-start` skips those steps instead (`blocked`). An API already running on
  `E2E_API_URL` is refused, not reused (it cannot tell its database): those steps are `blocked` until you stop it.
- Work on one feature at a time: at most one feature is `active` (a failed verification) while you work.
- A feature is done when the verifier sets it to `passing`.

## Is it effective? (review, 2026-11-11)

Partly. It stops an agent from calling a feature done when nothing ran. It does not prove that each feature has its
own tests.

### Strengths
- The agent can't mark itself done: only the verifier sets `state`, `testedAt` and `evidence` (with the hook
  registered, hand edits are blocked).
- A filter that matches no test counts as a failure. `dotnet test` exits 0 when its filter matches nothing; the
  verifier fails the step anyway (`scripts/verify-features.mjs`, `NO_TESTS`).
- "Done" means a command ran: `evidence` records the commit and the commands that passed, not a sentence the agent
  wrote.

### Weaknesses
1. One check covers many features. Each step runs a whole test class, and several features share it:
   `ValidatorTests` is a step of 5 features (F01, F09, F11, F13, F16), `OrderPricingTests` of 4. Delete every test for
   "create a menu item" and F01 still passes, because the rest of those classes still pass. `passing` means "these
   classes are green", not "this feature has tests".
2. `passing` is a snapshot and goes stale. Nothing resets a feature when its code changes; only a manual verifier run
   does (the pre-push hook doesn't call it). After a breaking change, `dotnet test`, CI and the pre-push hook fail, but
   the JSON still says `passing` until someone runs the verifier. An agent that reads the file to see what is done is
   misled. CI already runs every test on each PR, so the state mostly repeats what CI says, just later. CI is a required check, so broken code still can't reach `main`; the stale state only misleads someone who
   trusts the JSON instead of CI.
3. It costs a lot to keep up: the file is about 36 KB (roughly 9k tokens each time it is read), and many commits exist
   only to record verifier results.
4. It no longer serves as a backlog. In the long-running-agent pattern the list matters while many features are
   `not_started` and each new session needs to see what is left. Here every feature is `passing`, and new work arrives
   as GitHub issues.

### How to make it prove something
- Tag each test with its feature (`[Trait("Feature", "F01")]`) and verify with `--filter "Feature=F01"`. Deleting a
  feature's tests then fails that feature, through the existing no-test rule.
- Mark a feature `stale` instead of `passing` when files changed since `evidence.commit`, or run the verifier in CI so
  CI writes the result.
- Or keep only the map from feature id to its tests, drop `state`, and let CI decide pass/fail.
