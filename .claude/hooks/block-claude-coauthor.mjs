// PreToolUse hook (Bash, PowerShell): blocks a commit, PR or issue whose text has a "Co-Authored-By: Claude" line.
// Checks the command itself (-m "...", --body "...", heredocs) and any message file it passes
// (git commit -F/--file, gh --body-file/-F). Exit 2 blocks the call and tells Claude why.
// .githooks/commit-msg strips the same line from commits made outside Claude; this hook also covers PR bodies.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

// A trailer starts a line (or a -m "..." argument) and names an email: "Co-Authored-By: Claude <noreply@anthropic.com>".
// Prose that mentions the trailer (indented, or without an email) is not one.
const CO_AUTHOR = /(?:^|["'])co-authored-by:[^\n<]*(?:claude[^\n<]*<[^>\n]*>|<[^>\n]*noreply@anthropic\.com>)/im;
const PUBLISHES_TEXT = /\bgit\b[^\n|;&]*\bcommit\b|\bgh\s+(pr|issue)\s+(create|edit|comment|review)\b|\bgh\s+api\b/;
const MESSAGE_FILE = /(?:^|\s)(?:-F|--file|--body-file)(?:=|\s+)(?:"([^"]+)"|'([^']+)'|(\S+))/g;

const input = JSON.parse(readFileSync(0, 'utf8').replace(/^﻿/, '') || '{}');
const command = input.tool_input?.command ?? '';
if (!PUBLISHES_TEXT.test(command)) process.exit(0);

const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const texts = [command];
for (const match of command.matchAll(MESSAGE_FILE)) {
  const file = match[1] ?? match[2] ?? match[3];
  const full = path.resolve(cwd, file);
  if (file !== '-' && existsSync(full)) texts.push(readFileSync(full, 'utf8'));
}

const line = texts.map((text) => text.match(CO_AUTHOR)?.[0]).find(Boolean);
if (line) {
  process.stderr.write(
    `Blocked: the commit/PR text contains "${line.trim()}". ` +
      'This repo does not credit Claude as co-author. Remove that line and run the command again.\n',
  );
  process.exit(2);
}
