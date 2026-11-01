#!/bin/sh
# Rewrites Api:BaseUrl in a published web appsettings.json to the origin in AUTONOGRAMMER_API_ORIGIN.
# Optional AUTONOGRAMMER_AUTH_ORIGIN (https://host[:port] only, no loopback) rewrites Auth:Origin. Unset leaves Origin empty.
# The Demo file is never passed here.
#
# Usage: AUTONOGRAMMER_API_ORIGIN=https://api.example.com sh scripts/render-web-config.sh <appsettings.json>
#
# Accepted API origins (scheme, host, optional port; no path, query, fragment or userinfo):
#   https://<host>[:port]
#   http://127.0.0.1:<port>
#   http://localhost:<port>
# Anything else exits non-zero without touching the file and without echoing the value. No network access.
set -eu

fail() {
    printf 'render-web-config: %s\n' "$1" >&2
    exit 1
}

if [ "$#" -ne 1 ]; then
    printf 'usage: AUTONOGRAMMER_API_ORIGIN=<origin> sh scripts/render-web-config.sh <appsettings.json>\n' >&2
    exit 2
fi
settings=$1

origin=${AUTONOGRAMMER_API_ORIGIN:-}
[ -n "$origin" ] || fail "AUTONOGRAMMER_API_ORIGIN is not set"

case $origin in
    https://*)
        authority=${origin#https://}
        ;;
    http://127.0.0.1:*|http://localhost:*)
        authority=${origin#http://}
        ;;
    *)
        fail "origin must be https://<host>[:port], or http://127.0.0.1:<port> or http://localhost:<port>"
        ;;
esac

# Only letters, digits, dots, hyphens and a single colon may appear. This rejects
# paths, queries, userinfo, whitespace, quotes and backslashes before the value reaches JSON.
case $authority in
    *[!A-Za-z0-9.:-]*) fail "origin contains disallowed characters" ;;
esac

case $authority in
    *:*)
        hasport=1
        host=${authority%%:*}
        port=${authority#*:}
        ;;
    *)
        hasport=0
        host=$authority
        port=
        ;;
esac

case $host in
    '') fail "origin has no host" ;;
    .*|*.|-*|*-|*..*|*.-*|*-.*) fail "origin host is malformed" ;;
esac

case $origin in
    https://*)
        ;;
    http://*)
        case $host in
            127.0.0.1|localhost) ;;
            *) fail "http origins are allowed only for 127.0.0.1 and localhost" ;;
        esac
        [ "$hasport" -eq 1 ] || fail "http loopback origin requires an explicit port"
        ;;
esac

if [ "$hasport" -eq 1 ]; then
    case $port in
        ''|*:*|*[!0-9]*) fail "origin port is malformed" ;;
    esac
    [ "${#port}" -le 5 ] || fail "origin port is out of range"
    [ "$port" -ge 1 ] && [ "$port" -le 65535 ] || fail "origin port is out of range"
fi

[ -f "$settings" ] || fail "not a regular file: $settings"

count=$(grep -c '"BaseUrl"' "$settings" || :)
[ "$count" = 1 ] || fail "expected exactly one \"BaseUrl\" key in $settings, found $count"

tmp="$settings.tmp.$$"
trap 'rm -f "$tmp"' EXIT
sed 's|"BaseUrl"[[:space:]]*:[[:space:]]*"[^"]*"|"BaseUrl": "'"$origin"'"|' "$settings" > "$tmp"

replaced=$(grep -cF "\"BaseUrl\": \"$origin\"" "$tmp" || :)
[ "$replaced" = 1 ] || fail "failed to rewrite Api:BaseUrl in $settings"

cat "$tmp" > "$settings"

auth=${AUTONOGRAMMER_AUTH_ORIGIN-}
if [ -n "$auth" ]; then
    case $auth in
        https://*) auth_authority=${auth#https://} ;;
        *) fail "auth origin must be https://<host>[:port]" ;;
    esac
    case $auth_authority in
        *[!A-Za-z0-9.:-]*) fail "auth origin contains disallowed characters" ;;
        '') fail "auth origin has no host" ;;
    esac
    case $auth_authority in
        *:*)
            auth_host=${auth_authority%%:*}
            auth_port=${auth_authority#*:}
            case $auth_port in
                ''|*:*|*[!0-9]*) fail "auth origin port is malformed" ;;
            esac
            [ "${#auth_port}" -le 5 ] || fail "auth origin port is out of range"
            [ "$auth_port" -ge 1 ] && [ "$auth_port" -le 65535 ] || fail "auth origin port is out of range"
            ;;
        *)
            auth_host=$auth_authority
            ;;
    esac
    case $auth_host in
        '') fail "auth origin has no host" ;;
        .*|*.|-*|*-|*..*|*.-*|*-.*) fail "auth origin host is malformed" ;;
    esac
    origin_count=$(grep -c '"Origin"' "$settings" || :)
    [ "$origin_count" = 1 ] || fail "expected exactly one \"Origin\" key in $settings, found $origin_count"
    auth_tmp="$settings.auth.$$"
    sed 's|"Origin"[[:space:]]*:[[:space:]]*"[^"]*"|"Origin": "'"$auth"'"|' "$settings" > "$auth_tmp"
    auth_replaced=$(grep -cF "\"Origin\": \"$auth\"" "$auth_tmp" || :)
    [ "$auth_replaced" = 1 ] || fail "failed to rewrite Auth:Origin in $settings"
    cat "$auth_tmp" > "$settings"
    rm -f "$auth_tmp"
fi

printf 'render-web-config: Api:BaseUrl set to %s in %s\n' "$origin" "$settings"
