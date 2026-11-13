#!/usr/bin/env bash
# Runs a SQL file from this directory against the dev Postgres container.
# Inspect the selected SQL file first: scripts such as seed.sql delete data.
#
# Usage (from any working directory):
#   /path/to/dev/run-sql.sh bootstrap-account-settings.sql
#   /path/to/dev/run-sql.sh bootstrap-account-settings.sql my-postgres

set -euo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
    echo "Usage: $0 <filename.sql> [container-name]" >&2
    exit 1
fi

SQL_NAME="$1"
CONTAINER="${2:-medinilla-postgres}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "$SQL_NAME" == */* || "$SQL_NAME" != *.sql ]]; then
    echo "Provide a .sql filename from $SCRIPT_DIR, not a path." >&2
    exit 1
fi

SQL_FILE="$SCRIPT_DIR/$SQL_NAME"
if [[ ! -f "$SQL_FILE" || ! -r "$SQL_FILE" ]]; then
    echo "SQL file does not exist or is not readable: $SQL_FILE" >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "docker is required but not found in PATH" >&2
    exit 1
fi

if [[ "$(docker container inspect --format '{{.State.Running}}' "$CONTAINER" 2>/dev/null || true)" != "true" ]]; then
    echo "Container '$CONTAINER' is not running. Start the dev Postgres with:" >&2
    echo "  docker compose -f \"$SCRIPT_DIR/docker-compose.yml\" up -d postgres" >&2
    exit 1
fi

echo "Running $SQL_FILE against database 'medinilla' in $CONTAINER..."
docker exec -i "$CONTAINER" \
    psql -X -U medinilla -d medinilla -v ON_ERROR_STOP=1 < "$SQL_FILE"

echo "SQL script completed successfully."
