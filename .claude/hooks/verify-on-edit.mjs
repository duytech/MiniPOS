// Stop hook: when Claude is about to end its turn, run the checks for each side of the repo with uncommitted changes.
//   .cs / .csproj / .props          -> dotnet build Pos.slnx, then the tests that need no database
//   web/**/*.ts, web/**/*.html      -> eslint on the changed files, then the Angular compiler (types + templates)
// A failure exits with code 2 and prints the errors on stderr, which keeps Claude working to fix them.
// With --before-commit it is a PreToolUse hook (Bash, PowerShell) instead: it runs the same checks only when the
// command runs `git commit`, and exit 2 blocks the commit. Otherwise a commit made during the turn leaves the Stop
// hook nothing uncommitted to check.
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const MAX_ERROR_LINES = 40;
const FAST_TEST_PROJECTS = [
  'tests/Pos.Domain.Tests',
  'tests/Pos.Application.Tests',
  'tests/Pos.ArchitectureTests',
];

const input = JSON.parse(readFileSync(0, 'utf8').replace(/^﻿/, '') || '{}');
const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
const beforeCommit = process.argv.includes('--before-commit');

/**
 * Whether a shell command runs `git commit`. Heredoc and here-string bodies and quoted strings are dropped first, so a
 * commit message or an echo that mentions "git commit" does not count.
 */
function runsGitCommit(command) {
  const masked = command
    .replace(/<<-?\s*['"]?(\w+)['"]?[^\n]*\n[\s\S]*?\n\s*\1\b/g, '')
    .replace(/@(['"])\r?\n[\s\S]*?\r?\n\1@/g, '""')
    .replace(/"[^"]*"|'[^']*'/g, '""');
  return masked.split(/&&|\|\||[;|\n]/).some((segment) => {
    const words = segment.trim().split(/\s+/);
    const at = words.findIndex((w) => /^git(\.exe)?$/i.test(w));
    if (at < 0) return false;
    let i = at + 1;
    while (words[i]?.startsWith('-')) i += words[i] === '-C' || words[i] === '-c' ? 2 : 1;
    return words[i] === 'commit';
  });
}

if (beforeCommit && !runsGitCommit(input.tool_input?.command ?? '')) process.exit(0);

/** Modified, added and untracked files (deleted ones need no lint), relative to the repo root with forward slashes. */
const status = spawnSync('git status --porcelain --untracked-files=all', { cwd: root, shell: true, encoding: 'utf8' });
const changed = (status.stdout ?? '')
  .split(/\r?\n/)
  .filter((line) => line.length > 3 && !line.startsWith(' D') && !line.startsWith('D '))
  .map((line) => line.slice(3).replace(/^.* -> /, '').replace(/^"|"$/g, ''));
const ext = (file) => path.extname(file).toLowerCase();
const dotnetChanged = changed.some((file) => ['.cs', '.csproj', '.props'].includes(ext(file)));
const webFiles = changed.filter((file) => file.startsWith('web/src/') && ['.ts', '.html'].includes(ext(file)));
if (!dotnetChanged && webFiles.length === 0) process.exit(0);

const failures = [];

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

/** Runs a command; on a non-zero exit keeps the lines that `pick` selects from its output. */
function check(label, command, cwd, pick) {
  const started = Date.now();
  const result = spawnSync(command, { cwd, shell: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const seconds = ((Date.now() - started) / 1000).toFixed(1);
  if (result.status === 0) return true;

  const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}`
    .replace(/\x1b\[[0-9;]*m/g, '') // ngc colours its output even with --pretty false
    .split(/\r?\n/);
  const picked = pick(output);
  const shown = picked.length ? picked : output.filter((line) => line.trim()).slice(-MAX_ERROR_LINES);
  failures.push(
    `✗ ${label} failed (${seconds}s): ${command}`,
    ...shown.slice(0, MAX_ERROR_LINES),
    ...(shown.length > MAX_ERROR_LINES ? [`… ${shown.length - MAX_ERROR_LINES} more`] : []),
  );
  return false;
}

if (dotnetChanged) {
  const built = check('dotnet build', 'dotnet build Pos.slnx -nologo -v q', root, matching(/: (error|warning) [A-Z]+\d+/));
  if (built) {
    for (const project of FAST_TEST_PROJECTS) {
      check(
        `dotnet test ${project}`,
        `dotnet test ${project} --no-build -nologo -v q --logger "console;verbosity=normal"`,
        root,
        failedTests,
      );
    }
  }
}

if (webFiles.length) {
  const web = path.join(root, 'web');
  const filesInWeb = webFiles.map((file) => `"${file.slice('web/'.length)}"`).join(' ');
  check('eslint', `npx eslint ${filesInWeb}`, web, matching(/^\s*\d+:\d+\s+(error|warning)\s|^\S.*\.(ts|html)$/));
  if (webFiles.some((file) => !file.endsWith('.spec.ts'))) {
    check('ngc (types + templates)', 'npx ngc -p tsconfig.app.json --noEmit --pretty false', web, matching(/error (TS|NG)\d+/));
  }
  if (webFiles.some((file) => file.endsWith('.spec.ts'))) {
    check('tsc (spec)', 'npx tsc -p tsconfig.spec.json --noEmit --pretty false', web, matching(/error (TS|NG)\d+/));
  }
}

if (failures.length) {
  const heading = beforeCommit
    ? 'Commit blocked: checks failed on the uncommitted changes. Fix these, then commit again:'
    : 'Checks failed on the uncommitted changes. Fix these before finishing:';
  process.stderr.write([heading, ...failures].join('\n') + '\n');
  process.exit(2);
}
