#!/usr/bin/env bash
# Builds and runs the web app and the worker (the skail function) with podman or docker.
# Both connect to your skail environment with the values in .env; nothing from skail runs locally.
#
# Usage: scripts/run.sh [build|up|down|restart|logs|status]   (default: up)
#   restart  restarts only the worker, so a run survives it while the web app keeps the page state
# Override the engine with CONTAINER_ENGINE=docker|podman.
set -euo pipefail

cd "$(dirname "$0")/.."

ENGINE="${CONTAINER_ENGINE:-}"
if [ -z "$ENGINE" ]; then
  if command -v podman >/dev/null 2>&1; then ENGINE=podman
  elif command -v docker >/dev/null 2>&1; then ENGINE=docker
  else echo "ERROR: neither podman nor docker found" >&2; exit 1
  fi
fi

NETWORK=usd-quote
WEB=usd-quote-web
WORKER=usd-quote-worker

if [ ! -f .env ]; then
  echo "ERROR: .env not found. Run: cp .env.example .env" >&2
  exit 1
fi
# shellcheck disable=SC1091
set -a; . ./.env; set +a

build() {
  "$ENGINE" build -f src/SkailUsdQuote.Worker/Dockerfile -t "$WORKER" .
  "$ENGINE" build -f src/SkailUsdQuote.Web/Dockerfile -t "$WEB" .
}

down() {
  # one call per container: podman stops at the first missing name and would leave the other one behind
  for name in "$WORKER" "$WEB"; do
    "$ENGINE" rm -f "$name" >/dev/null 2>&1 || true
  done
}

up() {
  build
  down
  "$ENGINE" network exists "$NETWORK" >/dev/null 2>&1 || "$ENGINE" network create "$NETWORK" >/dev/null

  "$ENGINE" run -d --name "$WEB" --network "$NETWORK" \
    --env-file .env -p "${WEB_PORT:-8080}:8080" "$WEB" >/dev/null

  "$ENGINE" run -d --name "$WORKER" --network "$NETWORK" \
    --env-file .env --memory 1g --cpus 1 "$WORKER" >/dev/null

  echo "web app: http://localhost:${WEB_PORT:-8080}"
  echo "logs:    scripts/run.sh logs"
}

case "${1:-up}" in
  build) build ;;
  up) up ;;
  down) down ;;
  restart) "$ENGINE" restart "$WORKER" ;;
  logs) "$ENGINE" logs -f --tail 100 "$WORKER" "$WEB" 2>&1 || "$ENGINE" logs -f "$WORKER" ;;
  status) "$ENGINE" ps --filter "name=usd-quote" ;;
  *) echo "Usage: $0 [build|up|down|restart|logs|status]" >&2; exit 1 ;;
esac
