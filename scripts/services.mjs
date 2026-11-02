// The services a verification step of docs/feature-list.json can require, used by verify-features.mjs:
//   db   Postgres of docker-compose.yml
//   e2e  the API on the pos_e2e database and the web app, as in tests/e2e/README.md (needs db)
// ensure() checks a service and, when allowed, starts what is missing and waits for it. stopStarted() stops only what
// ensure() started; a service that was already running is left alone. Addresses come from the environment, with the
// defaults of CLAUDE.md.
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, openSync } from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const DB_HOST = process.env.POS_DB_HOST ?? 'localhost';
const DB_PORT = Number(process.env.POS_DB_PORT ?? 5433);
const WEB = process.env.E2E_BASE_URL ?? 'http://localhost:4200';
const API = process.env.E2E_API_URL ?? 'http://localhost:5191';
const E2E_DATABASE = 'pos_e2e';
const CHECK_TIMEOUT_MS = 3000;
// dotnet run and ng serve build first: a cold start takes minutes.
const START_TIMEOUT_MS = Number(process.env.VERIFY_START_TIMEOUT_MS ?? 300_000);
const POLL_MS = 1000;

/** Whether a TCP port accepts a connection. */
function portOpen(host, port) {
  return new Promise((resolve) => {
    const socket = net.connect({ host, port, timeout: CHECK_TIMEOUT_MS });
    const done = (open) => {
      socket.destroy();
      resolve(open);
    };
    socket.once('connect', () => done(true));
    socket.once('timeout', () => done(false));
    socket.once('error', () => done(false));
  });
}

/** Whether a URL answers at all (any HTTP status counts: only reachability matters). */
async function answers(url) {
  try {
    await fetch(url, { signal: AbortSignal.timeout(CHECK_TIMEOUT_MS) });
    return true;
  } catch {
    return false;
  }
}

const started = [];

/** Kills a background command and every process it started (dotnet run and ng serve start child processes). */
function killTree(pid) {
  if (process.platform === 'win32') {
    spawnSync('taskkill', ['/PID', String(pid), '/T', '/F'], { stdio: 'ignore' });
    return;
  }
  try {
    process.kill(-pid, 'SIGTERM');
  } catch {
    // Already gone.
  }
}

/** Starts a long-running command in the background, its output in a log file under the temp folder. */
function background(label, command, logName) {
  const log = path.join(os.tmpdir(), `verify-features-${logName}.log`);
  const out = openSync(log, 'w');
  const child = spawn(command, {
    cwd: root,
    shell: true,
    stdio: ['ignore', out, out],
    detached: process.platform !== 'win32',
    windowsHide: true,
  });
  started.push({ label, stop: () => killTree(child.pid) });
  console.log(`  started ${label} (log: ${log})`);
  return { child, log };
}

/** Polls until isUp() is true; false on timeout or as soon as a started command has exited. */
async function waitFor(isUp, children = []) {
  const deadline = Date.now() + START_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (await isUp()) return true;
    if (children.some((child) => child.exitCode !== null)) return false;
    await new Promise((resolve) => setTimeout(resolve, POLL_MS));
  }
  return false;
}

function docker(...args) {
  const result = spawnSync('docker', args, { cwd: root, encoding: 'utf8' });
  const lastLine = `${result.stdout ?? ''}${result.stderr ?? ''}`.trim().split(/\r?\n/).pop() ?? '';
  return { ok: result.status === 0, lastLine };
}

const dbUp = () => portOpen(DB_HOST, DB_PORT);
const apiUp = () => answers(API);
const webUp = () => answers(WEB);

/** Docker Desktop's executable on Windows, the same places init.sh looks: next to the docker CLI, per user, machine. */
function dockerDesktopExe() {
  const cli = spawnSync('where', ['docker'], { encoding: 'utf8' }).stdout?.split(/\r?\n/)[0] ?? '';
  return [
    cli && path.join(path.dirname(cli), '..', '..', 'Docker Desktop.exe'),
    path.join(process.env.LOCALAPPDATA ?? '', 'Programs', 'DockerDesktop', 'Docker Desktop.exe'),
    path.join(process.env.ProgramW6432 ?? 'C:/Program Files', 'Docker', 'Docker', 'Docker Desktop.exe'),
  ].find((candidate) => candidate && existsSync(candidate));
}

/**
 * Starts Docker Desktop when the daemon is down (Windows and macOS, like init.sh) and waits for it. Docker Desktop is
 * left running at the end: only the db container is stopped.
 */
async function ensureDockerDaemon() {
  const daemonUp = async () => docker('info').ok;
  if (await daemonUp()) return undefined;
  if (process.platform === 'win32') {
    const exe = dockerDesktopExe();
    if (!exe) return 'the Docker daemon is not running and Docker Desktop was not found';
    spawn(exe, { detached: true, stdio: 'ignore' }).unref();
  } else if (process.platform === 'darwin') {
    spawnSync('open', ['-a', 'Docker']);
  } else {
    return 'the Docker daemon is not running';
  }
  console.log('  started Docker Desktop');
  return (await waitFor(daemonUp)) ? undefined : 'Docker Desktop did not start';
}

async function startDb() {
  const daemon = await ensureDockerDaemon();
  if (daemon) return daemon;
  const up = docker('compose', 'up', '-d', 'db');
  if (!up.ok) return `docker compose up -d failed: ${up.lastLine}`;
  started.push({ label: 'Postgres', stop: () => docker('compose', 'stop', 'db') });
  console.log('  started Postgres (docker compose up -d)');
  // The published port opens as soon as the container runs, and the image first runs a temporary init server on the
  // unix socket only: wait until the real server answers over TCP inside the container.
  const ready = async () => docker('compose', 'exec', '-T', 'db', 'sh', '-c', 'pg_isready -h localhost -U "$POSTGRES_USER"').ok;
  return (await waitFor(ready)) ? undefined : `Postgres did not get ready in the db container`;
}

/** Creates the e2e database when it is missing (the one-time setup of tests/e2e/README.md). */
function createE2eDatabase() {
  const script =
    `psql -U "$POSTGRES_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '${E2E_DATABASE}'" ` +
    `| grep -q 1 || createdb -U "$POSTGRES_USER" ${E2E_DATABASE}`;
  const result = docker('compose', 'exec', '-T', 'db', 'sh', '-c', script);
  return result.ok ? undefined : `could not create the ${E2E_DATABASE} database: ${result.lastLine}`;
}

async function startE2e() {
  const db = await ensure('db', true);
  if (db) return db;
  const created = createE2eDatabase();
  if (created) return created;
  const shell = process.platform === 'win32' ? 'powershell' : 'pwsh';
  const runs = [];
  if (!(await apiUp())) {
    runs.push(background('API on pos_e2e', `${shell} -NoProfile -ExecutionPolicy Bypass -File tests/e2e/start-api.ps1`, 'api'));
  }
  if (!(await webUp())) runs.push(background('web app', 'npm start --prefix web', 'web'));
  const ok = await waitFor(async () => (await apiUp()) && (await webUp()), runs.map((run) => run.child));
  return ok ? undefined : `API (${API}) or web app (${WEB}) did not start; logs: ${runs.map((run) => run.log).join(', ')}`;
}

// down() returns undefined when the service answers, else the reason; start() starts it and returns the same.
const SERVICES = {
  db: {
    down: async () => ((await dbUp()) ? undefined : `Postgres is not reachable on ${DB_HOST}:${DB_PORT}`),
    start: startDb,
  },
  e2e: {
    down: async () => {
      const down = [];
      if (!(await webUp())) down.push(`web app (${WEB})`);
      if (!(await apiUp())) down.push(`API (${API})`);
      return down.length ? `not running: ${down.join(', ')}; see tests/e2e/README.md` : undefined;
    },
    start: startE2e,
  },
};

const results = new Map();

/** Undefined when the service is available (started first when allowed), else the reason. Once per service per run. */
export async function ensure(name, allowStart) {
  const service = SERVICES[name];
  if (!service) throw new Error(`Unknown requirement "${name}"`);
  if (!results.has(name)) {
    const down = await service.down();
    results.set(name, down && allowStart ? await service.start() : down);
  }
  return results.get(name);
}

/** Stops what ensure() started, last started first. */
export function stopStarted() {
  for (const { label, stop } of started.splice(0).reverse()) {
    console.log(`stopping ${label}`);
    stop();
  }
}
