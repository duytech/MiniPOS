// Tests of block-claude-coauthor.mjs: each case runs the hook on a sample command and checks the exit code (#100).
// The samples with the trailer stay in this file, since the hook blocks a command whose text has it.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const hook = fileURLToPath(new URL('./block-claude-coauthor.mjs', import.meta.url));
const BLOCKED = 2;
const ALLOWED = 0;
const TRAILER = 'Co-Authored-By: Claude <noreply@anthropic.com>';

let cwd;

function exitCode(command) {
  const input = JSON.stringify({ tool_name: 'Bash', cwd, tool_input: { command } });
  return spawnSync(process.execPath, [hook], { input, encoding: 'utf8' }).status;
}

before(() => {
  cwd = mkdtempSync(path.join(os.tmpdir(), 'pos-hook-'));
  writeFileSync(path.join(cwd, 'with-trailer.md'), `Title\n\n${TRAILER}\n`);
  writeFileSync(path.join(cwd, 'without-trailer.md'), 'Title\n');
});

after(() => {
  rmSync(cwd, { recursive: true, force: true });
});

// The commands of #100, then the two forms added with the user's OK, then what the hook already covered.
const PUBLISHING = [
  (text) => `gh pr merge 5 --squash --body "${text}"`,
  (text) => `gh pr close 5 --comment "${text}"`,
  (text) => `gh issue close 5 --comment "${text}"`,
  (text) => `git merge other -m "${text}"`,
  (text) => `gh pr reopen 5 --comment "${text}"`,
  (text) => `gh issue reopen 5 --comment "${text}"`,
  (text) => `git \\\n  commit -m "${text}"`,
  (text) => `git \`\n  commit -m "${text}"`,
  (text) => `gh \\\n  pr merge 5 --body "${text}"`,
  (text) => `git commit -m "${text}"`,
  (text) => `gh pr create --title x --body "${text}"`,
  (text) => `gh issue comment 5 --body "${text}"`,
  (text) => `gh api repos/x/y/issues -f body="${text}"`,
];

for (const form of PUBLISHING) {
  test(`blocks ${JSON.stringify(form('<trailer>'))} with a trailer`, () => {
    assert.equal(exitCode(form(TRAILER)), BLOCKED);
  });

  test(`allows ${JSON.stringify(form('<text>'))} without a trailer`, () => {
    assert.equal(exitCode(form('Only a message')), ALLOWED);
  });
}

test('blocks a trailer after a blank line in the text', () => {
  assert.equal(exitCode(`gh pr merge 5 --body "Title\n\n${TRAILER}"`), BLOCKED);
});

test('blocks a trailer that names Claude with another email', () => {
  assert.equal(exitCode('git merge other -m "Co-Authored-By: Claude Opus <claude@example.com>"'), BLOCKED);
});

test('reads the message file of a command', () => {
  assert.equal(exitCode('gh pr merge 5 --body-file with-trailer.md'), BLOCKED);
  assert.equal(exitCode('git commit -F with-trailer.md'), BLOCKED);
  assert.equal(exitCode('gh pr merge 5 --body-file without-trailer.md'), ALLOWED);
});

// Text that only mentions a command or the trailer, and commands that publish no text.
for (const command of [
  `echo "${TRAILER}"`,
  'echo "gh pr merge --body"',
  'echo "git merge -m"',
  `gh pr merge 5 --body "Notes:\n  ${TRAILER}"`,
  `git merge other -m "We do not add ${TRAILER}"`,
  'gh pr close 5 --comment "Co-Authored-By: Claude"',
  'git merge other -m "Co-Authored-By: A Person <person@example.com>"',
  'git push origin other',
  'git status',
  'gh pr view 5',
]) {
  test(`allows ${JSON.stringify(command)}`, () => {
    assert.equal(exitCode(command), ALLOWED);
  });
}
