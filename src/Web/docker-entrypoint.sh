#!/bin/sh
set -e

# Inject API_BASE_URL into appsettings.json at container start so the
# Blazor WASM app knows where to reach the API without a rebuild.
if [ -n "$API_BASE_URL" ]; then
    sed -i "s|https://api.example.com|$API_BASE_URL|g" \
        /usr/share/nginx/html/appsettings.json
fi

exec "$@"