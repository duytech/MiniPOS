// Runs the verification commands of docs/feature-list.json and writes each feature's result back: this script is the
// only thing that changes a feature's state, testedAt and evidence (.claude/hooks/protect-feature-state.mjs blocks
// hand edits).
//
//   node scripts/verify-features.mjs            every feature
//   node scripts/verify-features.mjs F05 F06    only these features
//   --no-start                                  do not start missing services (see below)
//
// A step's `requires` (db: Postgres; e2e: API on pos_e2e and web app) is started when it is not running, and what the
// script started is stopped at the end (scripts/services.mjs). With --no-start, when it cannot start, or when an API it
// did not start is already running, the step is skipped and the reason printed. Result per feature:
// a step failed → active; every step that ran passed but some were skipped → blocked; all passed → passing.
// testedAt is the start of the run when at least one step ran, and stays as it was when none did.
// Exits 1 when a `prepare` command or any feature fails.
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { ensure, stopStarted } from './services.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const listPath = path.join(root, 'docs', 'feature-list.json');
const OUTPUT_LINES = 20;
// What dotnet test, Vitest and Playwright print when nothing matched.
const NO_TESTS = /No test matches the given testcase filter|No test files found|No tests found/i;
const args = process.argv.slice(2);
const allowStart = !args.includes('--no-start');

/** Reasons the given requirements are not available after starting what is missing (unless --no-start). */
async function missing(requires) {
  const reasons = [];
  for (const name of requires) {
    const reason = await ensure(name, allowStart);
    if (reason) reasons.push(reason);
  }
  return reasons;
}

function run(step) {
  console.log(`  $ ${step.cwd ? `(${step.cwd}) ` : ''}${step.run}`);
  const result = spawnSync(step.run, {
    cwd: path.join(root, step.cwd ?? '.'),
    shell: true,
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
  });
  const text = `${result.stdout ?? ''}${result.stderr ?? ''}`;
  const output = text.trimEnd().split(/\r?\n/).slice(-OUTPUT_LINES);
  // A filter or path that matches no test still exits 0; that proves nothing, so it fails the step.
  if (result.status === 0 && NO_TESTS.test(text)) return { run: step.run, exitCode: 1, output, reason: 'no test ran' };
  return { run: step.run, exitCode: result.status ?? 1, output };
}

/** Runs the steps of one feature, stopping at the first failure. */
async function runSteps(feature) {
  const passed = [];
  const skipped = [];
  for (const step of feature.verification) {
    const reasons = await missing(step.requires ?? []);
    if (reasons.length) {
      const reason = reasons.join('; ');
      console.log(`  skipped: ${reason}`);
      skipped.push({ run: step.run, reason });
      continue;
    }
    const result = run(step);
    if (result.exitCode !== 0) return { passed, skipped, failed: result };
    passed.push(step.run);
  }
  return { passed, skipped };
}

function stateOf({ passed, skipped, failed }) {
  if (failed) return 'active';
  if (skipped.length || !passed.length) return 'blocked';
  return 'passing';
}

async function verify(feature, startedAt, commit) {
  console.log(`${feature.id} ${feature.behavior.split(': ')[0]}`);
  const outcome = await runSteps(feature);
  feature.state = stateOf(outcome);
  if (outcome.passed.length || outcome.failed) feature.testedAt = startedAt;
  feature.evidence = { commit, ...outcome };
  console.log(`  → ${feature.state}`);
}

/** Runs each `prepare` command that a selected step needs (e.g. one build before `dotnet test --no-build`). */
function prepare(list, features) {
  const runs = features.flatMap((feature) => feature.verification.map((step) => step.run));
  for (const step of list.prepare ?? []) {
    if (!runs.some((command) => command.startsWith(step.for))) continue;
    const result = run(step);
    if (result.exitCode !== 0) {
      console.error(result.output.join('\n'));
      console.error(`prepare failed: ${step.run}; ${listPath} is unchanged`);
      process.exit(1);
    }
  }
}

function select(list, ids) {
  if (!ids.length) return list.features;
  const unknown = ids.filter((id) => !list.features.some((feature) => feature.id === id));
  if (unknown.length) {
    console.error(`Unknown feature id: ${unknown.join(', ')}`);
    process.exit(1);
  }
  return list.features.filter((feature) => ids.includes(feature.id));
}

const list = JSON.parse(readFileSync(listPath, 'utf8'));
const features = select(list, args.filter((arg) => !arg.startsWith('--')));
const startedAt = new Date().toISOString().replace(/\.\d{3}Z$/, 'Z');
const commit = spawnSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: root, encoding: 'utf8' }).stdout.trim();

prepare(list, features);
process.once('SIGINT', () => {
  stopStarted();
  process.exit(130);
});
try {
  for (const feature of features) {
    await verify(feature, startedAt, commit);
    // Written after each feature, so an interrupted run keeps the results it already has.
    writeFileSync(listPath, `${JSON.stringify(list, null, 2)}\n`, 'utf8');
  }
} finally {
  stopStarted();
}

const counts = Object.groupBy(features, (feature) => feature.state);
console.log(list.states.map((state) => `${state} ${counts[state]?.length ?? 0}`).join(' / '));
process.exit(counts.active ? 1 : 0);
