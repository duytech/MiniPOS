// Helpers shared by the hooks: reading the hook input, the git commands in a shell command, and the uncommitted changes
// (verify-on-edit.mjs, check-utf8.mjs).
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';

// Git options before the subcommand whose value is the next word (-C and --git-dir are read on their own).
const OPTIONS_WITH_VALUE = /^(-c|--work-tree|--namespace|--config-env|--attr-source)$/;
// A git status entry whose file is gone from the index or working tree. UD and DU are conflicts: the file is still there.
const DELETED = /^(?:D[^U]|[^U]D)/;

/** The hook input JSON from stdin (a BOM is dropped: Windows PowerShell may add one). */
export function readHookInput() {
  return JSON.parse(readFileSync(0, 'utf8').replace(/^﻿/, '') || '{}');
}

/**
 * The git commands in a shell command, in order: `{ repo, gitDir, sub, args }` per command. `cd` and `git -C` set `repo`;
 * `gitDir` is the `--git-dir` value, when there is one. Heredoc and here-string bodies are dropped and quoted strings stay one word, so a commit message or an echo that
 * mentions "git commit" does not count. Pass `subshells: false` for PowerShell, where brackets do not scope a cd.
 */
export function gitCommands(command, cwd = process.cwd(), { subshells = true } = {}) {
  const quoted = [];
  // The commands inside a word that stays whole, per word: $(x), `x`.
  const inners = new Map();
  const keepWord = (text, commandsInside = []) => {
    const word = quoted.push(text) - 1;
    if (commandsInside.length) inners.set(word, commandsInside);
    return `\u0001${word}\u0001`;
  };
  // A double-quoted string still runs $(x), and `x` in a POSIX shell, unless escaped. PowerShell's backtick only escapes.
  // The first branch takes each escape with its character, so \\$(x) is a live command and \\\$(x) is not.
  const inDoubleQuotes = subshells ? /\\[\s\S]|\$\(([^()]*)\)|`([^`]*)`/g : /`[\s\S]|\$\(([^()]*)\)/g;
  const substitutions = (text) =>
    [...text.matchAll(inDoubleQuotes)].map((match) => match[1] ?? match[2]).filter((inner) => inner !== undefined);
  // A heredoc loses its marker and body. The rest of its opening line is still commands: <<EOF && git push.
  const masked = command
    .replace(/<<-?\s*['"]?(\w+)['"]?([^\n]*)\n(?:[\s\S]*?\n)?\s*\1\b/g, '$2')
    .replace(/@(['"])\r?\n[\s\S]*?\r?\n\1@/g, '""')
    .replace(/"([^"]*)"|'([^']*)'/g, (_, double, single) =>
      double === undefined ? keepWord(single) : keepWord(double, substitutions(double)),
    );
  const words = (text) =>
    text
      .split(/\s+/)
      .filter(Boolean)
      .map((word) => word.replace(/\u0001(\d+)\u0001/g, (_, i) => quoted[Number(i)]));
  // A line continuation runs as one line: \ in a POSIX shell, a backtick in PowerShell. An escaped one (\\) ends the line.
  // Read after the masking above, so quoted text and heredocs stay as they are.
  const joined = subshells
    ? masked.replace(/(?<!\\)((?:\\\\)*)\\\r?\n/g, '$1')
    : masked.replace(/(?<!`)((?:``)*)`\r?\n/g, '$1 ');
  // Git Bash writes D:\x as /d/x; path.resolve would read that as a folder on the current drive.
  const fromShell = (dir) => (process.platform === 'win32' ? dir.replace(/^\/([a-zA-Z])(?=\/|$)/, '$1:') : dir);

  const commands = [];
  let dir = cwd;
  // `x`, ${x}, PowerShell's -C (x) and brackets that touch a word ($(x), x=(y), D:\foo(1), foo{1}) stay one word. A brace
  // with a space or after a bracket is a block: else{git push}, if(x){git push}. Repeated, so an outer bracket is seen
  // once its inner one is a word: $(dirname $(pwd)).
  let unwrapped = subshells ? joined.replace(/`([^`]*)`/g, (text, inner) => keepWord(text, [inner])) : joined;
  for (let previous; previous !== unwrapped; ) {
    previous = unwrapped;
    unwrapped = unwrapped.replace(
      /(?:(?<=[^\s;&|(){}])|(?<=\s-[Cc]\s+))\(([^()]*)\)|\$\{[^{}]*\}|(?<=[^\s;&|(){}\u0001])\{[^{}\s\u0001]*\}/g,
      (text, inner) => keepWord(text, inner ? [inner] : []),
    );
  }

  const outerDirs = [];
  // A single & also separates commands, except in a redirect (2>&1, &>file). So do brackets: if (git x) { git push }.
  const walk = (script) => {
    for (const segment of script.split(/(&&|\|\||[;|\n(){}]|(?<!>)&(?!>))/)) {
      // A cd inside (x) or $(x) ends with the bracket in a POSIX shell. PowerShell has no subshell: the cd stays.
      if (subshells && segment === '(') outerDirs.push(dir);
      if (subshells && segment === ')') dir = outerDirs.pop() ?? dir;
      // The commands inside a substitution run just before the command that holds it.
      for (const [, word] of segment.matchAll(/\u0001(\d+)\u0001/g)) {
        for (const inner of inners.get(Number(word)) ?? []) walk(`(${inner})`);
      }
      const all = words(segment.trim());
      if (/^(cd|Set-Location|sl|pushd)$/i.test(all[0] ?? '') && all[1]) {
        dir = path.resolve(dir, fromShell(all.slice(1).filter((w) => !w.startsWith('-'))[0] ?? '.'));
        continue;
      }
      // git may follow a wrapper such as `if ($?) {`, so look for it anywhere in the segment.
      const at = all.findIndex((w) => /^git(\.exe)?$/i.test(w));
      if (at < 0) continue;
      const rest = all.slice(at + 1);
      let repo = dir;
      let gitDir;
      let i = 0;
      while (rest[i]?.startsWith('-')) {
        if (rest[i] === '-C') repo = path.resolve(repo, fromShell(rest[++i] ?? '.'));
        else if (rest[i] === '--git-dir') gitDir = rest[++i];
        else if (rest[i].startsWith('--git-dir=')) gitDir = rest[i].slice('--git-dir='.length);
        else if (OPTIONS_WITH_VALUE.test(rest[i])) i++;
        i++;
      }
      const [sub, ...args] = rest.slice(i);
      // Git reads a relative --git-dir after every -C, so it resolves against the final repo.
      if (sub) commands.push({ repo, gitDir: gitDir && path.resolve(repo, fromShell(gitDir)), sub, args });
    }
  };
  walk(unwrapped);
  return commands;
}

/** Whether the hook input's shell command runs this git subcommand. */
export function runsGit({ tool_name, tool_input }, subcommand) {
  const commands = gitCommands(tool_input?.command ?? '', undefined, { subshells: tool_name !== 'PowerShell' });
  return commands.some(({ sub }) => sub === subcommand);
}

/**
 * Modified, added and untracked files, relative to the repo root with forward slashes. Deleted ones only with
 * `deleted: true`. Throws when git status fails: a partial list would let a hook skip a file.
 */
export function changedFiles(root, { deleted = false } = {}) {
  const status = spawnSync('git', ['status', '--porcelain', '-z', '--no-renames', '--untracked-files=all'], {
    cwd: root,
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
  });
  if (status.error || status.status !== 0) {
    throw new Error(`git status failed: ${status.error?.message ?? status.stderr.trim()}`);
  }
  return status.stdout
    .split('\0')
    .filter((entry) => entry && (deleted || !DELETED.test(entry)))
    .map((entry) => entry.slice(3));
}
