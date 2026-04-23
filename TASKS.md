# Virtual Stock Trading Platform — Task List

Stack locked: **.NET 8**, ASP.NET Core REST API + Worker Service, Blazor WebAssembly, PostgreSQL (containerized on Fargate with EFS), EF Core, Hangfire, Finnhub, Anthropic Claude API (`claude-sonnet-4-20250514`), AWS Fargate + ALB + EventBridge Scheduler, AWS CDK in C# for IaC, Docker, GitHub Actions with OIDC.

---

## Phase 1 — Solution Scaffolding

- [ ] Create solution with projects: `src/Api`, `src/Worker`, `src/Web` (Blazor WASM), `src/Shared`, `src/Infrastructure` (CDK), `tests/Api.Tests`, `tests/Worker.Tests`, `tests/Shared.Tests`.
- [ ] Enable nullable reference types and `TreatWarningsAsErrors` across all projects via `Directory.Build.props`.
- [ ] Set up centralized NuGet version management via `Directory.Packages.props`.
- [ ] Add `.editorconfig` with .NET formatting + naming conventions enforced at build time.
- [ ] Add `.gitignore` (Visual Studio + Node templates merged) and `.dockerignore`.
- [ ] Initialize `dotnet user-secrets` for `Api` and `Worker` projects.
- [ ] Commit the CLAUDE.md written previously.
- [ ] Draw a one-page architecture diagram, commit it to `docs/architecture.md`.

---

## Phase 2 — Data Model & Database Foundation

- [ ] Design ERD on paper: `users`, `stocks`, `prices` (OHLCV per ticker per day), `fundamentals`, `news_items`, `analyses`, `transactions`, `job_runs`, `refresh_tokens`.
- [ ] Decide holdings representation — derived from `transactions` (simpler, cleaner) vs denormalized `holdings` table. **Recommend derived** for a demo; add a materialized projection later if reads get slow.
- [ ] Create `AppDbContext` in `src/Infrastructure/Data` (shared between Api and Worker via project reference).
- [ ] Define entity classes with explicit configuration via `IEntityTypeConfiguration<T>` — avoid data annotations on entities.
- [ ] Configure monetary fields as `decimal(18,4)`. Document this in a review checklist.
- [ ] Add unique constraints: `(ticker, date)` on prices/fundamentals/analyses.
- [ ] Add indexes: `(ticker, date DESC)` on prices/fundamentals/analyses, `(user_id, created_at DESC)` on transactions.
- [ ] Explicitly configure foreign key cascade rules — do not accept EF Core defaults silently.
- [ ] Set up Hangfire Postgres storage in a separate schema (`hangfire`) so its tables stay out of EF migrations.
- [ ] Create initial EF migration. Review generated SQL before committing.
- [ ] Write a development-only ticker seeder (runs on Api startup in Development), seed 5–10 tickers.
- [ ] Add `/health` and `/health/db` endpoints to Api.

---

## Phase 3 — Authentication & User Management

- [ ] Add ASP.NET Identity with `IdentityDbContext<AppUser>`, minimal config — no email confirmation, no roles unless needed.
- [ ] Extend `AppUser` with `VirtualCashBalance` (decimal) and `CreatedAt`.
- [ ] Implement JWT access token issuance (15 min TTL) on login.
- [ ] Implement refresh token flow: refresh tokens stored hashed in `refresh_tokens` table with expiry and revocation flag, rotated on each refresh.
- [ ] Endpoints: `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`.
- [ ] Fail fast on startup if JWT signing key is missing from configuration.
- [ ] Set initial virtual cash balance ($10,000) at user creation.
- [ ] Add rate limiting to `/auth/login` and `/auth/register` (ASP.NET Core's built-in `RateLimiter` middleware).
- [ ] Configure CORS: allow credentials from the Blazor origin only. No wildcards.
- [ ] **Decide:** refresh token transport. **Recommend** `HttpOnly Secure SameSite=Strict` cookie for refresh token + access token in memory on the client. Alternative: both in memory with no persistence across refreshes (simpler, logs user out on every page reload — acceptable for a demo).
- [ ] Add a test asserting every non-auth controller has `[Authorize]` — runs at build/CI time, prevents accidental exposure.
- [ ] Add an authorization policy ensuring `user_id` claim matches resource ownership (portfolio, transactions).

---

## Phase 4 — Finnhub Market Data Integration

- [ ] Define `IMarketDataProvider` interface: `GetPricesAsync`, `GetFundamentalsAsync`, `GetNewsAsync`. Keep DTOs provider-agnostic.
- [ ] Implement `FinnhubMarketDataProvider` using `IHttpClientFactory`-registered typed client.
- [ ] Configure Polly policies: retry with exponential backoff on 5xx + timeouts, circuit breaker on sustained failures.
- [ ] Handle HTTP 429 explicitly — back off, do not retry immediately.
- [ ] Store Finnhub API key in user secrets (local) / Secrets Manager (prod). Fail fast on startup if missing.
- [ ] Write unit tests with mocked `HttpMessageHandler` for success, 429, 5xx, and malformed-response cases.
- [ ] Document Finnhub's free-tier limits (60 req/min) in the README.
- [ ] **Flag in code:** weekends and US market holidays return empty price data — treat as no-op success, not failure.

---

## Phase 5 — Claude API Integration

- [ ] Add `IAnalysisGenerator` service in `src/Worker` with typed `HttpClient` for `api.anthropic.com`.
- [ ] Store Anthropic API key in user secrets / Secrets Manager. Fail fast on startup if missing.
- [ ] Create versioned prompt template file: `src/Worker/Prompts/analysis_v1.md`. Commit prompt changes with code changes.
- [ ] Prompt inputs: ticker, company name, today's date, price action (with deltas vs 50-day MA and 52-week range), fundamentals snapshot, 2–3 news headlines wrapped in `<news>` delimiters (prompt injection defense), one-sentence summary of yesterday's analysis.
- [ ] Request structured JSON output with `analysis` and `summary` fields — single API call.
- [ ] Parse JSON defensively. If parsing fails, fall back to using full text as summary.
- [ ] Log input/output token counts per call. Aggregate into `job_runs` for cost tracking.
- [ ] Implement monthly cost ceiling: read month-to-date token spend from `job_runs` before each call; abort + loud ERROR log if exceeded.
- [ ] On API failure (non-success status, timeout, malformed response), keep yesterday's analysis. Never write an empty or error-message analysis.
- [ ] Integration test gated behind an environment flag that calls the real API with a tiny prompt — off by default in CI.

---

## Phase 6 — Nightly Jobs (Hangfire)

- [ ] Configure Hangfire with Postgres storage in the `hangfire` schema.
- [ ] Hangfire dashboard enabled in Development only (or protected with basic auth in prod).
- [ ] Implement `PriceSyncJob` — fetches last ~20 trading days per ticker, upserts `prices` table.
- [ ] Implement `FundamentalsSyncJob` — fetches + upserts latest fundamentals snapshot per ticker.
- [ ] Implement `NewsSyncJob` — fetches 3–5 latest headlines per ticker, deduplicates by article ID.
- [ ] Implement `AnalysisGenerationJob` — runs last, reads fresh data from the previous three jobs, calls Claude, upserts `analyses`.
- [ ] Every job: idempotent (upsert not insert), continues through per-ticker failures, writes a row to `job_runs` with status/duration/counts/error.
- [ ] Every job: accepts and respects a `CancellationToken`.
- [ ] Local dev: run all four as Hangfire recurring jobs at 02:00 UTC.
- [ ] Production: jobs chain via orchestrator entry point that runs all four sequentially then exits (Worker is EventBridge-triggered, not always-on).
- [ ] Add admin-only API endpoint to trigger any job on demand — critical for debugging and first deploy.

---

## Phase 7 — REST API

- [ ] `GET /api/stocks` — list all tickers with latest price + day change.
- [ ] `GET /api/stocks/{ticker}` — fundamentals + latest analysis + latest price.
- [ ] `GET /api/stocks/{ticker}/history?days=14` — pre-shaped close + volume for chart. **Recommend close + volume over OHLCV** — simpler chart, same insight for a demo.
- [ ] `GET /api/stocks/{ticker}/news` — recent news for the ticker.
- [ ] `GET /api/portfolio` — current user's holdings (computed from transactions), cash balance, unrealized P&L.
- [ ] `GET /api/portfolio/transactions` — transaction history.
- [ ] `POST /api/portfolio/buy` — validate balance, write transaction inside `IDbContextTransaction`, return updated state.
- [ ] `POST /api/portfolio/sell` — validate holdings, write transaction inside `IDbContextTransaction`.
- [ ] All request DTOs validated with FluentValidation.
- [ ] Object-to-DTO mapping via hand-written extension methods. **No AutoMapper, no MediatR.**
- [ ] Return `ProblemDetails` for errors, not plain strings or anonymous objects.
- [ ] Swagger/OpenAPI enabled in Development only.
- [ ] Serilog configured: console in Development, CloudWatch in Production. Correlation IDs propagated via middleware.

---

## Phase 8 — Blazor WASM Frontend

- [ ] Add **MudBlazor** for component library (charts included — keeps the chart library decision simple).
- [ ] Configure `HttpClient` base URL from config; one typed client per API area: `StocksClient`, `PortfolioClient`, `AuthClient`.
- [ ] Implement `AuthenticationStateProvider`: access token in memory, auto-refresh on 401, redirect to login on refresh failure.
- [ ] Implement `DelegatingHandler` that attaches `Authorization: Bearer` to all API calls.
- [ ] Pages: Register, Login, Stock List, Stock Detail, Portfolio, Transaction History.
- [ ] Stock Detail page: 14-day chart (MudBlazor line chart, close price), fundamentals table, AI analysis panel, buy/sell component, news list.
- [ ] Explicit loading/error/empty states on every page.
- [ ] Buy/sell confirmation modal shows quantity × price = total vs available balance.
- [ ] Portfolio page shows unrealized P&L with color coding.
- [ ] Prominent disclaimers: "Market data delayed ~15 minutes" and "AI-generated, not financial advice" — next to every analysis, not just on an About page.
- [ ] Enable AOT compilation in Release builds.

---

## Phase 9 — Docker & Local Compose

- [ ] Multi-stage `Dockerfile` for Api (build on `sdk:8.0`, run on `aspnet:8.0` as non-root).
- [ ] Multi-stage `Dockerfile` for Worker (run on `runtime:8.0` — smaller than aspnet).
- [ ] Multi-stage `Dockerfile` for Web — build produces static files, served by a minimal Nginx container.
- [ ] `docker-compose.yml`: Api, Worker, Web, Postgres, with healthchecks and Postgres volume.
- [ ] Compose overrides: `docker-compose.override.yml` for dev-only settings (hot reload, exposed ports).
- [ ] README section documenting local run flow: first-time setup, hot reload, running migrations.

---

## Phase 10 — AWS Infrastructure (CDK in C#)

- [ ] Bootstrap AWS account: enable MFA on root, create IAM admin user for daily use, enable CloudTrail, set billing alarm at $20/month.
- [ ] Set up `src/Infrastructure` CDK project in C#.
- [ ] Stack 1 — **Network**: VPC with public + private subnets across two AZs, NAT gateway (needed for Fargate tasks to reach Finnhub/Anthropic).
- [ ] Stack 2 — **Storage**: ECR repositories per service, EFS filesystem for Postgres data, Secrets Manager entries (DB password, JWT signing key, Anthropic key, Finnhub key) with random initial values to be rotated manually.
- [ ] Stack 3 — **Compute**: ECS cluster, task definitions for Api/Worker/Web/Postgres, ECS services for Api/Web/Postgres, task role per service with scoped Secrets Manager access.
- [ ] **Postgres ECS service**: `desiredCount=1`, `maximumPercent=100`, `minimumHealthyPercent=0` — no rolling updates (two Postgres instances on the same EFS would corrupt data).
- [ ] Stack 4 — **Ingress**: ALB with HTTPS listener, ACM certificate, target groups for Api and Web, Route 53 records.
- [ ] Stack 5 — **Scheduler**: EventBridge Scheduler rule triggering `RunTask` for Worker at 02:00 UTC.
- [ ] CloudWatch log groups per service, 7-day retention.
- [ ] Document `cdk deploy` flow in README.

---

## Phase 11 — CI/CD (GitHub Actions with OIDC)

- [ ] Configure GitHub OIDC provider in AWS, create IAM role trusted by the OIDC provider scoped to the repo.
- [ ] Workflow: **PR** — restore, build, run all tests. No AWS access.
- [ ] Workflow: **Main build** — restore, build, test, build + push Docker images tagged with Git SHA + `latest` to ECR.
- [ ] Workflow: **Deploy** — manual approval gate, then:
    1. Run EF migrations as a one-off ECS task using the new image tag
    2. Update Api service task definition to new image
    3. Update Web service task definition to new image
    4. Update Worker task definition (next EventBridge trigger picks it up)
- [ ] **Do not** run migrations from Api startup in production.
- [ ] Write rollback runbook: how to redeploy a prior Git SHA tag.
- [ ] Do not store long-lived AWS access keys in GitHub Secrets — OIDC only.

---

## Phase 12 — Observability

- [ ] Serilog sinks: console in Dev, CloudWatch in Prod. Correlation IDs on every log line.
- [ ] Save CloudWatch Insights queries for: nightly job success/failure, Claude token usage by day, auth failure rate, Api 5xx rate.
- [ ] Alarm: no successful `AnalysisGenerationJob` run in 25 hours.
- [ ] Alarm: Api 5xx rate above threshold over 5 minutes.
- [ ] Alarm: monthly Claude cost ceiling approached (80% threshold).

---

## Phase 13 — Demo Polish

- [ ] Seed 2–3 demo user accounts with varied portfolios (visible on the login page for quick demo access).
- [ ] "About this demo" page: virtual money, ~15 min data delay, AI-generated analysis, not financial advice.
- [ ] Run full stack against real Finnhub data for at least one full week before calling it done — catches weekend/holiday/earnings-day edge cases.
- [ ] Final README: architecture diagram, local dev instructions, environment variables, deploy process, known limitations, cost estimate.

---

## Items Worth Flagging That Aren't Otherwise Obvious

- [ ] **Timezones**: DB in UTC, Finnhub in US/Eastern implicitly, users anywhere. Be explicit at every boundary, especially in the nightly job and chart date labels.
- [ ] **Postgres deployment downtime**: because the Postgres ECS service can't do rolling updates, every deploy of the Postgres task (rare, but happens on config changes) takes the app offline briefly. Plan deploys accordingly.
- [ ] **Concurrent trade safety**: two browser tabs submitting buys simultaneously against the same balance. Add a transaction test case for this — it's a classic bug.
- [ ] **Finnhub ToS**: confirm the free tier permits the demo's use case. Note in README.
- [ ] **Prompt injection via news headlines**: news content goes into Claude's prompt wrapped in `<news>` delimiters, with explicit instruction to treat contents as untrusted data.
- [ ] **Data retention for analyses**: storing every day's analysis forever grows linearly. Decide whether to prune old ones (e.g. 90 days) — defer for now, but track as a known future task.
- [ ] **Cold start on Worker task**: EventBridge-triggered Fargate tasks take 30–60s to start. Document this so the first minute of the nightly job isn't mistaken for a hang.
