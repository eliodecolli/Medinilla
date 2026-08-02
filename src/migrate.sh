#!/usr/bin/env bash
set -euo pipefail

PROJECT="Medinilla.DataAccess/Medinilla.DataAccess.csproj"
STARTUP="Medinilla.DataAccess/Medinilla.DataAccess.csproj"
CONTEXT="MedinillaOcppDbContext"

usage() {
    cat <<EOF
Usage: $0 <command> [args]

Commands:
  add <name>        Create a new migration with the given name
  apply             Apply pending migrations to the database
  remove            Remove the last (not yet applied) migration
  list              List all migrations and their applied status
  script [from] [to]
                    Generate a SQL script; defaults to all pending migrations
  help              Show this message

Notes:
  Run \`dotnet tool restore\` once before the first use to install dotnet-ef.
  Working directory must be the folder containing this script.
EOF
}

cmd="${1:-help}"
shift || true

case "$cmd" in
    add)
        name="${1:?Migration name required. Usage: $0 add <Name>}"
        dotnet ef migrations add "$name" \
            --project "$PROJECT" \
            --startup-project "$STARTUP" \
            --context "$CONTEXT"
        ;;
    apply)
        dotnet ef database update \
            --project "$PROJECT" \
            --startup-project "$STARTUP" \
            --context "$CONTEXT"
        ;;
    remove)
        dotnet ef migrations remove \
            --project "$PROJECT" \
            --startup-project "$STARTUP" \
            --context "$CONTEXT"
        ;;
    list)
        dotnet ef migrations list \
            --project "$PROJECT" \
            --startup-project "$STARTUP" \
            --context "$CONTEXT"
        ;;
    script)
        case "$#" in
            0)
                dotnet ef migrations script \
                    --project "$PROJECT" --startup-project "$STARTUP" --context "$CONTEXT"
                ;;
            1)
                dotnet ef migrations script "$1" \
                    --project "$PROJECT" --startup-project "$STARTUP" --context "$CONTEXT"
                ;;
            *)
                dotnet ef migrations script "$1" "$2" \
                    --project "$PROJECT" --startup-project "$STARTUP" --context "$CONTEXT"
                ;;
        esac
        ;;
    help|--help|-h)
        usage
        ;;
    *)
        echo "Unknown command: $cmd" >&2
        usage
        exit 1
        ;;
esac
