// PreToolUse hook (Edit, Write): blocks hand edits of a feature's state, testedAt and evidence in
// docs/feature-list.json. Only scripts/verify-features.mjs sets them, from the result of the verification commands.
// A new feature must start as not_started with testedAt and evidence null. Exit 2 blocks the call and tells Claude why.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { readHookInput } from './git-changes.mjs';

const LIST = 'docs/feature-list.json';
const VERIFIED = ['state', 'testedAt', 'evidence'];
const NEW_FEATURE = { state: 'not_started', testedAt: null, evidence: null };

const input = readHookInput();
const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
const tool = input.tool_input ?? {};
const file = path.resolve(input.cwd || root, tool.file_path ?? '');
if (path.relative(root, file).replaceAll('\\', '/') !== LIST) process.exit(0);

const before = existsSync(file) ? readFileSync(file, 'utf8') : '';

/** The file content after the edit: Write gives it whole, Edit replaces old_string the way the tool does. */
function contentAfter() {
  if (typeof tool.content === 'string') return tool.content;
  const edits = tool.edits ?? [tool];
  return edits.reduce(
    (text, edit) =>
      edit.replace_all
        ? text.replaceAll(edit.old_string, edit.new_string)
        : text.replace(edit.old_string, () => edit.new_string),
    before,
  );
}

function block(reason) {
  process.stderr.write(
    `Blocked: ${reason} state, testedAt and evidence in ${LIST} are set only by ` +
      '`node scripts/verify-features.mjs <id>`; edit the other fields, then run it.\n',
  );
  process.exit(2);
}

function featuresOf(text) {
  try {
    return new Map((JSON.parse(text || '{"features":[]}').features ?? []).map((feature) => [feature.id, feature]));
  } catch {
    return undefined;
  }
}

const old = featuresOf(before) ?? new Map();
const next = featuresOf(contentAfter());
if (!next) block(`the edit leaves ${LIST} as invalid JSON.`);

const changed = [];
for (const [id, feature] of next) {
  const expected = old.get(id) ?? NEW_FEATURE;
  const fields = VERIFIED.filter((field) => JSON.stringify(feature[field] ?? null) !== JSON.stringify(expected[field] ?? null));
  if (fields.length) changed.push(`${id} (${fields.join(', ')})`);
}
if (changed.length) block(`the edit changes ${changed.join(', ')}.`);
