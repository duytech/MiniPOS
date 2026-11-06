// PreToolUse hook (Bash, PowerShell): blocks `git commit` while on main and any `git push` that would update main.
// Work goes on a branch and reaches main through a pull request. Exit 2 blocks the call and tells Claude why.
// Push targets: each refspec's destination (HEAD = current branch, :main deletes main), or with no refspec the
// current branch's upstream (or the current branch itself when it has none). --all, --mirror and the refspec `:`
// (push all matching branches) include main.
import { spawnSync } from 'node:child_process';
import { gitCommands, readHookInput } from './git-changes.mjs';

const PROTECTED = 'main';

const input = readHookInput();
const command = input.tool_input?.command ?? '';

/** Runs git in the repository a command names: `target` is `{ repo, gitDir }` from gitCommands. */
const runGit = ({ repo, gitDir }, ...args) =>
  spawnSync('git', ['-C', repo, ...(gitDir ? ['--git-dir', gitDir] : []), ...args], { encoding: 'utf8' });
const git = (target, ...args) => runGit(target, ...args).stdout?.trim() ?? '';

/** Branch name a push destination points at: "main", "refs/heads/main" and "origin/main" all mean main. */
const branchOf = (ref) => ref.replace(/^refs\/heads\//, '');

// The hook runs before the whole command, so `git switch main && git commit` is still on the old branch now.
// Branches switched to earlier in the same command, per repository.
const switchedTo = new Map();
const repoKey = ({ repo, gitDir }) => gitDir ?? repo;
const currentBranch = (target) => switchedTo.get(repoKey(target)) ?? git(target, 'branch', '--show-current');

/** Branch a `git switch` / `git checkout` command moves to, or undefined when it does not change branch. */
function switchTarget(target, sub, args) {
  const create = args.findIndex((arg) => /^(-c|-C|-b|-B|--create|--force-create)$/.test(arg));
  if (create >= 0) return args[create + 1];
  const names = args.filter((arg) => !arg.startsWith('-'));
  if (names.length !== 1) return undefined;
  if (sub === 'switch') return names[0];
  // `git checkout x` may restore a file named x; it switches only when x is a local branch.
  return runGit(target, 'show-ref', '--verify', '--quiet', `refs/heads/${names[0]}`).status === 0
    ? names[0]
    : undefined;
}

function pushTargets(target, args) {
  if (args.includes('--all') || args.includes('--mirror') || args.includes('--branches')) return [PROTECTED];
  const positional = args.filter((arg) => !arg.startsWith('-'));
  const current = currentBranch(target);
  const refspecs = positional.slice(1);
  if (!refspecs.length) {
    const upstream = git(target, 'rev-parse', '--abbrev-ref', '--symbolic-full-name', `${current}@{u}`);
    return [upstream ? upstream.slice(upstream.indexOf('/') + 1) : current];
  }
  return refspecs.map((spec) => {
    const refspec = spec.replace(/^\+/, '');
    if (refspec === ':') return PROTECTED;
    const [src, dst] = refspec.split(':');
    const destination = dst ?? src;
    return branchOf(destination === 'HEAD' || destination === '@' ? current : destination);
  });
}

const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const problems = [];

for (const { repo, gitDir, sub, args } of gitCommands(command, cwd, { subshells: input.tool_name !== 'PowerShell' })) {
  const target = { repo, gitDir };
  const shown = ['git', sub, ...args].join(' ');
  if (sub === 'switch' || sub === 'checkout') {
    const branch = switchTarget(target, sub, args);
    if (branch) switchedTo.set(repoKey(target), branch);
  } else if (sub === 'commit' && currentBranch(target) === PROTECTED) {
    problems.push(`"${shown}" commits on ${PROTECTED}.`);
  } else if (sub === 'push' && pushTargets(target, args).includes(PROTECTED)) {
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
