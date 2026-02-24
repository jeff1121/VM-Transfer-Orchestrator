#!/usr/bin/env bash
set -euo pipefail

TAG="${1:-latest}"
REGISTRY="${REGISTRY:-}"

echo "Building VMTO images with tag: $TAG"

docker compose -f docker-compose.build.yml build

if [ -n "$REGISTRY" ]; then
    for svc in api worker frontend; do
        docker tag "vmto-${svc}:${TAG}" "${REGISTRY}/vmto-${svc}:${TAG}"
        docker push "${REGISTRY}/vmto-${svc}:${TAG}"
    done
    echo "Pushed to $REGISTRY"
fi

echo "Done."
