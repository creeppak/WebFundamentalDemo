#!/usr/bin/env bash
# Bootstrap script — run once before `pulumi up`.
# Sets up the GCP project: enables required APIs and creates a $20/month billing alert.
#
# Prerequisites:
#   gcloud auth login
#   gcloud config set project <PROJECT_ID>
#
# Usage:
#   BILLING_ACCOUNT=<BILLING_ACCOUNT_ID> ./infra/bootstrap.sh
#
# Find your billing account ID:
#   gcloud billing accounts list

set -euo pipefail

PROJECT_ID=$(gcloud config get-value project 2>/dev/null)
if [ -z "$PROJECT_ID" ]; then
    echo "ERROR: no active gcloud project. Run: gcloud config set project <PROJECT_ID>"
    exit 1
fi

BILLING_ACCOUNT="${BILLING_ACCOUNT:-}"
if [ -z "$BILLING_ACCOUNT" ]; then
    echo "ERROR: BILLING_ACCOUNT is required. Export it before running this script:"
    echo "  export BILLING_ACCOUNT=\$(gcloud billing accounts list --format='value(name)' | head -1)"
    exit 1
fi

PROJECT_NUMBER=$(gcloud projects describe "$PROJECT_ID" --format="value(projectNumber)")

echo "==> Project: $PROJECT_ID ($PROJECT_NUMBER)"
echo "==> Billing account: $BILLING_ACCOUNT"

# Link billing account to project (no-op if already linked).
echo "==> Linking billing account..."
gcloud billing projects link "$PROJECT_ID" --billing-account="$BILLING_ACCOUNT"

# Enable all required APIs in one call.
echo "==> Enabling APIs..."
gcloud services enable \
    run.googleapis.com \
    artifactregistry.googleapis.com \
    secretmanager.googleapis.com \
    file.googleapis.com \
    cloudscheduler.googleapis.com \
    compute.googleapis.com \
    dns.googleapis.com \
    vpcaccess.googleapis.com \
    iam.googleapis.com \
    iamcredentials.googleapis.com \
    billingbudgets.googleapis.com \
    --project="$PROJECT_ID"

# Create a $20/month billing alert with notifications at 50% and 100%.
# Idempotency: gcloud will error if a budget with this name already exists;
# re-running the script safely skips this block.
# Make sure the correct currency type is used below.
echo "==> Creating billing alert..."
if gcloud billing budgets list \
        --billing-account="$BILLING_ACCOUNT" \
        --format="value(displayName)" \
    | grep -q "webfundamentaldemo-20-monthly"; then
    echo "    Budget already exists, skipping."
else
    gcloud billing budgets create \
        --billing-account="$BILLING_ACCOUNT" \
        --display-name="webfundamentaldemo-20-monthly" \
        --budget-amount=75PLN \
        --threshold-rule=percent=0.5 \
        --threshold-rule=percent=1.0 \
        --filter-projects="projects/$PROJECT_ID"
    echo "    Budget created."
fi

echo ""
echo "Bootstrap complete. Next: run 'pulumi up' from infra/."
