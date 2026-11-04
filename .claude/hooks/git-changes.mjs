// Helpers shared by the hooks: reading the hook input, the git commands in a shell command, and the uncommitted changes
// (verify-on-edit.mjs, check-utf8.mjs, require-review-before-push.mjs).
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';

/** The hook input JSON from stdin (a BOM is dropped: Windows PowerShell may add one). */
export function readHookInput() {
  return JSON.parse(readFileSync(0, 'utf8').replace(/^﻿/, '') || '{}');
}

/**
 * The git commands in a shell command, in order: `{ repo, sub, args }` per command. `cd` and `git -C` set `repo`.
 * Heredoc and here-string bodies are dropped and quoted strings stay one word, so a commit message or an echo that
 * mentions "git commit" does not count. Pass `subshells: false` for PowerShell, where brackets do not scope a cd.
 */
export function gitCommands(command, cwd = process.cwd(), { subshells = true } = {}) {
  const quoted = [];
  const masked = command
    .replace(/<<-?\s*['"]?(\w+)['"]?[^\n]*\n[\s\S]*?\n\s*\1\b/g, '')
    .replace(/@(['"])\r?\n[\s\S]*?\r?\n\1@/g, '""')
    .replace(/"([^"]*)"|'([^']*)'/g, (_, double, single) => `\u0001${quoted.push(double ?? single) - 1}\u0001`);
  const words = (text) =>
    text
      .split(/\s+/)
      .filter(Boolean)
      .map((word) => word.replace(/\u0001(\d+)\u0001/g, (_, i) => quoted[Number(i)]));
  // Git Bash writes D:\x as /d/x; path.resolve would read that as a folder on the current drive.
  const fromShell = (dir) => (process.platform === 'win32' ? dir.replace(/^\/([a-zA-Z])(?=\/|$)/, '$1:') : dir);

  const commands = [];
  let dir = cwd;
  // $(x), ${x} and PowerShell's -C (x) stay one word (git -C $(pwd) push). The commands inside are kept per word.
  const inners = new Map();
  const unwrapped = masked.replace(/(?:\$|(?<=\s-[Cc]\s+))\(([^()]*)\)|\$\{[^{}]*\}/g, (text, inner) => {
    const word = quoted.push(text) - 1;
    if (inner) inners.set(word, inner);
    return `\u0001${word}\u0001`;
  });

  const outerDirs = [];
  // A single & also separates commands, except in a redirect (2>&1, &>file). So do brackets: if (git x) { git push }.
  const walk = (script) => {
    for (const segment of script.split(/(&&|\|\||[;|\n(){}]|(?<!>)&(?!>))/)) {
      // A cd inside (x) or $(x) ends with the bracket in a POSIX shell. PowerShell has no subshell: the cd stays.
      if (subshells && segment === '(') outerDirs.push(dir);
      if (subshells && segment === ')') dir = outerDirs.pop() ?? dir;
      // The commands inside a substitution run just before the command that holds it.
      for (const [, word] of segment.matchAll(/\u0001(\d+)\u0001/g)) {
        if (inners.has(Number(word))) walk(`(${inners.get(Number(word))})`);
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
      let i = 0;
      while (rest[i]?.startsWith('-')) {
        if (rest[i] === '-C') repo = path.resolve(repo, fromShell(rest[++i] ?? '.'));
        else if (rest[i] === '-c') i++;
        i++;
      }
      const [sub, ...args] = rest.slice(i);
      if (sub) commands.push({ repo, sub, args });
    }
  };
  walk(unwrapped);
  return commands;
}

/** Whether a shell command runs `git commit`. */
export function runsGitCommit(command) {
  return gitCommands(command).some(({ sub }) => sub === 'commit');
}

/** Modified, added and untracked files (deleted ones need no check), relative to the repo root with forward slashes. */
export function changedFiles(root) {
  const status = spawnSync('git status --porcelain --untracked-files=all', { cwd: root, shell: true, encoding: 'utf8' });
  return (status.stdout ?? '')
    .split(/\r?\n/)
    .filter((line) => line.length > 3 && !line.startsWith(' D') && !line.startsWith('D '))
    .map((line) => line.slice(3).replace(/^.* -> /, '').replace(/^"|"$/g, ''));
}
