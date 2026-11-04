// PreToolUse hook (Bash, PowerShell): blocks `git push` until the `reviewer` agent has reviewed the current HEAD.
// The stamp is a file under .git/ that holds the reviewed SHA, so a new commit needs a new review. Exit 2 blocks the
// call and tells Claude why. Claude writes the stamp itself (`--stamp`), so this is a reminder, not a hard gate, and a
// push typed in a terminal is not checked. Only HEAD is compared: `git push origin other-branch` is not looked at.
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gitCommands, readHookInput } from './git-changes.mjs';

const STAMP_COMMAND = `node "${fileURLToPath(import.meta.url).replaceAll('\\', '/')}" --stamp`;
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
  const refspecs = args.filter((arg) => !arg.startsWith('-') && !/^\d*>/.test(arg)).slice(1);
  // A bare `:` is not a delete: it pushes all matching branches.
  return refspecs.length > 0 && refspecs.every((spec) => spec.startsWith(':') && spec.length > 1);
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
const commands = gitCommands(input.tool_input?.command ?? '', input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd(), {
  subshells: input.tool_name !== 'PowerShell',
});

for (const [index, { repo, sub, args }] of commands.entries()) {
  if (sub !== 'push' || sendsNoCode(args)) continue;

  // Any repo counts: `cd web && git commit && cd .. && git push` names the same repo with two different paths.
  const moved = commands.slice(0, index).find((earlier) => MOVES_HEAD.has(earlier.sub));
  if (moved) {
    process.stderr.write(
      `Blocked: this command runs "git ${moved.sub}" before "git push", so the review stamp cannot be checked against ` +
        'the commit that gets pushed. Run the push as a separate command.\n',
    );
    process.exit(2);
  }

  const head = git(repo, 'rev-parse', 'HEAD');
  if (!head) {
    process.stderr.write(
      `Blocked: cannot read HEAD in ${repo}, so the review stamp cannot be checked. A stamp does not fix this. Run the ` +
        'push from the repository folder, with no shell variable in the path.\n',
    );
    process.exit(2);
  }
  if (readStamp(repo) !== head) {
    process.stderr.write(
      `Blocked: HEAD (${head}) has no review stamp. Run the reviewer agent on git diff origin/main...HEAD and handle ` +
        'its findings. Only Verdict: approve on that full range allows the stamp, not a partial verdict. ' +
        `Then, from ${repo}, run: ${STAMP_COMMAND}\n`,
    );
    process.exit(2);
  }
}
