// PreToolUse hook (Bash, PowerShell): before a `git push`, runs every check of the repo on the working tree, in order, and
// stops at the first failure. Exit 2 blocks the push and prints the errors on stderr, which keeps Claude working to fix them.
// Commits and uncommitted files with only *.md or docs/ changes run no checks. Otherwise uncommitted files block the push:
// the checks read the working tree, not what is pushed. Services a check needs are started through scripts/services.mjs and stopped at the end.
// The UTF-8 check is a separate hook (check-utf8.mjs).
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { ensure, stopStarted } from '../../scripts/services.mjs';
import { readHookInput, runsGit } from './git-changes.mjs';

const MAX_ERROR_LINES = 40;
// Claude Code runs the command when a hook passes its timeout (tested on #103), so the hook blocks by itself first. The
// timeout in .claude/settings.json is this limit plus the service starts, which can still be waiting when it is reached.
const CHECKS_LIMIT_MS = Number(process.env.VERIFY_CHECKS_LIMIT_MS ?? 30 * 60_000);
const NOT_CODE = /\.md$|^docs\//;

const input = readHookInput();
const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();

if (!runsGit(input, 'push')) process.exit(0);

const started = Date.now();
const git = (...args) => spawnSync('git', args, { cwd: root, encoding: 'utf8' });

function block(heading, lines) {
  process.stderr.write([`Push blocked: ${heading}`, ...lines].join('\n') + '\n');
  process.exit(2);
}

const isCode = (file) => !NOT_CODE.test(file);
const uncommitted = (git('status', '--porcelain', '-z', '--no-renames', '--untracked-files=all').stdout ?? '')
  .split('\0')
  .filter(Boolean)
  .map((entry) => entry.slice(3));

/** Whether the commits to push change a file other than *.md and docs/. A base that cannot be read counts as a change. */
function commitsChangeCode() {
  const base = ['@{u}', 'origin/main'].find((ref) => git('rev-parse', '--verify', '--quiet', ref).status === 0);
  if (!base) return true;
  const diff = git('diff', '--name-only', '--no-renames', '-z', `${base}...HEAD`);
  return diff.status !== 0 || diff.stdout.split('\0').some((file) => file && isCode(file));
}

// Uncommitted files count too: in `git commit -a && git push` the commit has not run yet when this hook reads the repo.
if (!uncommitted.some(isCode) && !commitsChangeCode()) process.exit(0);

if (uncommitted.length) {
  block('the checks run on the working tree, and it has uncommitted files. Commit or stash them, then push again:', [
    ...uncommitted.map((file) => `  ${file}`),
  ]);
}

/** Keeps the distinct lines that match (compilers repeat the same error per target). */
const matching = (pattern) => (lines) => [...new Set(lines.filter((line) => pattern.test(line)).map((l) => l.trim()))];

/**
 * Keeps each failed test with its message (Expected/Actual, or the offender list of an architecture test) and the
 * first stack line that points at a .cs file. Output of the VSTest console logger at normal verbosity.
 */
function failedTests(lines) {
  const kept = [];
  let inFailure = false;
  let inStack = false;
  let stackLineKept = false;
  for (const line of lines) {
    const text = line.trim();
    if (/^Failed (?!!)/.test(text)) {
      [inFailure, inStack, stackLineKept] = [true, false, false];
      kept.push(text);
    } else if (/^(Passed |Skipped |Standard Output|Failed!|\[xUnit\.net)/.test(text)) {
      inFailure = false;
      if (text.startsWith('Failed!')) kept.push(text);
    } else if (inFailure && text === 'Stack Trace:') {
      inStack = true;
    } else if (inFailure && inStack) {
      if (!stackLineKept && /\.cs:line \d+/.test(text)) {
        kept.push(`  ${text}`);
        stackLineKept = true;
      }
    } else if (inFailure && text && text !== 'Error Message:') {
      kept.push(`  ${text}`);
    }
  }
  return kept;
}

const CHECKS = [
  { label: 'dotnet build', command: 'dotnet build Pos.slnx -nologo -v q', pick: matching(/: (error|warning) [A-Z]+\d+/) },
  {
    label: 'dotnet test',
    command: 'dotnet test Pos.slnx --no-build -nologo -v q --logger "console;verbosity=normal"',
    requires: 'db',
    pick: failedTests,
  },
  { label: 'verifier script tests', command: 'node --test scripts/services.test.mjs' },
  {
    label: 'eslint',
    command: 'npm run lint',
    cwd: 'web',
    pick: matching(/^\s*\d+:\d+\s+(error|warning)\s|^\S.*\.(ts|html)$/),
  },
  { label: 'web build', command: 'npm run build', cwd: 'web' },
  { label: 'Vitest', command: 'npx ng test --watch=false', cwd: 'web' },
  { label: 'e2e types', command: 'npx tsc -p .', cwd: 'tests/e2e' },
  { label: 'Playwright', command: 'npx playwright test', cwd: 'tests/e2e', requires: 'e2e' },
];

/** Runs a check; undefined when it passes, else the lines to report (those `pick` selects, or the end of the output). */
function run({ label, command, cwd = '.', pick = () => [] }) {
  const checkStarted = Date.now();
  const left = started + CHECKS_LIMIT_MS - checkStarted;
  if (left <= 0) return [`✗ ${label} did not run: the checks reached their time limit`];
  // shortcut: at the limit only the shell is killed and its child processes finish alone, upgrade if the limit is hit in practice.
  const result = spawnSync(command, {
    cwd: path.join(root, cwd),
    shell: true,
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
    timeout: left,
  });
  if (result.status === 0) return undefined;

  const seconds = ((Date.now() - checkStarted) / 1000).toFixed(1);
  const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}`
    .replace(/\x1b\[[0-9;]*m/g, '') // ngc colours its output even with --pretty false
    .split(/\r?\n/);
  const picked = pick(output);
  const shown = picked.length ? picked : output.filter((line) => line.trim()).slice(-MAX_ERROR_LINES);
  return [
    `✗ ${label} ${result.error?.code === 'ETIMEDOUT' ? 'reached the time limit' : 'failed'} (${seconds}s): ${command}`,
    ...shown.slice(0, MAX_ERROR_LINES),
    ...(shown.length > MAX_ERROR_LINES ? [`… ${shown.length - MAX_ERROR_LINES} more`] : []),
  ];
}

async function firstFailure() {
  for (const check of CHECKS) {
    const down = check.requires && (await ensure(check.requires, true));
    if (down) return [`✗ ${check.label} did not run: ${down}`];
    const failure = run(check);
    if (failure) return failure;
  }
  return undefined;
}

let failure;
try {
  failure = await firstFailure();
} finally {
  stopStarted();
}
if (failure) block('a check failed. Fix it, commit, then push again:', failure);
