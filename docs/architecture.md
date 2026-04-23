# Architecture

```mermaid
flowchart TD
    Browser(["Browser"])

    subgraph GCP["Google Cloud Platform"]
        LB["Cloud Load Balancing\npublic HTTPS"]

        subgraph svc["Cloud Run — Services"]
            Web["Web\nBlazor WASM · Nginx"]
            Api["Api\nASP.NET Core"]
        end

        DB[("PostgreSQL\nCloud Run · Filestore volume")]
        SM["Secret Manager"]

        Sched(["Cloud Scheduler\n02:00 UTC nightly"])

        subgraph job["Cloud Run Job"]
            Worker["Worker — .NET BackgroundService\n① PriceSyncJob\n② FundamentalsSyncJob\n③ NewsSyncJob\n④ AnalysisGenerationJob"]
        end
    end

    Finnhub["Finnhub\nMarket Data API"]
    Claude["Anthropic Claude API\nclaude-sonnet-4-20250514"]

    subgraph cicd["CI/CD — GitHub Actions + Workload Identity Federation"]
        GHA["build · test · push to Artifact Registry · deploy"]
    end

    Browser   -->|HTTPS|                LB
    LB        -->                       Web
    LB        -->                       Api
    Web       -->|"REST  ·  Bearer JWT"| Api
    Api       -->|read|                 DB
    Api       -. secrets .->           SM

    Sched     -->|triggers|             Worker
    Worker    -->|"①②③  price / fundamentals / news"| Finnhub
    Worker    -->|"④  analysis generation"|           Claude
    Worker    -->|write|                DB
    Worker    -. secrets .->           SM

    cicd      -->|deploy|              GCP
```

## Key design decisions

| Decision | Rationale |
|---|---|
| Claude API called only from Worker | Keeps LLM cost off the request path; analyses are pre-generated nightly |
| PostgreSQL on Cloud Run + Filestore | Single instance, no managed DB cost; `maxInstances=1` prevents split-brain |
| Worker as Cloud Run Job, not a service | Runs once and exits — no idle compute for a task that runs 10 min/day |
| JWT in memory / sessionStorage only | Prevents XSS token theft from localStorage |
| Migrations as one-off Cloud Run Job | Avoids race condition when multiple Api replicas start simultaneously |