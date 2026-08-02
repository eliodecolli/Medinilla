#!/usr/bin/env bash
# Wipes and reseeds the dev Postgres with realistic test data.
#
# Replaces the old bootstrap-main-account.sh + Medinilla.DataAccess/Relational/Seed.sql.
# Creates the 'MedinillaTest-Core' account (the one ChargingStationBooting looks
# up on first boot) plus a handful of 'noise' accounts with stations, connectors,
# tariffs, auth users, id tokens and transactions.
#
# Usage:
#   ./dev/seed.sh
#   ./dev/seed.sh my-postgres   # override container name
#
# Requires: docker, and the dev Postgres container running
#   (docker compose -f dev/docker-compose.yml up -d postgres).

set -euo pipefail

CONTAINER="${1:-medinilla-postgres}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_FILE="$SCRIPT_DIR/seed.sql"

if ! command -v docker >/dev/null 2>&1; then
    echo "docker is required but not found in PATH" >&2
    exit 1
fi

if ! docker container inspect "$CONTAINER" >/dev/null 2>&1; then
    echo "container '$CONTAINER' is not running. Start it with:" >&2
    echo "  docker compose -f dev/docker-compose.yml up -d postgres" >&2
    exit 1
fi

echo "Applying $SQL_FILE to $CONTAINER..."
docker exec -i "$CONTAINER" \
    psql -U medinilla -d medinilla -v ON_ERROR_STOP=1 < "$SQL_FILE"

echo
echo "Seed complete. 'MedinillaTest-Core' account is ready for new BootNotifications."
