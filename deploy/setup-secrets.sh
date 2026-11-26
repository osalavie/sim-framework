#!/usr/bin/env bash
# SimApp Production Secrets Setup
# Creates key-per-file secret directory for SIM_SECRETS_DIRECTORY injection
#
# Usage: ./setup-secrets.sh [secrets-dir] [swagger-htpasswd]
# Env:   JWT_KEY_COUNT=n   default JWT signing-key count used by the prompt (default 3)
#        SECRETS_UID       owner UID for the secret directory (default 1000)
#        SECRETS_GID       owner GID for the secret directory (default 1000)
#
# The script shows a selection menu. You can choose to configure JWT signing
# keys, database, Redis, RabbitMQ, or nginx Swagger Basic Auth secrets
# individually, or all of them.
#
# Each file in the directory becomes a configuration key:
# - Filename uses __ as hierarchy separator (maps to : in .NET config)
# - File content is the value (no trailing newline needed)

set -euo pipefail

SECRETS_DIR="${1:-./secrets}"
SWAGGER_AUTH_FILE="${2:-./nginx/.htpasswd}"
SECRETS_UID="${SECRETS_UID:-1000}"
SECRETS_GID="${SECRETS_GID:-1000}"
JWT_KEY_COUNT_DEFAULT="${JWT_KEY_COUNT:-3}"
mkdir -p "$SECRETS_DIR"

echo "=== SimApp Secrets Setup ==="
echo "Target directory: $SECRETS_DIR"
echo ""

set_jwt_keys() {
    if ! [[ "$JWT_KEY_COUNT_DEFAULT" =~ ^[0-9]+$ ]] || [ "$JWT_KEY_COUNT_DEFAULT" -lt 1 ]; then
        echo "JWT_KEY_COUNT must be a positive integer (default: 3)." >&2
        return 1
    fi

    read -r -p "Enter JWT key count [$JWT_KEY_COUNT_DEFAULT]: " JWT_KEY_COUNT_INPUT
    local JWT_KEY_COUNT="${JWT_KEY_COUNT_INPUT:-$JWT_KEY_COUNT_DEFAULT}"
    if ! [[ "$JWT_KEY_COUNT" =~ ^[0-9]+$ ]] || [ "$JWT_KEY_COUNT" -lt 1 ]; then
        echo "JWT key count must be a positive integer." >&2
        return 1
    fi

    echo "Generating JWT signing keys (count=$JWT_KEY_COUNT)..."
    local i KID SECRET IS_ACTIVE
    for ((i = 0; i < JWT_KEY_COUNT; i++)); do
        KID=$((i + 1))
        SECRET=$(openssl rand -hex 64)
        IS_ACTIVE="false"
        if [ "$i" -eq 0 ]; then
            IS_ACTIVE="true"
        fi

        printf '%s' "$KID" > "$SECRETS_DIR/JwtSettings__SigningKeys__Keys__${i}__Kid"
        printf '%s' "$SECRET" > "$SECRETS_DIR/JwtSettings__SigningKeys__Keys__${i}__Secret"
        printf '%s' "$IS_ACTIVE" > "$SECRETS_DIR/JwtSettings__SigningKeys__Keys__${i}__IsActive"

        echo "  Kid=$KID (Keys__${i}__) [active=$IS_ACTIVE]"
    done
}

set_database_connection() {
    read -r -p "Enter database connection string (empty to skip): " DATABASE_CONNECTION_STRING
    if [[ -n "$DATABASE_CONNECTION_STRING" ]]; then
        printf '%s' "$DATABASE_CONNECTION_STRING" > "$SECRETS_DIR/DataBase__ConnectionConfigs__0__ConnectionString"
        echo "  Database connection string set."
    else
        echo "  Database connection string skipped."
    fi
}

set_redis_connection() {
    read -r -p "Enter Redis connection string (empty to skip): " REDIS_CONNECTION_STRING
    if [[ -n "$REDIS_CONNECTION_STRING" ]]; then
        printf '%s' "$REDIS_CONNECTION_STRING" > "$SECRETS_DIR/Framework__Caching__RedisConnectionString"
        echo "  Redis connection string set."
    else
        echo "  Redis connection string skipped."
    fi
}

set_rabbitmq_connection() {
    read -r -p "Enter RabbitMQ connection string (empty to skip): " RABBITMQ_CONNECTION_STRING
    if [[ -n "$RABBITMQ_CONNECTION_STRING" ]]; then
        printf '%s' "$RABBITMQ_CONNECTION_STRING" > "$SECRETS_DIR/Framework__Messaging__ConnectionString"
        echo "  RabbitMQ connection string set."
    else
        echo "  RabbitMQ connection string skipped."
    fi
}

set_swagger_basic_auth() {
    read -r -p "Enter Swagger Basic Auth username (empty to skip): " SWAGGER_USERNAME
    if [[ -z "$SWAGGER_USERNAME" ]]; then
        echo "  Swagger Basic Auth skipped."
        return 0
    fi
    if ! [[ "$SWAGGER_USERNAME" =~ ^[A-Za-z0-9._-]+$ ]]; then
        echo "Swagger username can only contain letters, digits, dot, underscore, and hyphen." >&2
        return 0
    fi

    read -r -s -p "Enter Swagger Basic Auth password: " SWAGGER_PASSWORD
    echo ""
    read -r -s -p "Confirm Swagger Basic Auth password: " SWAGGER_PASSWORD_CONFIRM
    echo ""
    if [[ -z "$SWAGGER_PASSWORD" ]]; then
        echo "Swagger password cannot be empty." >&2
        return 0
    fi
    if [[ "$SWAGGER_PASSWORD" != "$SWAGGER_PASSWORD_CONFIRM" ]]; then
        echo "Swagger passwords do not match." >&2
        return 0
    fi
    if ! command -v openssl >/dev/null 2>&1; then
        echo "openssl is required to generate the nginx Basic Auth password hash." >&2
        return 0
    fi

    local SWAGGER_PASSWORD_HASH
    SWAGGER_PASSWORD_HASH=$(printf '%s' "$SWAGGER_PASSWORD" | openssl passwd -apr1 -stdin)
    mkdir -p "$(dirname "$SWAGGER_AUTH_FILE")"
    printf '%s:%s
' "$SWAGGER_USERNAME" "$SWAGGER_PASSWORD_HASH" > "$SWAGGER_AUTH_FILE"
    chmod 600 "$SWAGGER_AUTH_FILE"
    echo "  Swagger Basic Auth file set: $SWAGGER_AUTH_FILE"
}

while true; do
    echo "Select a secret to set:"
    echo "  1) JWT signing keys"
    echo "  2) Database connection string"
    echo "  3) Redis connection string"
    echo "  4) RabbitMQ connection string"
    echo "  5) Swagger Basic Auth (nginx .htpasswd)"
    echo "  6) All secrets"
    echo "  0) Finish"
    read -r -p "Choice [0-6]: " choice
    case "$choice" in
        1) set_jwt_keys ;;
        2) set_database_connection ;;
        3) set_redis_connection ;;
        4) set_rabbitmq_connection ;;
        5) set_swagger_basic_auth ;;
        6) set_jwt_keys && set_database_connection && set_redis_connection && set_rabbitmq_connection && set_swagger_basic_auth ;;
        0) break ;;
        *) echo "Invalid choice." >&2 ;;
    esac
    echo ""
done

# --- Set permissions ---
if [ "$(id -u)" -eq 0 ]; then
    chown -R "$SECRETS_UID:$SECRETS_GID" "$SECRETS_DIR"
else
    echo "Not running as root; skipped chown. Ensure $SECRETS_DIR is owned by $SECRETS_UID:$SECRETS_GID."
fi
if find "$SECRETS_DIR" -maxdepth 1 -type f -print -quit | grep -q .; then
    chmod 600 "$SECRETS_DIR"/*
else
    echo "No secret files created; skipped file permission changes."
fi
chmod 700 "$SECRETS_DIR"

echo ""
echo "=== Secrets created successfully ==="
echo "Set environment variable: SIM_SECRETS_DIRECTORY=$(cd "$SECRETS_DIR" && pwd)"
echo ""
echo "Files created:"
ls -la "$SECRETS_DIR"
if [[ -f "$SWAGGER_AUTH_FILE" ]]; then
    echo "Swagger Basic Auth file: $SWAGGER_AUTH_FILE"
fi
