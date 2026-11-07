// Tests of verify-on-edit.mjs: each case runs the hook on a temp repo and checks when a push runs the checks (#103).
// The temp repo has no Pos.slnx, so the first check, `dotnet build`, fails: its name in the output shows the checks ran.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { afterEach, beforeEach, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const hook = fileURLToPath(new URL('./verify-on-edit.mjs', import.meta.url));
const BLOCKED = 2;
const ALLOWED = 0;
const CHECKS_RAN = /dotnet build failed/;

let repo;
let remote;

function git(...args) {
  const result = spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@example.com', ...args], {
    cwd: repo,
    encoding: 'utf8',
  });
  assert.equal(result.status, 0, result.stderr);
}

function write(file, text = 'x') {
  mkdirSync(path.dirname(path.join(repo, file)), { recursive: true });
  writeFileSync(path.join(repo, file), text);
}

function commit(...files) {
  for (const file of files) write(file);
  git('add', '-A');
  git('commit', '-q', '-m', 'x');
}

function runHook(command = 'git push', env = {}) {
  return spawnSync(process.execPath, [hook], {
    input: JSON.stringify({ tool_name: 'Bash', cwd: repo, tool_input: { command } }),
    encoding: 'utf8',
    env: { ...process.env, CLAUDE_PROJECT_DIR: repo, ...env },
  });
}

/** A branch with an upstream: the commits after this call are what the push sends. */
function trackedBranch() {
  git('switch', '-q', '-c', 'work');
  git('push', '-q', '-u', 'origin', 'work');
}

beforeEach(() => {
  remote = mkdtempSync(path.join(os.tmpdir(), 'pos-hook-remote-'));
  repo = mkdtempSync(path.join(os.tmpdir(), 'pos-hook-'));
  spawnSync('git', ['init', '-q', '--bare', remote]);
  spawnSync('git', ['init', '-q', '-b', 'main', repo]);
  git('remote', 'add', 'origin', remote);
  commit('src/a.cs');
  git('push', '-q', 'origin', 'main');
  git('fetch', '-q', 'origin');
});

afterEach(() => {
  for (const dir of [repo, remote]) rmSync(dir, { recursive: true, force: true });
});

for (const command of ['git status', 'git commit -m x', 'echo "git push"']) {
  test(`${command} runs no checks, also with uncommitted files`, () => {
    write('src/b.cs');
    const result = runHook(command);
    assert.equal(result.status, ALLOWED, result.stderr);
  });
}

test('commits with only *.md and docs/ changes run no checks', () => {
  trackedBranch();
  commit('README.md', 'tests/e2e/README.md', 'docs/feature-list.json');
  const result = runHook();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('nothing to push runs no checks', () => {
  trackedBranch();
  assert.equal(runHook().status, ALLOWED);
});

test('one code file among docs runs the checks and blocks on the failure', () => {
  trackedBranch();
  commit('docs/x.md', 'src/b.cs');
  const result = runHook();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, CHECKS_RAN);
});

test('a code file moved under docs/ counts as a code change', () => {
  trackedBranch();
  mkdirSync(path.join(repo, 'docs'));
  renameSync(path.join(repo, 'src/a.cs'), path.join(repo, 'docs/a.cs'));
  commit();
  assert.match(runHook().stderr, CHECKS_RAN);
});

test('a deleted code file counts as a code change', () => {
  trackedBranch();
  commit('src/b.cs');
  git('push', '-q');
  rmSync(path.join(repo, 'src/b.cs'));
  commit();
  assert.match(runHook().stderr, CHECKS_RAN);
});

test('compares against origin/main when the branch has no upstream', () => {
  git('switch', '-q', '-c', 'work');
  commit('docs/x.md');
  assert.equal(runHook().status, ALLOWED);
  commit('src/b.cs');
  assert.match(runHook().stderr, CHECKS_RAN);
});

test('runs the checks when the base cannot be resolved', () => {
  git('switch', '-q', '-c', 'work');
  git('update-ref', '-d', 'refs/remotes/origin/main');
  commit('docs/x.md');
  const result = runHook();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, CHECKS_RAN);
});

for (const [name, change] of [
  ['a new code file', () => write('src/b.cs')],
  ['a changed code file', () => write('src/a.cs', 'changed')],
  ['a deleted code file', () => rmSync(path.join(repo, 'src/a.cs'))],
]) {
  test(`${name} in the working tree runs the checks`, () => {
    trackedBranch();
    change();
    const result = runHook();
    assert.equal(result.status, BLOCKED);
    assert.match(result.stderr, CHECKS_RAN);
  });
}

test('runs the checks when the configured upstream cannot be resolved, without falling back to origin/main', () => {
  git('switch', '-q', '-c', 'work');
  git('config', 'branch.work.remote', 'origin');
  git('config', 'branch.work.merge', 'refs/heads/work');
  commit('docs/x.md');
  const result = runHook();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, CHECKS_RAN);
});

for (const limit of ['abc', '1500.5']) {
  test(`blocks when a check throws (time limit ${limit})`, () => {
    trackedBranch();
    commit('src/b.cs');
    const result = runHook('git push', { VERIFY_CHECKS_LIMIT_MS: limit });
    assert.equal(result.status, BLOCKED);
    assert.match(result.stderr, /the checks could not run/);
  });
}

test('an uncommitted doc next to commits that change code runs the checks', () => {
  trackedBranch();
  commit('src/b.cs');
  write('docs/new.md');
  const result = runHook();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, CHECKS_RAN);
});

test('an uncommitted doc next to a docs-only push runs no checks', () => {
  trackedBranch();
  commit('docs/x.md');
  write('notes/new.md');
  const result = runHook();
  assert.equal(result.status, ALLOWED, result.stderr);
});

test('git commit && git push in one command runs the checks on the code file it would commit', () => {
  trackedBranch();
  write('src/b.cs');
  git('add', '-A');
  const result = runHook('git commit -m x && git push');
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, CHECKS_RAN);
});

test('a git status that fails next to a docs-only push runs the checks', () => {
  trackedBranch();
  commit('docs/x.md');
  write('.git/index', 'not an index');
  const result = runHook();
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, CHECKS_RAN);
});

test('blocks when the checks reach their time limit', () => {
  trackedBranch();
  commit('src/b.cs');
  const result = runHook('git push', { VERIFY_CHECKS_LIMIT_MS: '0' });
  assert.equal(result.status, BLOCKED);
  assert.match(result.stderr, /time limit/);
});
