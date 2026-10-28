// PreToolUse hook (Edit, Write, MultiEdit, NotebookEdit) for the `tester` sub-agent only: it is wired in the agent's
// frontmatter, not in settings.json, so the main session and the other agents can still edit everything.
// A path counts as inside tests/ when it sits under the tests/ folder at the top of its own git work tree, so it works
// both in the main checkout and in a sub-agent worktree (.claude/worktrees/<name>/tests/...). Anything else, including
// a path that climbs out with "..", is blocked with exit 2 and a reason Claude reads. It fails closed: input it cannot
// read blocks the call too, since any other exit code lets the edit through.
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { readHookInput } from './git-changes.mjs';

let input;
try {
  input = readHookInput();
} catch (error) {
  process.stderr.write(`Blocked: the tests-only hook could not read its input (${error.message}).\n`);
  process.exit(2);
}
const target = input.tool_input?.file_path ?? input.tool_input?.notebook_path;
if (!target) process.exit(0);

const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const file = path.resolve(cwd, target);

// The file may not exist yet (Write creates it), so ask git from the nearest folder that does.
let dir = path.dirname(file);
while (!existsSync(dir) && path.dirname(dir) !== dir) dir = path.dirname(dir);
const root = spawnSync('git', ['-C', dir, 'rev-parse', '--show-toplevel'], { encoding: 'utf8' }).stdout?.trim();

const relative = root ? path.relative(path.resolve(root), file).split(path.sep).join('/') : '';
if (relative.startsWith('tests/') && !relative.split('/').includes('..')) process.exit(0);

process.stderr.write(
  `Blocked: the tester agent may only edit files under tests/, and "${target}" is ${root ? `"${relative}" in ${root}` : 'outside any git repository'}. ` +
    'Do not change production code, configuration or the harness to make a test pass. If a test fails because of a ' +
    'bug, keep the test, and report the failure with its evidence instead.\n',
);
process.exit(2);
