// Tests of check-utf8.mjs: each case runs the hook on a temp repo and checks which changed files it rejects (#113).
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { afterEach, beforeEach, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const hook = fileURLToPath(new URL('./check-utf8.mjs', import.meta.url));
const BLOCKED = 2;
const ALLOWED = 0;
const NOT_UTF8 = Buffer.from([0xff]);
const NON_ASCII_NAME = 'ghi-chú.md';

let repo;

function runGit(...args) {
  return spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@example.com', ...args], {
    cwd: repo,
    encoding: 'utf8',
  });
}

function git(...args) {
  const result = runGit(...args);
  assert.equal(result.status, 0, result.stderr);
}

function write(file, content = 'x') {
  writeFileSync(path.join(repo, file), content);
}

function remove(file) {
  rmSync(path.join(repo, file));
}

function commit(file, content = 'x') {
  write(file, content);
  git('add', '-A');
  git('commit', '-q', '-m', 'x');
}

function runHook(...args) {
  return spawnSync(process.execPath, [hook, ...args], {
    input: JSON.stringify({ tool_name: 'Bash', cwd: repo, tool_input: { command: 'git commit -m x' } }),
    encoding: 'utf8',
    env: { ...process.env, CLAUDE_PROJECT_DIR: repo },
  });
}

const beforeCommit = () => runHook('--before-commit');

beforeEach(() => {
  repo = mkdtempSync(path.join(os.tmpdir(), 'pos-hook-'));
  spawnSync('git', ['init', '-q', '-b', 'main', repo]);
});

afterEach(() => {
  rmSync(repo, { recursive: true, force: true });
});

test('a UTF-8 file with a non-ASCII name passes', () => {
  write(NON_ASCII_NAME);
  const result = beforeCommit();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('a UTF-8 file with a non-ASCII name passes in the Stop hook', () => {
  write(NON_ASCII_NAME);
  const result = runHook();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('a file with a non-ASCII name that is not UTF-8 is blocked under its own name', () => {
  write(NON_ASCII_NAME, NOT_UTF8);
  const result = beforeCommit();
  assert.equal(result.status, BLOCKED);
  assert.ok(result.stderr.includes(NON_ASCII_NAME), result.stderr);
});

test('a file with an ASCII name that is not UTF-8 is blocked', () => {
  write('a.md', NOT_UTF8);
  const result = beforeCommit();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, /a\.md/);
});

test('a staged file then deleted from the working tree (AD) passes', () => {
  write('a.md');
  git('add', '-A');
  remove('a.md');
  const result = beforeCommit();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('a staged change then deleted from the working tree (MD) passes', () => {
  commit('a.md');
  write('a.md', 'y');
  git('add', '-A');
  remove('a.md');
  const result = beforeCommit();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('a file deleted from the working tree ( D) passes', () => {
  commit('a.md');
  remove('a.md');
  const result = beforeCommit();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('a staged delete (D ) passes', () => {
  commit('a.md');
  git('rm', '-q', 'a.md');
  const result = beforeCommit();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('a conflict file still on disk (UD) that is not UTF-8 is blocked', () => {
  commit('a.md');
  git('switch', '-q', '-c', 'other');
  git('rm', '-q', 'a.md');
  git('commit', '-q', '-m', 'x');
  git('switch', '-q', 'main');
  commit('a.md', NOT_UTF8);
  assert.notEqual(runGit('merge', 'other').status, 0);
  const result = beforeCommit();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, /a\.md/);
});
