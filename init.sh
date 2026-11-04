#!/usr/bin/env bash
# init.sh -- Check that the repo is in a clean, working state before starting work.
# Run it from the repo root (Git Bash on Windows) after cloning or when resuming a session.
#   bash init.sh          tools, config, harness files, build, the tests that need no database, web lint/build/Vitest
#   bash init.sh --full   the same, then every .NET test against Postgres (integration + concurrency)
# E2E is not run here: see tests/e2e/README.md.
#
# Steps:
#   1. Tools: dotnet (SDK 10 or later), node, npm, docker and git are on PATH.
#   2. Local config: .env exists and ConnectionStrings:Pos is in the user-secrets of the API and of the integration
#      tests (or in ConnectionStrings__Pos). Values are never printed.
#   3. Harness files: CLAUDE.md, docs/*.md, the hooks, ci.yml and the E2E README exist.
#   4. Postgres: starts Docker Desktop when the daemon is down (Windows, macOS; waits up to 120 s), then
#      docker compose up -d --wait. Fatal only with --full.
#   5. .NET build: warnings are errors and analyzers run.
#   6. .NET tests: the three projects that need no database; with --full, dotnet test Pos.slnx.
#   7. Web: npm ci when web/node_modules is missing, then lint, build and Vitest.
# Ends with "All checks passed" or the number of warnings; exits 1 on any warning or failure.
set -euo pipefail

cd "$(dirname "$0")"

FULL=false
if [ "${1:-}" = "--full" ]; then FULL=true; fi

WARNINGS=0
warn() { echo "  WARN: $1"; WARNINGS=$((WARNINGS + 1)); }
fail() { echo "  FAIL: $1"; exit 1; }

echo "=== Mini POS init ==="
echo ""

echo "[1/7] Checking tools..."
for tool in dotnet node npm docker git; do
  command -v "$tool" > /dev/null || fail "$tool is not on PATH"
done
DOTNET_MAJOR="$(dotnet --version | cut -d. -f1)"
[ "$DOTNET_MAJOR" -ge 10 ] || fail ".NET SDK 10 or later is required (found $(dotnet --version))"
echo "  OK: dotnet $(dotnet --version), node $(node --version), $(docker --version)"
echo ""

echo "[2/7] Checking local config (values are never printed)..."
[ -f .env ] || warn ".env is missing: copy .env.example and set the Postgres values"
has_secret() {
  dotnet user-secrets list --project "$1" 2> /dev/null | grep -q '^ConnectionStrings:Pos = '
}
has_secret src/Pos.Api || warn "ConnectionStrings:Pos is not in the user-secrets of src/Pos.Api"
if ! has_secret tests/Pos.Api.IntegrationTests && [ -z "${ConnectionStrings__Pos:-}" ]; then
  warn "ConnectionStrings:Pos is not in the user-secrets of tests/Pos.Api.IntegrationTests nor in ConnectionStrings__Pos"
fi
echo ""

echo "[3/7] Verifying harness files..."
for file in CLAUDE.md docs/feature-list.json docs/business-rules.md docs/architecture.md docs/clean-state-checklist.md \
  scripts/verify-features.mjs .claude/hooks/protect-feature-state.mjs \
  .claude/settings.json .claude/hooks/verify-on-edit.mjs .claude/hooks/check-utf8.mjs \
  .claude/hooks/block-main-branch.mjs .claude/hooks/block-claude-coauthor.mjs \
  .claude/hooks/require-review-before-push.mjs \
  .github/workflows/ci.yml tests/e2e/README.md; do
  if [ -f "$file" ]; then echo "  OK: $file"; else warn "missing $file"; fi
done
echo ""

DOCKER_START_TIMEOUT=120
# Starts Docker Desktop when the daemon is down (Windows and macOS) and waits until it answers.
start_docker() {
  docker info > /dev/null 2>&1 && return 0
  case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*)
      # Per-user install (next to the docker CLI, under LOCALAPPDATA) or the machine-wide one under Program Files.
      local exe candidate
      for candidate in "$(dirname "$(command -v docker)")/../../Docker Desktop.exe" \
        "${LOCALAPPDATA:-}/Programs/DockerDesktop/Docker Desktop.exe" \
        "${ProgramW6432:-C:/Program Files}/Docker/Docker/Docker Desktop.exe"; do
        if [ -f "$candidate" ]; then exe="$candidate"; break; fi
      done
      [ -n "${exe:-}" ] || return 1
      "$exe" > /dev/null 2>&1 &
      ;;
    Darwin) open -a Docker || return 1 ;;
    *) return 1 ;;
  esac
  echo "  Docker daemon is not running: starting Docker Desktop (up to ${DOCKER_START_TIMEOUT}s)..."
  local waited=0
  until docker info > /dev/null 2>&1; do
    [ "$waited" -ge "$DOCKER_START_TIMEOUT" ] && return 1
    sleep 3
    waited=$((waited + 3))
  done
  echo "  OK: Docker is running"
}

echo "[4/7] Starting Postgres (localhost:5433)..."
if ! start_docker; then
  DB_PROBLEM="the Docker daemon is not running and Docker Desktop could not be started"
elif ! docker compose up -d --wait > /dev/null 2>&1; then
  DB_PROBLEM="the db container is not healthy: run 'docker compose up -d' and check its output"
else
  DB_PROBLEM=""
  echo "  OK: db is healthy"
fi
if [ -n "$DB_PROBLEM" ] && [ "$FULL" = true ]; then fail "Postgres did not start: $DB_PROBLEM"; fi
if [ -n "$DB_PROBLEM" ]; then warn "Postgres did not start: $DB_PROBLEM. Integration tests and the API will not run"; fi
echo ""

echo "[5/7] Building .NET (warnings are errors)..."
dotnet build Pos.slnx --nologo -v quiet
echo ""

echo "[6/7] Running .NET tests..."
if [ "$FULL" = true ]; then
  dotnet test Pos.slnx --no-build --nologo
else
  for project in tests/Pos.Domain.Tests tests/Pos.Application.Tests tests/Pos.ArchitectureTests; do
    dotnet test "$project" --no-build --nologo
  done
  echo "  Integration tests skipped: run 'bash init.sh --full' with the database up."
fi
echo ""

echo "[7/7] Checking web/ (lint, build, Vitest)..."
if [ -d web/node_modules ]; then
  echo "  web/node_modules exists: skipping npm ci"
else
  npm ci --prefix web
fi
(cd web && npm run lint && npm run build && npx ng test --watch=false)
echo ""

echo "Git: branch $(git branch --show-current), $(git status --porcelain | wc -l | tr -d ' ') uncommitted file(s)"
echo ""
if [ "$WARNINGS" -eq 0 ]; then
  echo "=== Init complete. All checks passed. ==="
else
  echo "=== Init complete with $WARNINGS warning(s). Fix them before you start. ==="
fi
echo "Run the API: dotnet run --project src/Pos.Api --launch-profile http"
echo "Run the web app: npm start --prefix web"
echo "Before a PR: go through docs/clean-state-checklist.md"
if [ "$WARNINGS" -ne 0 ]; then exit 1; fi
