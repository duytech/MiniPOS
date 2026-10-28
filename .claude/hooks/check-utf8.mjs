// Stop hook: when Claude is about to end its turn, reject changed text files that are not UTF-8. A tool that writes the
// system code page (Windows PowerShell's Set-Content) turns "·" into a single byte that compilers, linters and tests
// accept, but browsers show as "�".
// A failure exits with code 2 and prints the files on stderr, which keeps Claude working to fix them.
// With --before-commit it is a PreToolUse hook (Bash, PowerShell) instead: it checks only when the command runs
// `git commit`, and exit 2 blocks the commit.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { changedFiles, readHookInput, runsGitCommit } from './git-changes.mjs';

const TEXT_EXTENSIONS = ['.cs', '.csproj', '.props', '.ts', '.html', '.css', '.json', '.md', '.mjs'];

const input = readHookInput();
const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
const beforeCommit = process.argv.includes('--before-commit');

if (beforeCommit && !runsGitCommit(input.tool_input?.command ?? '')) process.exit(0);

const notUtf8 = changedFiles(root).filter((file) => {
  if (!TEXT_EXTENSIONS.includes(path.extname(file).toLowerCase())) return false;
  try {
    new TextDecoder('utf-8', { fatal: true }).decode(readFileSync(path.join(root, file)));
    return false;
  } catch {
    return true;
  }
});

if (notUtf8.length) {
  const heading = beforeCommit
    ? 'Commit blocked: these files are not UTF-8. Rewrite them as UTF-8, then commit again:'
    : 'These files are not UTF-8. Rewrite them as UTF-8 before finishing:';
  process.stderr.write([heading, ...notUtf8.map((file) => `  ${file}`)].join('\n') + '\n');
  process.exit(2);
}
