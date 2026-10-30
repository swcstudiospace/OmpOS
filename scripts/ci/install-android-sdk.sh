#!/usr/bin/env bash
# Installs Android SDK platform 36 and build-tools 36.0.0 for the Android head.
# The cmdline-tools archive is Google's linux package 11076708 (HTTP 200 on 2026-10-30).
# Writes ANDROID_HOME and ANDROID_SDK_ROOT to GITHUB_ENV when that file is set.
set -euo pipefail

if [ -n "${ANDROID_HOME:-}" ] \
  && [ -f "$ANDROID_HOME/platforms/android-36/android.jar" ] \
  && [ -x "$ANDROID_HOME/build-tools/36.0.0/aapt2" ]; then
  echo "Android platform 36 and build-tools 36.0.0 are present at $ANDROID_HOME"
  exit 0
fi

if ! command -v sudo >/dev/null 2>&1; then
  echo "error: Android SDK is missing and sudo is not available" >&2
  exit 1
fi

sudo DEBIAN_FRONTEND=noninteractive apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y openjdk-17-jdk-headless unzip

sdk="${RUNNER_TEMP:-$HOME}/android-sdk"
mkdir -p "$sdk/cmdline-tools"
zip="${RUNNER_TEMP:-/tmp}/commandlinetools.zip"
curl -fsSL -o "$zip" https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip
rm -rf "$sdk/cmdline-tools/latest" "$sdk/cmdline-tools/cmdline-tools"
unzip -q "$zip" -d "$sdk/cmdline-tools"
if [ ! -x "$sdk/cmdline-tools/latest/bin/sdkmanager" ]; then
  mv "$sdk/cmdline-tools/cmdline-tools" "$sdk/cmdline-tools/latest"
fi
sdkmanager="$sdk/cmdline-tools/latest/bin/sdkmanager"
[ -x "$sdkmanager" ] || { echo "error: sdkmanager not found at $sdkmanager" >&2; exit 1; }

# sdkmanager exits before yes finishes writing, so yes can end on SIGPIPE; only sdkmanager's status counts.
set +o pipefail
yes | "$sdkmanager" --sdk_root="$sdk" --licenses >/dev/null
set -o pipefail
"$sdkmanager" --sdk_root="$sdk" --install "platforms;android-36" "build-tools;36.0.0"

if [ -n "${GITHUB_ENV:-}" ]; then
  echo "ANDROID_HOME=$sdk" >> "$GITHUB_ENV"
  echo "ANDROID_SDK_ROOT=$sdk" >> "$GITHUB_ENV"
fi
echo "Android SDK installed at $sdk"
