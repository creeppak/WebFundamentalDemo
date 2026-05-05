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
    sqladmin.googleapis.com \
    servicenetworking.googleapis.com \
    cloudscheduler.googleapis.com \
    compute.googleapis.com \
    dns.googleapis.com \
    iam.googleapis.com \
    iamcredentials.googleapis.com \
    sts.googleapis.com \
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

# ── Workload Identity Federation (GitHub Actions) ────────────────────────────
#
# Lets GitHub Actions authenticate to GCP without long-lived service account keys.
# Scoped to this repo only via the attribute condition.

GITHUB_REPO="creeppak/WebFundamentalDemo"
WIF_POOL_ID="github-actions"
WIF_PROVIDER_ID="github"
DEPLOYER_SA_ID="webfundamentaldemo-deployer"
DEPLOYER_SA_EMAIL="${DEPLOYER_SA_ID}@${PROJECT_ID}.iam.gserviceaccount.com"

echo "==> Setting up Workload Identity Federation..."

if ! gcloud iam workload-identity-pools describe "$WIF_POOL_ID" \
        --project="$PROJECT_ID" --location="global" &>/dev/null; then
    gcloud iam workload-identity-pools create "$WIF_POOL_ID" \
        --project="$PROJECT_ID" \
        --location="global" \
        --display-name="GitHub Actions"
    echo "    WIF pool created."
else
    echo "    WIF pool already exists, skipping."
fi

if ! gcloud iam workload-identity-pools providers describe "$WIF_PROVIDER_ID" \
        --project="$PROJECT_ID" --location="global" \
        --workload-identity-pool="$WIF_POOL_ID" &>/dev/null; then
    gcloud iam workload-identity-pools providers create-oidc "$WIF_PROVIDER_ID" \
        --project="$PROJECT_ID" \
        --location="global" \
        --workload-identity-pool="$WIF_POOL_ID" \
        --display-name="GitHub" \
        --issuer-uri="https://token.actions.githubusercontent.com" \
        --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.actor=assertion.actor" \
        --attribute-condition="assertion.repository=='${GITHUB_REPO}'"
    echo "    WIF OIDC provider created."
else
    echo "    WIF OIDC provider already exists, skipping."
fi

if ! gcloud iam service-accounts describe "$DEPLOYER_SA_EMAIL" \
        --project="$PROJECT_ID" &>/dev/null; then
    gcloud iam service-accounts create "$DEPLOYER_SA_ID" \
        --project="$PROJECT_ID" \
        --display-name="WebFundamentalDemo GitHub Actions Deployer"
    echo "    Deployer service account created."
else
    echo "    Deployer service account already exists, skipping."
fi

echo "==> Granting deployer permissions..."
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:$DEPLOYER_SA_EMAIL" \
    --role="roles/artifactregistry.writer" \
    --condition=None

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:$DEPLOYER_SA_EMAIL" \
    --role="roles/run.developer" \
    --condition=None

WIF_POOL_NAME="projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${WIF_POOL_ID}"

echo "==> Binding WIF pool to deployer service account..."
gcloud iam service-accounts add-iam-policy-binding "$DEPLOYER_SA_EMAIL" \
    --project="$PROJECT_ID" \
    --role="roles/iam.workloadIdentityUser" \
    --member="principalSet://iam.googleapis.com/${WIF_POOL_NAME}/attribute.repository/${GITHUB_REPO}"

echo ""
echo "Add the following as GitHub Actions secrets/variables (repo settings → Secrets and variables):"
echo ""
echo "  Secret  GCP_WIF_PROVIDER  =  ${WIF_POOL_NAME}/providers/${WIF_PROVIDER_ID}"
echo "  Secret  GCP_DEPLOYER_SA   =  ${DEPLOYER_SA_EMAIL}"
echo "  Variable GCP_PROJECT      =  ${PROJECT_ID}"
echo ""
echo "Then create a 'production' environment in repo settings and add required reviewers"
echo "to enable the manual approval gate on the deploy workflow."

echo ""
echo "Bootstrap complete. Next: run 'pulumi up' from infra/."
