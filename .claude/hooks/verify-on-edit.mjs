// PostToolUse hook: after Claude edits a file, run the checks for that file's side of the repo.
//   .cs / .csproj / .props          -> dotnet build Pos.slnx, then the tests that need no database
//   web/**/*.ts, web/**/*.html      -> eslint on the file, then the Angular compiler (types + templates)
// A failure exits with code 2 and prints the errors on stderr, which Claude Code feeds back to Claude.
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
const filePath = input.tool_input?.file_path;
if (!filePath) process.exit(0);

const rel = path.relative(root, path.resolve(root, filePath)).split(path.sep).join('/');
if (rel.startsWith('..')) process.exit(0);

const ext = path.extname(rel).toLowerCase();
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

if (ext === '.cs' || ext === '.csproj' || ext === '.props') {
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
} else if (rel.startsWith('web/src/') && (ext === '.ts' || ext === '.html')) {
  const web = path.join(root, 'web');
  const fileInWeb = rel.slice('web/'.length);
  check('eslint', `npx eslint "${fileInWeb}"`, web, matching(/^\s*\d+:\d+\s+(error|warning)\s/));
  const isSpec = rel.endsWith('.spec.ts');
  check(
    isSpec ? 'tsc (spec)' : 'ngc (types + templates)',
    isSpec
      ? 'npx tsc -p tsconfig.spec.json --noEmit --pretty false'
      : 'npx ngc -p tsconfig.app.json --noEmit --pretty false',
    web,
    matching(/error (TS|NG)\d+/),
  );
}

if (failures.length) {
  process.stderr.write(
    [`Checks failed after editing ${rel}. Fix these before continuing:`, ...failures].join('\n') + '\n',
  );
  process.exit(2);
}
