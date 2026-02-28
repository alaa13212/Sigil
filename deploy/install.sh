#!/usr/bin/env bash
set -e

# When run via `curl | bash`, the shell is non-interactive and non-login,
# so common install paths may be missing from PATH.
export PATH="$PATH:/usr/local/bin:/usr/bin:/bin:/snap/bin"

echo "Sigil Installer"
echo "-------------------"

# Check Docker is installed
if ! command -v docker >/dev/null 2>&1; then
  echo "Error: Docker is not installed. Install Docker first: https://docs.docker.com/get-docker/"
  exit 1
fi

# Check if Docker can be run without sudo; if not, use sudo.
# docker info exits 0 even on permission errors, so use docker ps instead.
DOCKER_SUDO=""
if ! docker ps >/dev/null 2>&1; then
  echo "Docker requires elevated permissions. You may be prompted for your password."
  if ! sudo docker ps >/dev/null 2>&1; then
    echo "Error: Cannot access the Docker daemon. Make sure Docker is running."
    exit 1
  fi
  DOCKER_SUDO="sudo"
fi

if ! $DOCKER_SUDO docker compose version >/dev/null 2>&1; then
  echo "Error: Docker Compose v2 is required."
  exit 1
fi

# Defaults
DEFAULT_PORT=8080
DEFAULT_DB_NAME=sigil
DEFAULT_DB_USER=sigil
DEFAULT_DB_PASS=$(openssl rand -hex 12)

# Prompt user.
# Read from /dev/tty explicitly so prompts work when the script is piped
# via `curl | bash` (where stdin is the pipe, not the terminal).
read -p "HTTP Port [$DEFAULT_PORT]: " SIGIL_PORT </dev/tty
SIGIL_PORT=${SIGIL_PORT:-$DEFAULT_PORT}

read -p "Postgres DB Name [$DEFAULT_DB_NAME]: " DB_NAME </dev/tty
DB_NAME=${DB_NAME:-$DEFAULT_DB_NAME}

read -p "Postgres User [$DEFAULT_DB_USER]: " DB_USER </dev/tty
DB_USER=${DB_USER:-$DEFAULT_DB_USER}

read -s -p "Postgres Password [$DEFAULT_DB_PASS]: " DB_PASS </dev/tty
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
$DOCKER_SUDO docker compose up -d

echo ""
echo "Sigil is running!"
echo "Open: http://localhost:$SIGIL_PORT"
echo "Postgres password: $DB_PASS"
