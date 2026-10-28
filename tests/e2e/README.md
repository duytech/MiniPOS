# E2E tests (Playwright)

Browser tests for the web app, run against the API on its own database (`pos_e2e`) so they never touch dev data.
The database keeps data between runs: tests create what they need through the API with unique names.

## One-time setup

```bash
docker compose up -d
docker compose exec db sh -c 'createdb -U "$POSTGRES_USER" pos_e2e'
npm install --prefix tests/e2e
```

Playwright reuses the Chromium that the Playwright MCP server installed; if it is missing, run
`npx playwright install chromium` in `tests/e2e`.

## Run

1. Start the API on `pos_e2e`: the same connection string as the dev one (user-secrets of `src/Pos.Api`), with
   `Database=pos_e2e`, in the `ConnectionStrings__Pos` environment variable. The API migrates the empty database on
   startup. In Git Bash:

   ```bash
   export ConnectionStrings__Pos="$(dotnet user-secrets list --project src/Pos.Api | sed -n 's/^ConnectionStrings:Pos = //p' | sed 's/Database=[^;]*/Database=pos_e2e/')"
   dotnet run --project src/Pos.Api --launch-profile http
   ```

2. Start the web app: `npm start` in `web/` (it proxies `/api` to the API).
3. In `tests/e2e`: `npx playwright test` (both `desktop` and `phone` at 375 px), `npx tsc -p .` for types.

`E2E_BASE_URL` overrides the web app address. Failures keep a trace and a screenshot in `test-results/`; the HTML
report is in `playwright-report/`.
