#!/usr/bin/env bash
#
# Send test events to a local Aptabase instance.
# Usage:
#   ./scripts/send-test-events.sh <APP_KEY>
#   ./scripts/send-test-events.sh <APP_KEY> <COUNT>
#
# Examples:
#   ./scripts/send-test-events.sh A-DEV-1234567890        # send 10 events (default)
#   ./scripts/send-test-events.sh A-DEV-1234567890 50     # send 50 events
#
# This script sends a mix of event types useful for testing notifications:
#   - purchase, signup, error, page_view (for event_push / threshold rules)
#   - Multiple app versions (for new_app_version detection)
#   - Multiple country codes (simulated via different sessions)

set -euo pipefail

BASE_URL="${APTABASE_URL:-https://localhost:5251}"
APP_KEY="${1:?Usage: $0 <APP_KEY> [COUNT]}"
COUNT="${2:-10}"

EVENT_NAMES=("purchase" "signup" "error" "page_view" "button_click" "app_start" "checkout")
APP_VERSIONS=("1.0.0" "1.1.0" "1.2.0" "2.0.0-beta")
SDK_VERSION="test-script/1.0"

echo "Sending $COUNT events to $BASE_URL with app key $APP_KEY"

for i in $(seq 1 "$COUNT"); do
  # Pick random event name and app version
  EVENT_NAME="${EVENT_NAMES[$((RANDOM % ${#EVENT_NAMES[@]}))]}"
  APP_VERSION="${APP_VERSIONS[$((RANDOM % ${#APP_VERSIONS[@]}))]}"
  SESSION_ID=$(uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid 2>/dev/null || echo "test-session-$((RANDOM % 100))")
  TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

  BODY=$(cat <<EOF
{
  "eventName": "$EVENT_NAME",
  "sessionId": "$SESSION_ID",
  "timestamp": "$TIMESTAMP",
  "systemProps": {
    "sdkVersion": "$SDK_VERSION",
    "osName": "macOS",
    "osVersion": "14.0",
    "appVersion": "$APP_VERSION",
    "locale": "en-US",
    "isDebug": false
  },
  "props": {
    "test_run": true,
    "index": $i
  }
}
EOF
)

  HTTP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" \
    -X POST "$BASE_URL/api/v0/event" \
    -H "Content-Type: application/json" \
    -H "App-Key: $APP_KEY" \
    -d "$BODY")

  if [ "$HTTP_CODE" = "200" ]; then
    echo "  [$i/$COUNT] $EVENT_NAME (v$APP_VERSION) -> OK"
  else
    echo "  [$i/$COUNT] $EVENT_NAME (v$APP_VERSION) -> HTTP $HTTP_CODE"
  fi

  # Small delay to avoid rate limiting (20 req/s per IP)
  sleep 0.1
done

echo "Done! Sent $COUNT events."
