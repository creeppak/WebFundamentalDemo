# WebFundamentalDemo

A virtual stock trading demo platform. Users register, receive a virtual cash balance, and can buy/sell a curated set of stocks. Each stock page shows fundamentals, a 14-day price chart, and an AI-generated fundamental analysis.

> **Disclaimer:** This is a demo project using virtual currency. All AI-generated analyses are not financial advice.

## Architecture

| Service | Description |
|---|---|
| `src/Api` | ASP.NET Core REST API — auth, stock data, trading |
| `src/Worker` | .NET Background Service — nightly price/fundamentals/news sync and Claude analysis generation |
| `src/Web` | Blazor WebAssembly client |
| `src/Shared` | DTOs and shared contracts |

**Data flow:** Nightly Worker jobs → Finnhub + Alpha Vantage + Claude API → Cloud SQL (PostgreSQL) ← ASP.NET API ← Blazor client

## Tech Stack

- .NET 8 / C# (ASP.NET Core, Blazor WASM, Worker Service)
- PostgreSQL + EF Core (Npgsql) — Cloud SQL `db-f1-micro` in production, Docker in local dev
- Mapperly (compile-time DTO↔domain mapping)
- Docker / Docker Compose
- GCP Cloud Run + Artifact Registry + Cloud SQL + Secret Manager + Cloud Scheduler + Cloud Run domain mappings
- GitHub Actions (CI/CD, Workload Identity Federation auth to GCP)
- Anthropic Claude API (`claude-sonnet-4-5`)
- Finnhub (fundamentals, news)
- Alpha Vantage (daily OHLCV prices)

## Local Development

### Prerequisites

- .NET 8 SDK
- Docker

### Setup

```bash
# Start PostgreSQL
docker compose up -d postgres

# Apply migrations
dotnet ef database update --project src/Api

# Run services (separate terminals)
dotnet run --project src/Api
dotnet run --project src/Worker
dotnet run --project src/Web
```

**Full stack in Docker** (first-time setup):

```bash
# 1. Copy the secrets template and fill in real API keys
cp .env.example .env

# 2. Start Postgres and apply migrations
docker compose up -d postgres
dotnet ef database update --project src/Api

# 3. Build and start all services
docker compose up --build
```

The app is then available at:
- Web: http://localhost:5081
- Api: http://localhost:5052

Subsequent runs only need `docker compose up` (no `--build` unless code changed).

**Running Worker jobs manually** (Docker):

```bash
docker compose run --rm worker
```

The Worker is excluded from the default `docker compose up` because it runs the full job chain and exits — it is not a long-running service.

### Secrets (local)

Use .NET user secrets — never commit API keys:

```bash
dotnet user-secrets set "Jwt:SigningKey"        "<key>" --project src/Api
dotnet user-secrets set "Anthropic:ApiKey"     "<key>" --project src/Worker
dotnet user-secrets set "Finnhub:ApiKey"       "<key>" --project src/Worker
dotnet user-secrets set "AlphaVantage:ApiKey"  "<key>" --project src/Worker
```

### Migrations

```bash
dotnet ef migrations add <Name> --project src/Api --startup-project src/Api
dotnet ef database update --project src/Api
```

Always review generated migrations before committing.

## Testing

```bash
dotnet test
```

## Market Data

### Finnhub (fundamentals + news)

Fundamentals and news are fetched from [Finnhub](https://finnhub.io).

**Free-tier limits:** 60 API calls per minute.

| Nightly job | Calls per ticker | 10 tickers |
|---|---|---|
| `FundamentalsSyncJob` | 1 | 10 |
| `NewsSyncJob` | 1 | 10 |
| **Total** | **2** | **20** |

20 calls per nightly run is well within the free-tier limit. The limit is easy to trip during **manual debugging** — repeated job triggers will hit it quickly. The HTTP client handles 429 by waiting for the `Retry-After` duration and retrying once.

**Terms of Service:** Finnhub's free tier permits this use case. Do not redistribute raw data or make the app publicly available at scale without reviewing their ToS.

### Alpha Vantage (prices)

Daily OHLCV price history is fetched from [Alpha Vantage](https://www.alphavantage.co).

**Free-tier limits:** 25 API calls per day.

| Nightly job | Calls per ticker | 10 tickers |
|---|---|---|
| `PriceSyncJob` | 1 | 10 |

10 calls per nightly run leaves 15 calls for manual debugging. **Do not trigger `PriceSyncJob` repeatedly in one day** — there is no automatic recovery once the daily limit is exhausted. Use `Worker:Jobs:0=Prices` to run only that job when testing.

Alpha Vantage does not return HTTP 429 on rate limit — it returns HTTP 200 with an `"Information"` field in the body. The provider detects this and skips the ticker gracefully rather than crashing.

## Deployment

Deployed to GCP. See `infra/` for Pulumi stack (C#). Estimated cost: ~$15/month.

- **Api** — Cloud Run service, public HTTPS via Cloud Run domain mapping (`api.{domain}`)
- **Web** — Nginx container serving Blazor WASM static files, Cloud Run domain mapping (`app.{domain}`)
- **Postgres** — Cloud SQL `db-f1-micro`, private IP via VPC peering (no public exposure)
- **Worker** — Cloud Scheduler triggers a Cloud Run Job at 02:00 UTC nightly

CI/CD via GitHub Actions (Workload Identity Federation, no long-lived keys). Migrations run as a one-off Cloud Run Job before Api deploy.

## License

MIT