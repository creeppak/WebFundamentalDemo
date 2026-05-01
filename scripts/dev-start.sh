#!/usr/bin/env bash
# Starts the full local stack: Postgres (Docker), Api, Worker, and Web.
# Usage: ./scripts/dev-start.sh
# Stop: Ctrl+C — kills all three services and stops the Postgres container.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# ── colours ──────────────────────────────────────────────────────────────────
R='\033[0m'; BOLD='\033[1m'
BLUE='\033[34m'; YELLOW='\033[33m'; GREEN='\033[32m'; CYAN='\033[36m'

log()  { printf "${BOLD}${CYAN}▶ %s${R}\n" "$*"; }
info() { printf "  %s\n" "$*"; }

# ── Postgres ──────────────────────────────────────────────────────────────────
log "Starting Postgres..."
docker compose up -d postgres

log "Waiting for Postgres to be ready..."
until docker compose exec -T postgres pg_isready -U postgres -q 2>/dev/null; do
    sleep 1
done
printf "  ${GREEN}Postgres ready${R}\n"

# ── Build ─────────────────────────────────────────────────────────────────────
log "Building all projects..."
dotnet build WebFundamentalDemo.sln

# ── .NET services ─────────────────────────────────────────────────────────────
log "Starting Api, Worker, and Web..."

# Each service gets a coloured label prepended to every log line.
label() {
    local color="$1" name="$2"; shift 2
    "$@" 2>&1 | while IFS= read -r line; do
        printf "${color}[%-6s]${R} %s\n" "$name" "$line"
    done
}

label "$BLUE"   "Api"    dotnet run --no-build --project src/Api    --launch-profile http &
PID_API=$!

label "$GREEN"  "Web"    dotnet run --no-build --project src/Web    --launch-profile http &
PID_WEB=$!

# ── Cleanup ───────────────────────────────────────────────────────────────────
cleanup() {
    printf "\n"
    log "Shutting down..."
    kill "$PID_API" "$PID_WEB" 2>/dev/null || true
    wait "$PID_API" "$PID_WEB" 2>/dev/null || true
    docker compose stop postgres
    log "Done."
}
trap cleanup EXIT INT TERM

printf "\n"
info "${BOLD}Api${R}    → http://localhost:5052/swagger"
info "${BOLD}Web${R}    → http://localhost:5081"
printf "\n"
info "Ctrl+C to stop all services"
printf "\n"

wait "$PID_API" "$PID_WEB"
