#!/usr/bin/env bash
# The Keycloak the *KeycloakIntegrationTests sign in against, and the one place its image, port and bootstrap admin
# (admin/admin, which KeycloakServer.cs signs in with) are set. The tests create uniquely named realms through the admin
# API, so the container needs no configuration of its own.
#
#   keycloak.sh start   starts the container detached, bound to 127.0.0.1:${KEYCLOAK_PORT:-8080}, and sets KEYCLOAK_URL
#                       for later steps in GitHub Actions (prints it otherwise)
#   keycloak.sh wait    waits up to 3 minutes for it to answer, printing its log if it never does
#   keycloak.sh test P  runs test project P's *KeycloakIntegrationTests (already built in Release) against it; the
#                       normal test runs skip them with every other *IntegrationTests class
#
# CI starts it before restoring and building, and waits just before the tests, so the two overlap.
set -euo pipefail

KEYCLOAK_IMAGE="quay.io/keycloak/keycloak:26.7.1"
CONTAINER="easyreasy-keycloak"
PORT="${KEYCLOAK_PORT:-8080}"
URL="http://127.0.0.1:${PORT}"

case "${1:-}" in
  start)
    docker run -d --name "$CONTAINER" -p "127.0.0.1:${PORT}:8080" \
      -e KC_BOOTSTRAP_ADMIN_USERNAME=admin \
      -e KC_BOOTSTRAP_ADMIN_PASSWORD=admin \
      "$KEYCLOAK_IMAGE" start-dev
    if [ -n "${GITHUB_ENV:-}" ]; then
      echo "KEYCLOAK_URL=${URL}" >> "$GITHUB_ENV"
    else
      echo "export KEYCLOAK_URL=${URL}"
    fi
    ;;
  wait)
    for attempt in $(seq 1 90); do
      if curl -sf "${URL}/realms/master" > /dev/null; then
        echo "Keycloak answered after ${attempt} attempts."
        exit 0
      fi
      sleep 2
    done
    echo "::error::Keycloak did not answer within 3 minutes."
    docker logs "$CONTAINER"
    exit 1
    ;;
  test)
    PROJECT="${2:?usage: $0 test <test project>}"
    KEYCLOAK_URL="${KEYCLOAK_URL:-$URL}" dotnet test "$PROJECT" --no-build --configuration Release --filter "FullyQualifiedName~KeycloakIntegrationTests"
    ;;
  *)
    echo "usage: $0 start|wait|test <test project>" >&2
    exit 2
    ;;
esac
