#!/usr/bin/env bash
# Prepares a fresh Vikunja container for the live tests: a user, one open task and an API token with
# only the permissions the plugin documents. Writes the values the tests read to $GITHUB_ENV.
set -euo pipefail

base="${1:-http://localhost:3456}"
api="$base/api/v1"

for _ in $(seq 1 60); do
	curl -sf "$api/info" > /dev/null && break
	sleep 2
done
curl -sf "$api/info"
echo

# Throwaway account inside a CI container that is deleted with the job.
password="ci-$(openssl rand -hex 12)"
curl -sS --fail-with-body -X POST "$api/register" -H 'Content-Type: application/json' \
	-d "{\"username\":\"citest\",\"email\":\"citest@example.invalid\",\"password\":\"$password\",\"language\":\"en\"}" > /dev/null
jwt=$(curl -sS --fail-with-body -X POST "$api/login" -H 'Content-Type: application/json' \
	-d "{\"username\":\"citest\",\"password\":\"$password\"}" | jq -r .token)
auth=(-H "Authorization: Bearer $jwt" -H 'Content-Type: application/json')

project=$(curl -sS --fail-with-body -X PUT "$api/projects" "${auth[@]}" -d '{"title":"Live test"}' | jq -r .id)
task=$(curl -sS --fail-with-body -X PUT "$api/projects/$project/tasks" "${auth[@]}" \
	-d '{"title":"Live task","description":"Must survive the check"}' | jq -r .id)

token=$(curl -sS --fail-with-body -X PUT "$api/tokens" "${auth[@]}" \
	-d '{"title":"plugin","expires_at":"2099-01-01T00:00:00Z","permissions":{"tasks":["read_all","update"]}}' | jq -r .token)

# The session JWT lets the test read the task back with full rights to check what the PATCH left alone.
{
	echo "VIKUNJA_LIVE_URL=$base"
	echo "VIKUNJA_LIVE_TOKEN=$token"
	echo "VIKUNJA_LIVE_JWT=$jwt"
	echo "VIKUNJA_LIVE_TASK_ID=$task"
} >> "$GITHUB_ENV"
