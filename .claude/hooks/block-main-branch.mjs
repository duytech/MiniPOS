// PreToolUse hook (Bash, PowerShell): blocks `git commit` while on main and any `git push` that would update main.
// Work goes on a branch and reaches main through a pull request. Exit 2 blocks the call and tells Claude why.
// Push targets: each refspec's destination (HEAD = current branch, :main deletes main), or with no refspec the
// current branch's upstream (or the current branch itself when it has none). --all and --mirror include main.
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const PROTECTED = 'main';

const input = JSON.parse(readFileSync(0, 'utf8').replace(/^﻿/, '') || '{}');
const command = input.tool_input?.command ?? '';
if (!/\bgit\b/.test(command)) process.exit(0);

const git = (dir, ...args) =>
  spawnSync('git', ['-C', dir, ...args], { encoding: 'utf8' }).stdout?.trim() ?? '';

// Text inside quotes, heredocs and here-strings is data (a commit message may say "git push to main"), so it must not
// be split into commands. Heredoc and here-string bodies are dropped; quoted strings become placeholders that
// words() turns back into their text.
const quoted = [];
const masked = command
  .replace(/<<-?\s*['"]?(\w+)['"]?[^\n]*\n[\s\S]*?\n\s*\1\b/g, '')
  .replace(/@(['"])\r?\n[\s\S]*?\r?\n\1@/g, '""')
  .replace(/"([^"]*)"|'([^']*)'/g, (_, double, single) => `\u0001${quoted.push(double ?? single) - 1}\u0001`);

/** Splits a masked segment into words, restoring quoted strings. */
const words = (text) =>
  text
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => word.replace(/\u0001(\d+)\u0001/g, (_, i) => quoted[Number(i)]));

/** Branch name a push destination points at: "main", "refs/heads/main" and "origin/main" all mean main. */
const branchOf = (ref) => ref.replace(/^refs\/heads\//, '');

// The hook runs before the whole command, so `git switch main && git commit` is still on the old branch now.
// Branches switched to earlier in the same command, per repository.
const switchedTo = new Map();
const currentBranch = (dir) => switchedTo.get(dir) ?? git(dir, 'branch', '--show-current');

/** Branch a `git switch` / `git checkout` segment moves to, or undefined when it does not change branch. */
function switchTarget(dir, sub, args) {
  const create = args.findIndex((arg) => /^(-c|-C|-b|-B|--create|--force-create)$/.test(arg));
  if (create >= 0) return args[create + 1];
  const names = args.filter((arg) => !arg.startsWith('-'));
  if (names.length !== 1) return undefined;
  if (sub === 'switch') return names[0];
  // `git checkout x` may restore a file named x; it switches only when x is a local branch.
  return spawnSync('git', ['-C', dir, 'show-ref', '--verify', '--quiet', `refs/heads/${names[0]}`]).status === 0
    ? names[0]
    : undefined;
}

function pushTargets(dir, args) {
  if (args.includes('--all') || args.includes('--mirror') || args.includes('--branches')) return [PROTECTED];
  const positional = args.filter((arg) => !arg.startsWith('-'));
  const current = currentBranch(dir);
  const refspecs = positional.slice(1);
  if (!refspecs.length) {
    const upstream = git(dir, 'rev-parse', '--abbrev-ref', '--symbolic-full-name', `${current}@{u}`);
    return [upstream ? upstream.slice(upstream.indexOf('/') + 1) : current];
  }
  return refspecs.map((spec) => {
    const [src, dst] = spec.replace(/^\+/, '').split(':');
    const target = dst ?? src;
    return branchOf(target === 'HEAD' || target === '@' ? current : target);
  });
}

let dir = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const problems = [];

// Walk the command segment by segment so `cd repo && git commit` checks the right repository.
for (const segment of masked.split(/&&|\|\||[;|\n]/)) {
  const all = words(segment.trim());
  const shown = all.join(' ');
  if (/^(cd|Set-Location|sl|pushd)$/i.test(all[0] ?? '') && all[1]) {
    dir = path.resolve(dir, all.slice(1).filter((w) => !w.startsWith('-'))[0] ?? '.');
    continue;
  }
  // git may follow a wrapper such as `if ($?) {` or `& `, so look for it anywhere in the segment.
  const at = all.findIndex((w) => /^git(\.exe)?$/i.test(w));
  if (at < 0) continue;
  const rest = all.slice(at + 1);

  let repo = dir;
  let i = 0;
  while (rest[i]?.startsWith('-')) {
    if (rest[i] === '-C') repo = path.resolve(repo, rest[++i] ?? '.');
    else if (rest[i] === '-c') i++;
    i++;
  }
  const [sub, ...args] = rest.slice(i);

  if (sub === 'switch' || sub === 'checkout') {
    const target = switchTarget(repo, sub, args);
    if (target) switchedTo.set(repo, target);
  } else if (sub === 'commit' && currentBranch(repo) === PROTECTED) {
    problems.push(`"${shown}" commits on ${PROTECTED}.`);
  } else if (sub === 'push' && pushTargets(repo, args).includes(PROTECTED)) {
    problems.push(`"${shown}" pushes to ${PROTECTED}.`);
  }
}

if (problems.length) {
  process.stderr.write(
    `Blocked: ${problems.join(' ')} Do not commit or push to ${PROTECTED} directly. ` +
      'Create a branch (git switch -c <name>), commit and push there, then open a pull request.\n',
  );
  process.exit(2);
}
