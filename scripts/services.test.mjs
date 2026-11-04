// Tests of scripts/services.mjs: the verifier never reuses an API it did not start (#84).
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { afterEach, beforeEach, test } from 'node:test';

const servers = [];
const loaded = [];
let logMock;
let moduleCount = 0;

/** A local server that answers every request, like any API or web app on the port. */
async function serverUrl() {
  const server = http.createServer((_request, response) => response.end());
  servers.push(server);
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  return `http://127.0.0.1:${server.address().port}`;
}

/** A URL on a port where nothing listens. */
async function closedUrl() {
  const server = http.createServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const url = `http://127.0.0.1:${server.address().port}`;
  await new Promise((resolve) => server.close(resolve));
  return url;
}

/** A fresh copy of the module: it reads the addresses at import and keeps results per run. */
async function services(api, web, startTimeoutMs = '1') {
  process.env.E2E_API_URL = api;
  process.env.E2E_BASE_URL = web;
  // If a refusal ever breaks, the test fails fast and afterEach stops what got started.
  process.env.VERIFY_START_TIMEOUT_MS = startTimeoutMs;
  moduleCount += 1;
  const module = await import(`./services.mjs?case=${moduleCount}`);
  loaded.push(module);
  return module;
}

const refusal = (api) => `something already answers on ${api} and the verifier cannot tell its database; stop it and run again`;

beforeEach((context) => {
  logMock = context.mock.method(console, 'log', () => {});
});

afterEach(async () => {
  for (const module of loaded.splice(0)) module.stopStarted();
  await Promise.all(servers.splice(0).map((server) => new Promise((resolve) => server.close(resolve))));
  delete process.env.E2E_API_URL;
  delete process.env.E2E_BASE_URL;
  delete process.env.VERIFY_START_TIMEOUT_MS;
});

test('refuses a running API and starts nothing, when the web app also runs', async () => {
  const api = await serverUrl();
  const { ensure } = await services(api, await serverUrl());

  assert.equal(await ensure('e2e', true), refusal(api));
  assert.equal(logMock.mock.callCount(), 0);
});

test('refuses a running API and starts nothing, when the web app is down', async () => {
  const api = await serverUrl();
  const { ensure } = await services(api, await closedUrl());

  assert.equal(await ensure('e2e', true), refusal(api));
  assert.equal(logMock.mock.callCount(), 0);
});

test('refuses a running API with --no-start', async () => {
  const api = await serverUrl();
  const { ensure } = await services(api, await serverUrl());

  assert.equal(await ensure('e2e', false), refusal(api));
});

test('says what is not running with --no-start, when the API is down', async () => {
  const api = await closedUrl();
  const web = await closedUrl();
  const { ensure } = await services(api, web);

  assert.equal(await ensure('e2e', false), `not running: web app (${web}), API (${api}); see tests/e2e/README.md`);
});

test('names only the API as not running with --no-start, when the web app runs', async () => {
  const api = await closedUrl();
  const { ensure } = await services(api, await serverUrl());

  assert.equal(await ensure('e2e', false), `not running: API (${api}); see tests/e2e/README.md`);
});

test('does not take an answer on the port for the started API, until its own log says it bound', async () => {
  const { startedStackUp } = await services(await serverUrl(), await serverUrl(), '5000');
  const folder = mkdtempSync(path.join(os.tmpdir(), 'services-test-'));
  const log = path.join(folder, 'api.log');
  try {
    writeFileSync(log, 'Building...\nSystem.IO.IOException: Failed to bind to address: address already in use.\n');
    assert.equal(await startedStackUp(log, [{ exitCode: 1 }]), false);

    writeFileSync(log, 'info: Microsoft.Hosting.Lifetime[14]\n      Now listening on: http://localhost:5191\n');
    assert.equal(await startedStackUp(log, [{ exitCode: null }]), true);
  } finally {
    rmSync(folder, { recursive: true });
  }
});
