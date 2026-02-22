#!/usr/bin/env bash
set -e

echo "Sigil Installer"
echo "-------------------"

# Check Docker
if ! command -v docker &> /dev/null; then
  echo "Error: Docker is not installed. Install Docker first."
  exit 1
fi

if ! docker compose version &> /dev/null; then
  echo "Error: Docker Compose v2 is required."
  exit 1
fi

# Defaults
DEFAULT_PORT=8080
DEFAULT_DB_NAME=sigil
DEFAULT_DB_USER=sigil
DEFAULT_DB_PASS=$(openssl rand -hex 12)

# Prompt user
read -p "HTTP Port [$DEFAULT_PORT]: " SIGIL_PORT
SIGIL_PORT=${SIGIL_PORT:-$DEFAULT_PORT}

read -p "Postgres DB Name [$DEFAULT_DB_NAME]: " DB_NAME
DB_NAME=${DB_NAME:-$DEFAULT_DB_NAME}

read -p "Postgres User [$DEFAULT_DB_USER]: " DB_USER
DB_USER=${DB_USER:-$DEFAULT_DB_USER}

read -s -p "Postgres Password [$DEFAULT_DB_PASS]: " DB_PASS
echo
DB_PASS=${DB_PASS:-$DEFAULT_DB_PASS}

# Create folder
mkdir -p sigil && cd sigil

# Download compose file
curl -fsSL https://github.com/alaa13212/sigil/releases/latest/download/docker-compose.yml -o docker-compose.yml

# Write env file
cat > .env <<EOF
SIGIL_PORT=$SIGIL_PORT

POSTGRES_DB=$DB_NAME
POSTGRES_USER=$DB_USER
POSTGRES_PASSWORD=$DB_PASS
EOF

echo "Starting Sigil..."
docker compose up -d

echo ""
echo "Sigil is running!"
echo "Open: http://localhost:$SIGIL_PORT"
echo "Postgres password: $DB_PASS"