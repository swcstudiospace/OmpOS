#!/usr/bin/env bash
# Ad-hoc signing helpers for the Autonogrammer.app bundle. SOURCE this file; it has no side effects and changes no shell options:
#
#   source "$(dirname "${BASH_SOURCE[0]}")/macos-adhoc-sign.sh"
#   adhoc_sign_bundle <bundle>          signs every Mach-O file under <bundle>/Contents/MacOS, then the bundle itself
#   verify_bundle_signature <bundle>    checks those signatures
#
# Why not `codesign --deep`: the self-contained publish output, the bundled plugin trees (plugins/<dir>, with dot-directories such
# as .cursor, .claude-plugin and .github) and the .NET dylibs all sit flat in Contents/MacOS. --deep walks every sub-directory
# there and treats directory names that look like bundles as nested code, so it fails with "bundle format unrecognized, invalid,
# or unsuitable ... In subcomponent: .../Contents/MacOS/plugins/agent-swarm/.cursor" (GitHub Actions run 44508808964). The layout
# cannot change, because the app finds its plugins beside its executable. So each Mach-O file is signed on its own, and the bundle
# is then signed WITHOUT --deep, which binds Contents/Info.plist and the main executable.
#
# The signature is ad hoc (identity "-"): not a Developer ID signature, no hardened runtime, not notarized.
# Only bash 3.2 features are used (macOS ships bash 3.2). All output goes to stderr; stdout stays free for the callers' results.
# Both functions return non-zero on failure and check every command themselves, so a caller may use `fn || handler`.

# Prints the regular files under $1/Contents/MacOS that `file -b` identifies as Mach-O (thin and universal), NUL-separated.
_adhoc_macho_files() {
  local f
  while IFS= read -r -d '' f; do
    case "$(file -b "$f")" in
      *Mach-O*) printf '%s\0' "$f" ;;
    esac
  done < <(find "$1/Contents/MacOS" -type f -print0)
}

# Failure diagnostic: the Mach-O files that were considered.
_adhoc_list_machos() {
  local f
  echo "Mach-O files under $1/Contents/MacOS:" >&2
  while IFS= read -r -d '' f; do
    echo "  $f" >&2
  done < <(_adhoc_macho_files "$1")
}

adhoc_sign_bundle() {
  local bundle="$1" f count=0
  if [ ! -d "$bundle/Contents/MacOS" ]; then
    echo "adhoc_sign_bundle: $bundle/Contents/MacOS not found" >&2
    return 1
  fi
  while IFS= read -r -d '' f; do
    if ! codesign --force --sign - --timestamp=none "$f" >&2; then
      echo "adhoc_sign_bundle: codesign failed for $f" >&2
      _adhoc_list_machos "$bundle"
      return 1
    fi
    count=$((count + 1))
  done < <(_adhoc_macho_files "$bundle")
  if [ "$count" -eq 0 ]; then
    echo "adhoc_sign_bundle: no Mach-O file found under $bundle/Contents/MacOS" >&2
    return 1
  fi
  echo "ad-hoc signed $count Mach-O files under $bundle/Contents/MacOS" >&2
  if ! codesign --force --sign - --timestamp=none "$bundle" >&2; then
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
  # (a) every Mach-O file carries a valid signature
  while IFS= read -r -d '' f; do
    if ! codesign --verify --strict "$f" >&2; then
      echo "verify_bundle_signature: $f does not verify" >&2
      _adhoc_list_machos "$bundle"
      return 1
    fi
    count=$((count + 1))
  done < <(_adhoc_macho_files "$bundle")
  if [ "$count" -eq 0 ]; then
    echo "verify_bundle_signature: no Mach-O file found under $bundle/Contents/MacOS" >&2
    return 1
  fi
  echo "verified $count Mach-O files under $bundle/Contents/MacOS" >&2
  # (b) the bundle itself (no --deep)
  if ! codesign --verify --verbose=2 "$bundle" >&2; then
    echo "verify_bundle_signature: the bundle $bundle does not verify" >&2
    _adhoc_list_machos "$bundle"
    return 1
  fi
  # (c) non-fatal diagnostic: the strict check also flags unsealed content in the bundle
  if codesign --verify --strict --verbose=2 "$bundle" >&2; then
    echo "strict bundle verification: ok" >&2
  else
    echo "strict bundle verification: FAILED (not fatal, see output above)" >&2
  fi
  # (d) show the signature kind in the log (expect Signature=adhoc)
  codesign -dv --verbose=2 "$bundle" 2>&1 | grep -E 'Signature|Identifier|Format' >&2 || true
  return 0
}
