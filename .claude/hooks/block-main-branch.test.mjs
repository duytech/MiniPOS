// Tests of block-main-branch.mjs: each case runs the hook on a sample command and checks the exit code (#100).
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const hook = fileURLToPath(new URL('./block-main-branch.mjs', import.meta.url));
const BLOCKED = 2;
const ALLOWED = 0;

let onMain;
let onBranch;

/** A temp git repo on the given branch, with forward slashes so a path works inside a sample command. */
function repoOn(branch) {
  const dir = mkdtempSync(path.join(os.tmpdir(), 'pos-hook-'));
  const init = spawnSync('git', ['init', '-q', '-b', branch, dir], { encoding: 'utf8' });
  assert.equal(init.status, 0, init.stderr);
  return dir.replaceAll('\\', '/');
}

function exitCode(command, cwd, toolName = 'Bash') {
  const input = JSON.stringify({ tool_name: toolName, cwd, tool_input: { command } });
  return spawnSync(process.execPath, [hook], { input, encoding: 'utf8' }).status;
}

before(() => {
  onMain = repoOn('main');
  onBranch = repoOn('feature');
});

after(() => {
  for (const dir of [onMain, onBranch]) rmSync(dir, { recursive: true, force: true });
});

// The forms of #100. onBranch shows that the push target decides, not the current branch.
for (const command of [
  '(git push origin main)',
  '$(git push origin main)',
  'cd . & git push origin main',
  'git --git-dir .git push origin main',
  'git --work-tree . push origin main',
  'git \\\n  push origin main',
  'git \\\r\n  push origin main',
  'git push \\\n  origin main',
  'echo "$(git push origin main)"',
  'git status & git push origin main',
]) {
  test(`blocks ${JSON.stringify(command)}`, () => {
    assert.equal(exitCode(command, onBranch), BLOCKED);
  });
}

test('blocks a commit in brackets on main', () => {
  assert.equal(exitCode('(git commit -m x)', onMain), BLOCKED);
});

test('blocks a PowerShell line continuation after git', () => {
  assert.equal(exitCode('git `\n  push origin main', onBranch, 'PowerShell'), BLOCKED);
});

test('blocks a commit after a line continuation on main', () => {
  assert.equal(exitCode('git \\\n  commit -m x', onMain), BLOCKED);
});

// Behavior the move to gitCommands must keep.
for (const command of [
  'git push origin main',
  'git push origin main 2>&1 | tail',
  'git push origin other:main',
  'git push origin :main',
  'git push origin +main',
  'git push origin refs/heads/main',
  'git push --all',
  'git push --mirror',
  'git push origin :',
  'git status; git push origin main',
  'if ($?) { git push origin main }',
]) {
  test(`still blocks ${JSON.stringify(command)}`, () => {
    assert.equal(exitCode(command, onBranch), BLOCKED);
  });
}

test('still blocks a commit on main', () => {
  assert.equal(exitCode('git commit -m x', onMain), BLOCKED);
});

test('still blocks a push of the current branch on main', () => {
  assert.equal(exitCode('git push', onMain), BLOCKED);
  assert.equal(exitCode('git push origin HEAD', onMain), BLOCKED);
});

test('still blocks a commit after a switch to main in the same command', () => {
  assert.equal(exitCode('git switch main && git commit -m x', onBranch), BLOCKED);
});

test('reads the branch of the repo that -C and cd name', () => {
  assert.equal(exitCode(`git -C ${onMain} commit -m x`, onBranch), BLOCKED);
  assert.equal(exitCode(`git -C ${onBranch} commit -m x`, onMain), ALLOWED);
  assert.equal(exitCode(`cd ${onMain} && git commit -m x`, onBranch), BLOCKED);
  assert.equal(exitCode(`cd ${onBranch} && git commit -m x`, onMain), ALLOWED);
});

test('a cd in brackets ends with the bracket in a POSIX shell and stays in PowerShell', () => {
  assert.equal(exitCode(`(cd ${onBranch}); git commit -m x`, onMain, 'Bash'), BLOCKED);
  assert.equal(exitCode(`(cd ${onBranch}); git commit -m x`, onMain, 'PowerShell'), ALLOWED);
});

for (const flag of [(dir) => `--git-dir ${dir}/.git`, (dir) => `--git-dir=${dir}/.git`]) {
  test(`reads the branch of the repo that ${flag('x')} names`, () => {
    assert.equal(exitCode(`git ${flag(onMain)} commit -m x`, onBranch), BLOCKED);
    assert.equal(exitCode(`git ${flag(onBranch)} commit -m x`, onMain), ALLOWED);
    assert.equal(exitCode(`git ${flag(onMain)} push`, onBranch), BLOCKED);
    assert.equal(exitCode(`git ${flag(onBranch)} push`, onMain), ALLOWED);
  });
}

test('a switch counts for the repo that --git-dir names only', () => {
  assert.equal(exitCode(`git --git-dir ${onMain}/.git switch -c x && git commit -m y`, onMain), BLOCKED);
  assert.equal(exitCode(`git switch -c x && git --git-dir ${onMain}/.git commit -m y`, onMain), BLOCKED);
});

test('backticks in a double-quoted string run in a POSIX shell and only escape in PowerShell', () => {
  const command = 'git commit -m "a `git push origin main` b"';
  assert.equal(exitCode(command, onBranch, 'Bash'), BLOCKED);
  assert.equal(exitCode(command, onBranch, 'PowerShell'), ALLOWED);
});

test('a backslash at the end of a line continues it only in a POSIX shell', () => {
  const command = 'git status \\\ngit commit -m x';
  assert.equal(exitCode(command, onMain, 'Bash'), ALLOWED);
  assert.equal(exitCode(command, onMain, 'PowerShell'), BLOCKED);
});

test('a backtick at the end of a line continues it only in PowerShell', () => {
  assert.equal(exitCode('git status `\ngit commit -m x', onMain, 'PowerShell'), ALLOWED);
  assert.equal(exitCode('git status \\\ngit commit -m x', onMain, 'Bash'), ALLOWED);
});

// A push to another branch, and text that only mentions a command.
for (const command of [
  'git push origin other',
  'git push origin main:other',
  'git --git-dir .git push origin other',
  '(git push origin other)',
  'git \\\n  push origin other',
  'git commit -m x',
  'git push',
  'git status',
  'echo "git push origin main"',
  "echo 'git push origin main'",
  'gh pr create --body "(git push origin main)"',
  'git commit -m "say git push origin main"',
  'git commit -m "line one \\\n git push origin main"',
  'git commit -F - <<EOF\ngit push origin main\nEOF',
  'echo "\\$(git push origin main)"',
]) {
  test(`allows ${JSON.stringify(command)}`, () => {
    assert.equal(exitCode(command, onBranch), ALLOWED);
  });
}

test('allows a commit after a switch to a new branch in the same command', () => {
  assert.equal(exitCode('git switch -c x && git commit -m y', onMain), ALLOWED);
});

test('allows a command with no git in it on main', () => {
  assert.equal(exitCode('npm run build', onMain), ALLOWED);
});
