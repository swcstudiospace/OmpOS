#!/usr/bin/env bash
# Ad-hoc signing helpers for the Autonogrammer.app bundle. SOURCE this file; it has no side effects and changes no shell options:
#
#   source "$(dirname "${BASH_SOURCE[0]}")/macos-adhoc-sign.sh"
#   adhoc_sign_bundle <bundle>          guards the layout, then signs the bundle with `codesign --force --deep --sign -`
#   verify_bundle_signature <bundle>    `codesign --verify --deep --strict --verbose=2` (fatal), then prints what was signed
#
# The signature is ad hoc (identity "-"): not a Developer ID signature, no hardened runtime, not notarized.
#
# Evidence (a probe on a macos-26 arm64 runner, GitHub Actions runs 44516324587 and 44516778775, 13 bundle layouts):
#   1. Signing each Mach-O file and then the bundle without --deep fails ("code object is not signed at all In subcomponent:
#      .../System.Diagnostics.Contracts.dll"): every non-Mach-O file in Contents/MacOS counts as nested code that must be signed.
#   2. `codesign --force --deep --sign - --timestamp=none <bundle>` fails only when Contents/MacOS holds a directory whose NAME
#      STARTS WITH A DOT ("bundle format unrecognized, invalid, or unsuitable"; GitHub Actions run 44508808964 failed on
#      .../Contents/MacOS/plugins/agent-swarm/.cursor). Plain sub-directories (runtimes/osx-arm64) and dot-FILES are fine.
#   3. What works: the flat publish output in Contents/MacOS, the plugin tree in Contents/Resources/plugins, then --deep signing.
#      After it `codesign --verify --deep --strict --verbose=2` passes, the executable runs (also with the quarantine flag), a
#      `ditto -c -k --keepParent` zip round trip extracted with `ditto -x -k` verifies, and a dmg round trip (ditto into a stage
#      directory, hdiutil create UDZO, hdiutil verify, attach, verify the mounted copy, ditto out, verify, run) passes. Changing or
#      adding a file in the sealed plugin folder makes verification fail.
#   4. Caveat: --deep stores the signatures of non-Mach-O files (the .dll files) as extended attributes. Extracting the zip with the
#      plain `unzip` command drops them, so `codesign --verify --deep --strict` then fails on a .dll although the app still starts.
#      Finder and `ditto -x -k` restore them. This is documented, not fixed.
#
# Only bash 3.2 features are used (macOS ships bash 3.2). All output goes to stderr; stdout stays free for the callers' results.
# Both functions return non-zero on failure and check every command themselves, so a caller may use `fn || handler`.

# Failure diagnostic: the Mach-O files (thin and universal, per `file -b`) under $1/Contents/MacOS.
_adhoc_list_machos() {
  local f
  echo "Mach-O files under $1/Contents/MacOS:" >&2
  while IFS= read -r -d '' f; do
    case "$(file -b "$f")" in
      *Mach-O*) echo "  $f" >&2 ;;
    esac
  done < <(find "$1/Contents/MacOS" -type f -print0)
}

adhoc_sign_bundle() {
  local bundle="$1" dots
  if [ ! -d "$bundle/Contents/MacOS" ]; then
    echo "adhoc_sign_bundle: $bundle/Contents/MacOS not found" >&2
    return 1
  fi
  dots="$(find "$bundle/Contents/MacOS" -mindepth 1 -type d -name '.*')"
  if [ -n "$dots" ]; then
    echo "adhoc_sign_bundle: a directory whose name starts with a dot in Contents/MacOS makes codesign fail with 'bundle format unrecognized, invalid, or unsuitable' (probe run 44516778775). Move such content to Contents/Resources. Offenders:" >&2
    echo "$dots" >&2
    return 1
  fi
  if [ -e "$bundle/Contents/MacOS/plugins" ]; then
    echo "adhoc_sign_bundle: $bundle/Contents/MacOS/plugins exists; the plugin tree belongs in Contents/Resources/plugins (its dot-directories break codesign in Contents/MacOS)" >&2
    return 1
  fi
  if ! codesign --force --deep --sign - --timestamp=none "$bundle" >&2; then
    echo "adhoc_sign_bundle: codesign failed for the bundle $bundle" >&2
    _adhoc_list_machos "$bundle"
    return 1
  fi
  echo "ad-hoc signed the bundle $bundle" >&2
}

verify_bundle_signature() {
  local bundle="$1" f count=0
  if [ ! -d "$bundle/Contents/MacOS" ]; then
    echo "verify_bundle_signature: $bundle/Contents/MacOS not found" >&2
    return 1
  fi
  if ! codesign --verify --deep --strict --verbose=2 "$bundle" >&2; then
    echo "verify_bundle_signature: the bundle $bundle does not verify (codesign --verify --deep --strict)" >&2
    _adhoc_list_machos "$bundle"
    return 1
  fi
  while IFS= read -r -d '' f; do
    case "$(file -b "$f")" in
      *Mach-O*) count=$((count + 1)) ;;
    esac
  done < <(find "$bundle/Contents/MacOS" -type f -print0)
  echo "verified $bundle: $count Mach-O files under Contents/MacOS" >&2
  # Show the signature kind in the log (expect Signature=adhoc).
  codesign -dv --verbose=2 "$bundle" 2>&1 | grep -E 'Signature|Identifier|Format|Sealed' >&2 || true
  return 0
}
