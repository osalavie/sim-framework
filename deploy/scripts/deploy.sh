#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat >&2 <<'USAGE'
Usage: deploy.sh <test|prod> <api|web|all> <tag> <registry> [--migrate]
Environment:
  DEPLOY_DIR  Deployment directory (default: /opt/sim/deploy)
USAGE
    exit 2
}

[ "$#" -ge 4 ] || usage

env_name=$1
service=$2
tag=$3
registry=$4
migrate_flag=${5:-}

case "$env_name" in
    test | prod) ;;
    *) usage ;;
esac
case "$service" in
    api | web | all) ;;
    *) usage ;;
esac
case "$migrate_flag" in
    "" | --migrate) ;;
    *) usage ;;
esac
if [ "$migrate_flag" = "--migrate" ] && [ "$service" = "web" ]; then
    echo "--migrate is only valid for api or all" >&2
    exit 2
fi

deploy_dir=${DEPLOY_DIR:-/opt/sim/deploy}
compose_file="$deploy_dir/docker-compose.yml"
env_file="$deploy_dir/.env"
api_ref="$registry/sim-api:$tag"
web_ref="$registry/sim-web:$tag"

for command_name in docker curl; do
    command -v "$command_name" >/dev/null 2>&1 || {
        echo "Required command not found: $command_name" >&2
        exit 1
    }
done

[ -f "$compose_file" ] || {
    echo "Missing $compose_file" >&2
    exit 1
}
[ -f "$env_file" ] || {
    echo "Missing $env_file" >&2
    exit 1
}

if command -v flock >/dev/null 2>&1; then
    exec 9>"$deploy_dir/.deploy.lock"
    flock -w 300 9 || {
        echo "Another deployment is running" >&2
        exit 1
    }
else
    lock_dir="$deploy_dir/.deploy.lock.d"
    lock_acquired=0
    attempt=0
    while [ "$attempt" -lt 300 ]; do
        if mkdir "$lock_dir" 2>/dev/null; then
            lock_acquired=1
            break
        fi
        attempt=$((attempt + 1))
        sleep 1
    done
    [ "$lock_acquired" -eq 1 ] || {
        echo "Another deployment is running" >&2
        exit 1
    }
    trap 'rmdir "$lock_dir" 2>/dev/null || true' EXIT
fi

services=()
if [ "$service" = "api" ] || [ "$service" = "all" ]; then
    services+=(api)
fi
if [ "$service" = "web" ] || [ "$service" = "all" ]; then
    services+=(web)
fi

sed -i 's/\r$//' "$env_file"
backup_file="$env_file.bak.$(date +%Y%m%d%H%M%S)"
cp -p "$env_file" "$backup_file"

update_env() {
    local tmp
    tmp=$(mktemp "$deploy_dir/.env.XXXXXX")

    grep -v -E '^(SIM_API_IMAGE|SIM_WEB_IMAGE)=' "$env_file" >"$tmp" || true

    if [ "$service" = "api" ] || [ "$service" = "all" ]; then
        printf 'SIM_API_IMAGE=%s\n' "$api_ref" >>"$tmp"
    else
        grep -E '^SIM_API_IMAGE=' "$env_file" >>"$tmp" || true
    fi

    if [ "$service" = "web" ] || [ "$service" = "all" ]; then
        printf 'SIM_WEB_IMAGE=%s\n' "$web_ref" >>"$tmp"
    else
        grep -E '^SIM_WEB_IMAGE=' "$env_file" >>"$tmp" || true
    fi

    chmod 600 "$tmp"
    mv "$tmp" "$env_file"
}

restore_env() {
    cp -p "$backup_file" "$env_file"
    chmod 600 "$env_file" || true
}

rollback() {
    local status=$?
    trap - ERR

    echo "Deployment failed; rolling back $service..." >&2
    if [ "$migrate_flag" = "--migrate" ]; then
        echo "WARNING: --migrate was executed before the failure. Restore the pre-deployment database backup if the schema is not backward compatible." >&2
    fi

    restore_env
    docker compose --project-directory "$deploy_dir" --env-file "$env_file" \
        up -d --remove-orphans "${services[@]}" || true
    exit "$status"
}
trap rollback ERR

update_env

cd "$deploy_dir"
compose=(docker compose --project-directory "$deploy_dir" --env-file "$env_file")
"${compose[@]}" pull "${services[@]}"

if [ "$migrate_flag" = "--migrate" ]; then
    echo "Running database migration..."
    "${compose[@]}" run --rm --no-deps api --migrate
fi

"${compose[@]}" up -d --remove-orphans "${services[@]}"

wait_for_http() {
    local url=$1
    local curl_options=$2
    local attempts=${3:-30}
    local attempt

    for ((attempt = 1; attempt <= attempts; attempt++)); do
        if curl $curl_options -fsS --max-time 5 "$url" >/dev/null; then
            return 0
        fi
        sleep 2
    done

    return 1
}

if [ "$service" = "api" ] || [ "$service" = "all" ]; then
    wait_for_http "http://127.0.0.1:7777/health/ready" "" 60 || {
        echo "API readiness check failed" >&2
        false
    }
fi

if [ "$service" = "web" ] || [ "$service" = "all" ]; then
    wait_for_http "https://127.0.0.1/health/ready" "-k" 60 || {
        echo "Web readiness check failed" >&2
        false
    }
fi

rm -f "$backup_file"
echo "Deployment succeeded: env=$env_name service=$service tag=$tag"
