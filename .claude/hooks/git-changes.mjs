// Helpers shared by the hooks that check uncommitted changes (verify-on-edit.mjs, check-utf8.mjs).
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';

/** The hook input JSON from stdin (a BOM is dropped: Windows PowerShell may add one). */
export function readHookInput() {
  return JSON.parse(readFileSync(0, 'utf8').replace(/^﻿/, '') || '{}');
}

/**
 * Whether a shell command runs `git commit`. Heredoc and here-string bodies and quoted strings are dropped first, so a
 * commit message or an echo that mentions "git commit" does not count.
 */
export function runsGitCommit(command) {
  const masked = command
    .replace(/<<-?\s*['"]?(\w+)['"]?[^\n]*\n[\s\S]*?\n\s*\1\b/g, '')
    .replace(/@(['"])\r?\n[\s\S]*?\r?\n\1@/g, '""')
    .replace(/"[^"]*"|'[^']*'/g, '""');
  return masked.split(/&&|\|\||[;|\n]/).some((segment) => {
    const words = segment.trim().split(/\s+/);
    const at = words.findIndex((w) => /^git(\.exe)?$/i.test(w));
    if (at < 0) return false;
    let i = at + 1;
    while (words[i]?.startsWith('-')) i += words[i] === '-C' || words[i] === '-c' ? 2 : 1;
    return words[i] === 'commit';
  });
}

/** Modified, added and untracked files (deleted ones need no check), relative to the repo root with forward slashes. */
export function changedFiles(root) {
  const status = spawnSync('git status --porcelain --untracked-files=all', { cwd: root, shell: true, encoding: 'utf8' });
  return (status.stdout ?? '')
    .split(/\r?\n/)
    .filter((line) => line.length > 3 && !line.startsWith(' D') && !line.startsWith('D '))
    .map((line) => line.slice(3).replace(/^.* -> /, '').replace(/^"|"$/g, ''));
}
