#!/usr/bin/env bash
# Validates the one API origin the hosted web head may call and prints its CSP connect-src value.
#
# Input:  AUTONOGRAMMER_DEMO=1                         Demo build (ADR-0017): the web head answers every API route in the browser, so it
#                                              needs no API origin; prints connect-src 'self' and ignores AUTONOGRAMMER_API_ORIGIN
#         AUTONOGRAMMER_API_ORIGIN                     https://host[:port] only
#         AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK=1    local development only: also accepts http://127.0.0.1:<port>
# Output: on success, exactly one line: connect-src 'self' <origin>
# Exit:   0 on success; 1 on any invalid or missing origin. The origin value is never printed,
#         because it may carry credentials in an invalid form.
set -euo pipefail

fail() {
  printf 'render-csp: %s\n' "$1" >&2
  exit 1
}

# The Demo makes no network call, so its CSP allows nothing but the page's own origin. Only the literal 1 selects it: any other value
# (empty, 0, true) is a normal build and still fails closed without an origin below.
if [[ "${AUTONOGRAMMER_DEMO-}" == "1" ]]; then
  printf "connect-src 'self'\n"
  exit 0
fi

origin="${AUTONOGRAMMER_API_ORIGIN-}"

if [[ -z "$origin" ]]; then
  fail "AUTONOGRAMMER_API_ORIGIN is not set; expected https://host[:port]"
fi

# RFC 1123 host label: 1-63 characters, letters, digits and interior hyphens.
label='[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?'
https_re='^https://'"$label"'(\.'"$label"')*(:[0-9]{1,5})?$'
loopback_re='^http://127\.0\.0\.1:[0-9]{1,5}$'

allowed=0
if [[ "$origin" =~ $https_re ]]; then
  allowed=1
elif [[ "${AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK-}" == "1" && "$origin" =~ $loopback_re ]]; then
  allowed=1
fi

if [[ "$allowed" -ne 1 ]]; then
  fail "AUTONOGRAMMER_API_ORIGIN must be https://host[:port] with no path, query, fragment, userinfo, wildcard or trailing slash (loopback http://127.0.0.1:<port> only when AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK=1)"
fi

# Host length limit (RFC 1035): at most 253 characters without the port.
authority="${origin#*://}"
host="${authority%%:*}"
if [[ "${#host}" -gt 253 ]]; then
  fail "AUTONOGRAMMER_API_ORIGIN host exceeds 253 characters"
fi

# Explicit port must be 1-65535. Base 10 avoids octal interpretation of leading zeros.
if [[ "$authority" == *:* ]]; then
  port="${authority##*:}"
  if (( 10#$port < 1 || 10#$port > 65535 )); then
    fail "AUTONOGRAMMER_API_ORIGIN port must be between 1 and 65535"
  fi
fi

printf "connect-src 'self' %s\n" "$origin"
