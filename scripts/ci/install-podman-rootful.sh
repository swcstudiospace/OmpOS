#!/usr/bin/env bash
# Makes `podman` usable on a GitHub-hosted Ubuntu runner.
# Rootless podman cannot create a user namespace there: the runner user has no subuid range.
# That is the same failure the Omega pool hit (OQ-168). Passwordless sudo is available, so this
# installs podman and puts a rootful wrapper first on PATH. scripts/test-services.sh and
# podman-compose keep calling `podman`. Docker is not installed and not invoked.
set -euo pipefail

if command -v podman >/dev/null 2>&1 && podman info >/dev/null 2>&1; then
  echo "podman already usable: $(podman --version)"
  exit 0
fi

if ! command -v sudo >/dev/null 2>&1; then
  echo "error: podman is not usable and sudo is not available to install a rootful wrapper" >&2
  exit 1
fi

if ! command -v podman >/dev/null 2>&1; then
  sudo apt-get update
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y podman pipx
fi

wrap_dir="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/autonogrammer-bin"
mkdir -p "$wrap_dir"
cat > "$wrap_dir/podman" << 'EOF'
#!/bin/sh
exec sudo -n --preserve-env=POSTGRES_PASSWORD,DFLY_requirepass /usr/bin/podman "$@"
EOF
chmod 755 "$wrap_dir/podman"

if [ -n "${GITHUB_PATH:-}" ]; then
  echo "$wrap_dir" >> "$GITHUB_PATH"
fi
export PATH="$wrap_dir:$PATH"

podman --version
podman info >/dev/null
echo "rootful podman wrapper is on PATH"
