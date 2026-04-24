#!/usr/bin/env bash
set -euo pipefail

API_KEY=$(dotnet user-secrets get "Anthropic:ApiKey" --project src/Worker 2>/dev/null) || {
  echo "Error: Anthropic:ApiKey not found in user secrets."
  echo "Run: dotnet user-secrets set 'Anthropic:ApiKey' '<your-key>' --project src/Worker"
  exit 1
}

ANTHROPIC_API_KEY="$API_KEY" dotnet test tests/Worker.Tests \
  --filter "ClaudeAnalysisGeneratorIntegrationTests" \
  --logger "console;verbosity=normal"