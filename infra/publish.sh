#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

# Read version from version.json
VERSION=$(grep -o '"version": *"[^"]*"' ../version.json | head -1 | cut -d'"' -f4)
TAG="${1:-$VERSION}"
REGISTRY="${REGISTRY:-}"

echo "Building VMTO images — version: $VERSION, tag: $TAG"

VERSION=$VERSION TAG=$TAG docker compose -f docker-compose.build.yml build

if [ -n "$REGISTRY" ]; then
    for svc in api worker frontend; do
        docker tag "vmto-${svc}:${TAG}" "${REGISTRY}/vmto-${svc}:${TAG}"
        docker push "${REGISTRY}/vmto-${svc}:${TAG}"
    done
    echo "Pushed to $REGISTRY"
fi

echo "Done."
