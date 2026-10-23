#!/usr/bin/env bash
# Builds and runs the front end (web) and the skailFunc (worker) with podman or docker.
# The skail server is the one of your environment in the skail portal (SKAIL_SIDECAR and SKAIL_BASE_URL
# in .env); nothing skail-related runs locally.
#
# Usage: scripts/run.sh [build|up|down|restart|logs|status]   (default: up)
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
  "$ENGINE" rm -f "$WORKER" "$WEB" >/dev/null 2>&1 || true
}

up() {
  build
  down
  "$ENGINE" network exists "$NETWORK" >/dev/null 2>&1 || "$ENGINE" network create "$NETWORK" >/dev/null

  "$ENGINE" run -d --name "$WEB" --network "$NETWORK" \
    --env-file .env -p "${WEB_PORT:-8080}:8080" "$WEB" >/dev/null

  # resource limits for the worker: 1 GB of memory and 1 CPU
  "$ENGINE" run -d --name "$WORKER" --network "$NETWORK" \
    --env-file .env --memory 1g --cpus 1 "$WORKER" >/dev/null

  echo "front end: http://localhost:${WEB_PORT:-8080}"
  echo "logs:      scripts/run.sh logs"
}

case "${1:-up}" in
  build) build ;;
  up) up ;;
  down) down ;;
  restart) "$ENGINE" restart "$WORKER" "$WEB" ;;
  logs) "$ENGINE" logs -f --tail 100 "$WORKER" "$WEB" 2>&1 || "$ENGINE" logs -f "$WORKER" ;;
  status) "$ENGINE" ps --filter "name=usd-quote" ;;
  *) echo "Usage: $0 [build|up|down|restart|logs|status]" >&2; exit 1 ;;
esac
