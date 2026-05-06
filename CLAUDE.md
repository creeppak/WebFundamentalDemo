# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

Virtual stock trading demo platform. Users register, receive a virtual cash balance, and can buy/sell a curated set of 5–10 stocks. Each stock page shows fundamentals, a 14-day price chart, and an AI-generated fundamental analysis. Analyses are pre-generated nightly and served from the database — never generated per user request.

This is a **demo project**, not a production trading system. Optimize for clarity, shippability, and cost over scale.

## Architecture

Four deployable services:

- **`src/Api`** — ASP.NET Core REST API. Handles auth, stock data reads, trading operations.
- **`src/Worker`** — .NET Background Service. Runs nightly jobs: price sync, fundamentals sync, news sync, Claude analysis generation.
- **`src/Web`** — Blazor WebAssembly client. Consumes the API.
- **`src/Shared`** — DTOs, domain models, shared contracts between API and Worker.

**Data flow:**
```
Nightly jobs (Worker) → Market data APIs + Claude API → Postgres
Blazor client → ASP.NET API → Postgres (reads cached data)
```

**Key principle:** the Claude API is called **only** by the nightly Worker job, never from the API layer in response to a user request. If you find yourself adding an LLM call to a request path, stop and reconsider.

## Tech Stack

- **.NET 8** / C# (ASP.NET Core, Blazor WebAssembly, Worker Service)
- **PostgreSQL** + EF Core (Npgsql provider) — Cloud SQL for PostgreSQL (`db-f1-micro`) with private IP via VPC peering
- **Mapperly** for compile-time DTO↔domain object mapping
- Docker + Docker Compose for local dev
- **GCP Cloud Run** deployment (Cloud Run, Artifact Registry, Cloud SQL, Secret Manager, Cloud Scheduler for Worker job, Cloud Run domain mappings for HTTPS)
- GitHub Actions for CI/CD (Workload Identity Federation auth to GCP)
- **Anthropic Claude API** for analysis generation (`claude-sonnet-4-5`)
- **Finnhub** for market data (fundamentals, news) — free tier covers both
- **Alpha Vantage** for daily OHLCV price history — free tier is 25 req/day

## Development Workflow

**Local setup:**
```bash
docker compose up -d postgres
dotnet ef database update --project src/Api
dotnet run --project src/Api      # terminal 1
dotnet run --project src/Worker   # terminal 2
dotnet run --project src/Web      # terminal 3
```

Full stack in Docker:
```bash
docker compose up --build
```

**Migrations:**
```bash
dotnet ef migrations add <Name> --project src/Api --startup-project src/Api
dotnet ef database update --project src/Api
```

Always review generated migrations before committing — EF Core sometimes produces destructive diffs.

**Tests:**
```bash
dotnet test
```

## Code Conventions

- **Async everywhere** for I/O. Never block on `.Result` or `.Wait()`.
- **Cancellation tokens** on every async method that does I/O — especially in Worker jobs.
- **No business logic in controllers.** Controllers validate input and delegate to services.
- **No direct `DbContext` use in controllers.** Go through a repository or service layer.
- **DTOs at API boundaries.** Never expose EF entities directly in API responses.
- **Records for DTOs**, classes for entities.
- **FluentValidation** for request validation, not data annotations on DTOs.
- **Mapperly** for all DTO↔domain object mapping (both directions). Never write hand-rolled mapping code — add a partial mapper class annotated with `[Mapper]` in the relevant project. Mappers live in a `Mappers/` folder next to the types they map.
- **Nullable reference types enabled.** Don't disable to silence warnings — fix the nullability.

## Database

- Schema changes go through EF Core migrations. No hand-written SQL migrations.
- Index on `(ticker, date DESC)` for `prices`, `fundamentals`, and `analyses` tables — these are the hot read paths.
- Money-like fields (cash balance, stock prices, transaction amounts) use `decimal(18,4)`, never `double` or `float`.
- Transactions for multi-step operations (e.g. buy = deduct cash + insert transaction row + update holding must be atomic). Use `IDbContextTransaction`.
- **Migration ownership:** the `Api` project owns migrations. The `Worker` reads and writes data but does not run migrations.
- **Do not run migrations from Api startup in production.** Run them as a one-off Cloud Run Job before deploying a new Api revision. Multiple Api instances racing on startup corrupts migration history.
- **Production database is Cloud SQL** (`db-f1-micro`, private IP via VPC peering). Local dev still uses Docker Postgres (see `docker-compose.yml`). Do not confuse the two connection strings.

## Authentication

- ASP.NET Identity for user management, JWT for API auth.
- JWTs signed with a key from GCP Secret Manager in prod, user secrets locally.
- **Never** store JWTs in `localStorage` in Blazor WASM — use `sessionStorage` or in-memory with a refresh token flow.
- Access tokens short-lived (15 min), refresh tokens longer (7 days), rotated on use.
- Every non-public endpoint requires `[Authorize]`. Audit this on every PR — it's easy to forget.

## Market Data

Market data is split across two providers. The interface split reflects this: `IPriceDataProvider` (Alpha Vantage) and `ICompanyDataProvider` (Finnhub). Both use typed `HttpClient`s via `IHttpClientFactory`. Never `new HttpClient()`.

### Finnhub (fundamentals + news)

- `FinnhubMarketDataProvider` implements `ICompanyDataProvider`.
- API key in user secrets locally, GCP Secret Manager in prod. Fail fast at startup if missing.
- Wrap every call in **Polly** policies: retry with exponential backoff on 5xx, circuit breaker on sustained failures.
- **Handle HTTP 429 explicitly** — back off, do not retry immediately. Free tier is 60 req/min, plenty for a 10-ticker nightly job but easy to trip during debugging.
- If Finnhub returns an error for one ticker mid-batch, continue with the rest. One bad ticker must not take down the whole job.
- **Terms of Service:** Finnhub's free tier permits this use case — check before redistributing data or going public.

### Alpha Vantage (prices)

- `AlphaVantagePriceProvider` implements `IPriceDataProvider`.
- API key appended as a query parameter via `AlphaVantageApiKeyHandler` (a `DelegatingHandler`).
- **Free tier: 25 req/day.** For 10 tickers that's 10 calls per nightly run — enough, but leaves little headroom for debugging. Trigger `PriceSyncJob` in isolation when testing (`Worker:Jobs:0=Prices`).
- **Rate limits are not signalled via HTTP 429.** Alpha Vantage returns HTTP 200 with an `"Information"` field in the body instead of price data. The provider detects this, logs a warning, and returns empty for that ticker — it does not throw.
- **Weekends and US market holidays return no new prices.** Treat "no new data" as a successful no-op.
- `outputsize=compact` returns the last 100 trading days — enough for the 14-day chart and moving averages without over-fetching.

## Claude API Integration

- API key stored in GCP Secret Manager (prod) or user secrets (local). Never in `appsettings.json`, never in code.
- Use `claude-sonnet-4-5` unless there's a specific reason to change.
- **Prompt structure for the analysis job:**
  1. System role: financial analyst writing a compact daily brief
  2. Injected data block: today's price action (with deltas vs moving averages and 52-week range), fundamentals snapshot, 2–3 news headlines with timestamps, one-sentence summary of yesterday's analysis for delta-awareness
  3. Output rules: 3 paragraphs max, no filler, direct institutional tone, structure (what happened / valuation view / what to watch)
- Prompt template is **versioned in the repo** (e.g. `prompts/analysis_v1.md`). Prompt changes deserve Git history.
- **Request structured output:** ask for JSON with `analysis` and `summary` fields in a single call. Saves a round-trip and the cost of a second call. Parse defensively; fall back to using the full text as summary if parsing fails.
- Gracefully handle API errors: log and keep the previous day's analysis. Do not write an empty or error-message analysis to the DB.
- Per-ticker, per-day unique constraint on the `analyses` table — nightly jobs upsert, not duplicate.
- Log input/output token counts per call. Surface running totals in `job_runs` for cost tracking.
- **Cost guard:** a monthly ceiling enforced in code. If the running month's token spend exceeds the threshold, skip analysis generation and log a loud ERROR. Cheap insurance against a prompt bug causing a billing spike.
- **Prompt injection via news headlines:** news content is user-untrusted data going into a prompt. Wrap news content in clearly-delimited blocks (e.g. `<news>...</news>`) and instruct the model to treat it as data, not instructions. Low risk for a demo but easy to get right.
- **Not financial advice disclaimer** must appear alongside every displayed analysis in the UI, not just on an About page.

## Nightly Jobs

**Execution model:**

- **Worker runs imperatively** — there is no in-process scheduler in either environment. In production, Cloud Scheduler triggers the Worker as a Cloud Run Job at 02:00 UTC; it runs the four jobs in sequence and exits. Locally, jobs are triggered on demand via the admin API endpoint.
- Do not add Hangfire or any other in-process scheduler to the Worker.

**Job order (strict):**

1. `PriceSyncJob` — fetches OHLCV for all tickers (last ~20 trading days, not just today — needed for the 14-day chart and moving averages)
2. `FundamentalsSyncJob` — fetches fundamentals snapshot per ticker
3. `NewsSyncJob` — fetches 3–5 latest headlines per ticker, deduplicated by article ID or URL
4. `AnalysisGenerationJob` — runs **last**, depends on fresh data from the first three

Each job:
- Has its own cancellation-aware execution method, respects the passed `CancellationToken`
- Logs `start`, `success`, `failure` with ticker context
- Records a row in the `job_runs` table with status, duration, ticker counts, error message
- Is **idempotent** — running twice in one day must not corrupt data. Upsert, don't insert blindly.
- Continues through per-ticker failures — one bad ticker does not fail the batch.

**Manual trigger (local):** run the Worker directly — `dotnet run --project src/Worker` or `docker compose run --rm worker`. The Worker runs the full job chain and exits. There is no in-API admin endpoint.

## Trading Logic

- Validate holdings before sell (no short-selling in demo).
- Validate cash balance before buy.
- Use the **most recent price in the DB** for trades — don't call external APIs on the trade path.
- Every trade writes a `transactions` row. Portfolio state — both stock holdings and cash balance — is always **derived from the transaction log**, never stored as a separate column or table. Cash balance is the sum of `Deposit` transactions minus the cost of all `Buy` transactions plus proceeds of all `Sell` transactions. Do not add a `CashBalance` column to `User` or a `holdings` table.

## Frontend (Blazor WASM)

- One typed `HttpClient` per API area (e.g. `StocksClient`, `PortfolioClient`, `AuthClient`) — not a single god-client.
- Handle loading, error, and empty states **explicitly** on every page. No silent failures.
- Charts: pick **one** library in the first chart task and use it everywhere. MudBlazor has built-in charts; ApexCharts via the Blazor-ApexCharts NuGet is nicer for candlesticks. Do not mix.
- The 14-day chart reads from a dedicated endpoint returning pre-shaped data. Do not transform raw OHLCV in the component.
- **Initial load is large.** Enable AOT compilation in Release builds — accept the longer build time.
- Auth state provider: tracks access token **in memory**, handles refresh automatically on 401, redirects to login on refresh failure.
- Message handler attaches `Authorization: Bearer` header to every API call.
- Show "data delayed ~15 minutes" and "AI-generated, not financial advice" prominently. Users must not think this is live trading.

## Docker

- Multi-stage builds. Final image runs on `mcr.microsoft.com/dotnet/aspnet:8.0` (or `runtime` for Worker), not `sdk`.
- Don't run as root in the final image.
- `.dockerignore` must exclude `bin/`, `obj/`, `.git/`, local secrets.

## GCP Deployment Shape

- **Compute:** Cloud Run for all services; Cloud SQL for Postgres.
- **Services:**
  - `Api` — Cloud Run service, `minInstances>=1`, `INGRESS_TRAFFIC_ALL`. Public HTTPS via Cloud Run domain mapping at `api.{domain}`.
  - `Web` (Blazor WASM static files) — Nginx container as its own Cloud Run service, `minInstances=0`. Public HTTPS via Cloud Run domain mapping at `app.{domain}`.
  - `Postgres` — **Cloud SQL for PostgreSQL** (`db-f1-micro`, 10 GB SSD). Private IP via VPC peering — not exposed to the internet. ~$9/month.
  - `Worker` — **not a Cloud Run service.** Triggered by Cloud Scheduler as a Cloud Run Job at 02:00 UTC. Runs the job chain and exits.
- **Networking:** Single VPC with one subnet. Api and Worker use Direct VPC Egress (`PRIVATE_RANGES_ONLY`) to reach the Cloud SQL private IP. No separate load balancer — Cloud Run domain mappings handle HTTPS termination for free.
- **HTTPS / custom domains:** Cloud Run domain mappings provide Google-managed TLS certificates at no extra cost. DNS records are CNAME → `ghs.googlehosted.com`. Prerequisite: verify domain ownership in Google Search Console before first `pulumi up`.
- **Auth cookies across subdomains:** The refresh token cookie must be set with `Domain=.{domain}` so it is sent from `app.{domain}` to `api.{domain}`. Both share the same eTLD+1, so `SameSite=Strict` is honoured.
- **Secrets:** GCP Secret Manager for JWT signing key, Anthropic API key, Finnhub API key, Alpha Vantage API key, and DB connection string. The connection string is written by Pulumi automatically (Cloud SQL private IP + password). Service accounts grant each service access to only its own secrets.
- **Logs:** Cloud Logging per service, retention 14 days (custom log bucket).
- **Billing alert** at $20/month. Non-negotiable — catches misconfigurations before they're expensive. Estimated cost: ~$15/month.
- **Infrastructure-as-code:** Pulumi in C# keeps the stack in the same language as the app. Do not provision GCP resources by hand in the console beyond the initial project bootstrap.

## CI/CD

- PR workflow: restore, build, test. No GCP access needed.
- Main branch workflow: build, test, build Docker images tagged with Git SHA + `latest`, push to Artifact Registry.
- Deploy workflow: run EF Core migrations as a **one-off Cloud Run Job** before updating the Api service. Do not run migrations from Api startup.
- GCP credentials via **Workload Identity Federation** (GitHub → GCP service account impersonation). No long-lived service account keys in GitHub Secrets.
- Manual approval gate before production deploy.
- Tag images with both Git SHA (immutable, for rollback) and `latest` (convenience).
- Keep a written rollback runbook — how to redeploy a previous image tag.

## Secrets & Configuration

- Local: .NET user secrets (`dotnet user-secrets`)
- Prod: GCP Secret Manager, loaded at container startup
- **Never** commit `appsettings.Production.json` with real values
- **Never** hardcode API keys, connection strings, or JWT signing keys

Secrets by service:
- **Api** — `Jwt:SigningKey` only. Connection string is in `appsettings.Development.json` for local dev; Secret Manager in prod.
- **Worker** — `Anthropic:ApiKey`, `Finnhub:ApiKey`, `AlphaVantage:ApiKey`. Connection string same as above.
- Api does **not** need and must not be given market data or Anthropic API keys.

## Git Conventions

- Do **not** include `Co-Authored-By: Claude` in commit messages.

## What Claude Should NOT Do

- Do not add LLM calls to the API request path. Analyses are pre-generated.
- Do not introduce new services or NuGet packages without flagging the tradeoff.
- Do not disable nullable reference types, warnings-as-errors, or test failures to "make it build."
- Do not store JWTs in `localStorage`.
- Do not use `double`/`float` for money.
- Do not write raw SQL when EF Core can express it — unless there's a clear performance reason, then comment why.
- Do not add real trading functionality, real money integration, or actual brokerage APIs. This is a virtual-currency demo.
- Do not generate placeholder "TODO" implementations and claim a task is complete — if a piece of logic isn't finished, say so.

## When in Doubt

- Prefer boring, well-documented patterns over clever ones.
- If a task's scope is ambiguous, ask before implementing.
- If you notice something broken adjacent to what you're working on, flag it rather than silently fixing it (or silently ignoring it).
