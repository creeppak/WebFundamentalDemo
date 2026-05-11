#!/usr/bin/env bash
# Manually triggers the Worker Cloud Run Job and tails the execution logs.
# Usage: ./scripts/trigger-worker.sh [--project PROJECT_ID]
# Requires: gcloud CLI, authenticated with sufficient permissions.
set -euo pipefail

JOB="webfundamentaldemo-worker"
REGION="europe-west1"
PROJECT="${GCP_PROJECT:-}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --project) PROJECT="$2"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

if [[ -z "$PROJECT" ]]; then
    PROJECT="$(gcloud config get-value project 2>/dev/null)"
fi

if [[ -z "$PROJECT" ]]; then
    echo "Error: no GCP project set. Pass --project PROJECT_ID or run 'gcloud config set project PROJECT_ID'." >&2
    exit 1
fi

echo "Triggering Cloud Run Job: $JOB (project=$PROJECT, region=$REGION)"

EXECUTION=$(gcloud run jobs execute "$JOB" \
    --region="$REGION" \
    --project="$PROJECT" \
    --format="value(metadata.name)" \
    --wait 2>&1 | tail -1)

echo "Execution: $EXECUTION"
echo ""
echo "Streaming logs (Ctrl+C to stop following — job continues running):"
echo ""

gcloud logging read \
    "resource.type=cloud_run_job AND resource.labels.job_name=$JOB AND labels.\"run.googleapis.com/execution_name\"=$EXECUTION" \
    --project="$PROJECT" \
    --format="value(timestamp, textPayload)" \
    --order=asc \
    --freshness=1h
