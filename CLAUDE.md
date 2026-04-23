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
- **PostgreSQL** + EF Core (Npgsql provider) — containerized, single Cloud Run instance with Filestore-mounted volume for data persistence
- **Hangfire** for job scheduling (Postgres-backed storage, isolated schema)
- **Mapperly** for compile-time DTO↔domain object mapping
- Docker + Docker Compose for local dev
- **GCP Cloud Run** deployment (Cloud Run, Artifact Registry, Cloud Load Balancing, Secret Manager, Filestore for Postgres data, Cloud Scheduler for Worker job)
- GitHub Actions for CI/CD (Workload Identity Federation auth to GCP)
- **Anthropic Claude API** for analysis generation (`claude-sonnet-4-20250514`)
- **Finnhub** for market data (fundamentals, prices, news)

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
- **Hangfire uses a separate Postgres schema** (`hangfire`) to keep its tables out of EF Core migrations.
- **Migration ownership:** the `Api` project owns migrations. The `Worker` reads and writes data but does not run migrations.
- **Do not run migrations from Api startup in production.** Run them as a one-off Cloud Run Job before deploying a new Api revision. Multiple Api instances racing on startup corrupts migration history.
- **Postgres runs in a single Cloud Run instance with Filestore-mounted data directory.** This means only one Postgres instance may be running at a time — the Cloud Run service must be configured with `minInstances=1`, `maxInstances=1` (no concurrent revisions that would briefly run two). Deployments take Postgres offline for ~30s.
- Filestore has higher latency than local disk. Acceptable for demo scale; do not benchmark this against Cloud SQL.

## Authentication

- ASP.NET Identity for user management, JWT for API auth.
- JWTs signed with a key from GCP Secret Manager in prod, user secrets locally.
- **Never** store JWTs in `localStorage` in Blazor WASM — use `sessionStorage` or in-memory with a refresh token flow.
- Access tokens short-lived (15 min), refresh tokens longer (7 days), rotated on use.
- Every non-public endpoint requires `[Authorize]`. Audit this on every PR — it's easy to forget.

## Market Data (Finnhub)

- One `IMarketDataProvider` interface, `FinnhubMarketDataProvider` as the concrete implementation. Keep the interface provider-agnostic so swapping providers is possible.
- API key in user secrets locally, GCP Secret Manager in prod. Fail fast at startup if missing.
- Use `IHttpClientFactory` with a named/typed client for Finnhub. Never `new HttpClient()`.
- Wrap every Finnhub call in **Polly** policies: retry with exponential backoff on 5xx + timeouts, circuit breaker on sustained failures.
- **Handle HTTP 429 (rate limit) explicitly** — back off, do not retry immediately. Finnhub's free tier is 60 req/min, which is plenty for a 10-ticker nightly job but easy to trip during debugging.
- **Weekends and US market holidays return no new prices.** The job must treat "no new data since yesterday" as a successful no-op, not a failure.
- If Finnhub returns an error for one ticker mid-batch, continue with the rest. One bad ticker must not take down the whole job.
- Document Finnhub's free-tier limits in the README. Know the limits before you debug against the real API.
- **Terms of Service:** Finnhub's free tier permits this use case — check before ever redistributing data or going public.

## Claude API Integration

- API key stored in GCP Secret Manager (prod) or user secrets (local). Never in `appsettings.json`, never in code.
- Use `claude-sonnet-4-20250514` unless there's a specific reason to change.
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

## Nightly Jobs (Hangfire)

**Hangfire configuration:**

- Hangfire storage in the same Postgres instance, **isolated schema** (`hangfire`) — keeps its tables out of EF Core migrations.
- Hangfire dashboard enabled in Development, protected by basic auth or disabled entirely in Production.
- Jobs registered as recurring jobs via `RecurringJob.AddOrUpdate(...)` with cron expressions.
- Scheduled run time: **02:00 UTC** — after US market close + news cycle settles. Document any change.

**Execution model on GCP:**

- **Worker runs as a Cloud Scheduler-triggered Cloud Run Job**, not an always-on service. The job starts at 02:00 UTC, runs the job chain, exits. Cheaper and more correct than keeping a process idle 23 hours a day.
- Because the Worker is not always running, Hangfire's recurring-job scheduling is used only locally. In production, Cloud Scheduler triggers the Cloud Run Job and it runs the jobs imperatively on startup, then shuts down.
- Local dev: Hangfire runs as an always-on service inside the Worker container, scheduling itself.

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

**Manual trigger:** admin-only API endpoint to run a specific job on demand. Critical for debugging and first deploy — do not skip this.

## Trading Logic

- Validate holdings before sell (no short-selling in demo).
- Validate cash balance before buy.
- Use the **most recent price in the DB** for trades — don't call external APIs on the trade path.
- Every trade writes a `transactions` row. Portfolio state is derived from transactions, not stored as mutable balances (or, if stored, kept in sync inside a DB transaction).

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

- **Compute:** Cloud Run for all services.
- **Services:**
  - `Api` — Cloud Run service, `minInstances>=1`, behind Cloud Load Balancing, public HTTPS.
  - `Web` (Blazor WASM static files) — served from a minimal Nginx container as its own Cloud Run service behind the load balancer. (Alternative: Cloud Storage + Cloud CDN for cheaper static hosting — defer this choice, but know it's an option.)
  - `Postgres` — Cloud Run service, `minInstances=1`, `maxInstances=1`, Filestore-mounted data directory. **No rolling updates.** Deployment briefly takes Postgres offline.
  - `Worker` — **not a Cloud Run service.** Triggered by Cloud Scheduler as a Cloud Run Job at 02:00 UTC. Runs the job chain and exits.
- **Networking:** VPC with public and private subnets. Load balancer in public subnet, all Cloud Run services in private subnet via VPC connector. Postgres firewall rules allow traffic only from Api and Worker service accounts.
- **Secrets:** GCP Secret Manager for DB password, JWT signing key, Anthropic API key, Finnhub API key. Service accounts grant each service access to only its own secrets.
- **Logs:** Cloud Logging per service, retention 7–14 days (demo cost).
- **Billing alert** at $20/month. Non-negotiable — catches misconfigurations before they're expensive.
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
