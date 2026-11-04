// PreToolUse hook (Bash, PowerShell): blocks `git push` until the `reviewer` agent has reviewed the current HEAD.
// The stamp is a file under .git/ that holds the reviewed SHA, so a new commit needs a new review. Exit 2 blocks the
// call and tells Claude why. Claude writes the stamp itself (`--stamp`), so this is a reminder, not a hard gate, and a
// push typed in a terminal is not checked. Only HEAD is compared: `git push origin other-branch` is not looked at.
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { gitCommands, readHookInput } from './git-changes.mjs';

const STAMP_COMMAND = 'node .claude/hooks/require-review-before-push.mjs --stamp';
// The hook runs before the whole command, so after one of these the HEAD it reads is not the HEAD that gets pushed.
const MOVES_HEAD = new Set(['commit', 'merge', 'rebase', 'pull', 'cherry-pick', 'revert', 'am', 'reset', 'switch', 'checkout']);

const git = (dir, ...args) => spawnSync('git', ['-C', dir, ...args], { encoding: 'utf8' }).stdout?.trim() ?? '';
const stampPath = (dir) => path.resolve(dir, git(dir, 'rev-parse', '--git-path', 'reviewed-sha'));

function readStamp(dir) {
  try {
    return readFileSync(stampPath(dir), 'utf8').trim();
  } catch {
    return '';
  }
}

/** A push that sends no new code: a dry run, or one that only deletes remote branches. */
function sendsNoCode(args) {
  if (args.some((arg) => /^(--delete|-d|--dry-run|-n)$/.test(arg))) return true;
  const refspecs = args.filter((arg) => !arg.startsWith('-')).slice(1);
  return refspecs.length > 0 && refspecs.every((spec) => spec.startsWith(':'));
}

if (process.argv.includes('--stamp')) {
  const dir = process.cwd();
  const head = git(dir, 'rev-parse', 'HEAD');
  if (!head) {
    process.stderr.write('Cannot read HEAD, so no review stamp was written.\n');
    process.exit(1);
  }
  writeFileSync(stampPath(dir), `${head}\n`);
  process.stdout.write(`Review stamp written for ${head}\n`);
  process.exit(0);
}

const input = readHookInput();
const commands = gitCommands(input.tool_input?.command ?? '', input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd());

for (const [index, { repo, sub, args }] of commands.entries()) {
  if (sub !== 'push' || sendsNoCode(args)) continue;

  const moved = commands.slice(0, index).find((earlier) => earlier.repo === repo && MOVES_HEAD.has(earlier.sub));
  if (moved) {
    process.stderr.write(
      `Blocked: this command runs "git ${moved.sub}" before "git push", so the review stamp cannot be checked against ` +
        'the commit that gets pushed. Run the push as a separate command.\n',
    );
    process.exit(2);
  }

  const head = git(repo, 'rev-parse', 'HEAD');
  if (!head || readStamp(repo) !== head) {
    process.stderr.write(
      `Blocked: HEAD (${head || 'unknown'}) has no review stamp. Run the reviewer agent on ` +
        'git diff origin/main...HEAD and handle its findings. Do not stamp while a blocker is open, unless the user ' +
        `waives that finding. Then run: ${STAMP_COMMAND}\n`,
    );
    process.exit(2);
  }
}
