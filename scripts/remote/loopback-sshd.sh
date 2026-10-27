#!/usr/bin/env bash
# A throwaway sshd on 127.0.0.1 for the live remote-environment tests (OMPOS_LIVE_SSH=1) and for trying a remote profile by hand.
#
# It starts /usr/sbin/sshd in the foreground on a random high port with a temporary host key, a temporary client key as the only
# authorized key, StrictModes no, PasswordAuthentication no and UsePAM no, waits until the port accepts connections, and prints
#   1. the ready line: the ssh command that logs in (starts with `ssh -p <port> -i <key>`),
#   2. one `OMPOS_LIVE_ENV` line with the variables tests/OmpOS.Harness.Remote.Tests/RemoteSshLiveTests.cs reads.
# It runs until it is killed (Ctrl-C or SIGTERM); the temporary directory, the keys and the server go away with it.
#
# Nothing here touches ~/.ssh, ~/.omp or any system configuration. The server accepts only the current user, only the generated
# key, and only from this machine. Do not point it at anything other than a test.
#
# Usage:  scripts/remote/loopback-sshd.sh
#   OMPOS_SSHD=/path/to/sshd         override the sshd binary (default /usr/sbin/sshd)
#   OMPOS_SSH_KEYGEN=/path/ssh-keygen override ssh-keygen (default: from PATH)
set -euo pipefail

SSHD="${OMPOS_SSHD:-/usr/sbin/sshd}"
KEYGEN="${OMPOS_SSH_KEYGEN:-ssh-keygen}"

ATTEMPTS=10
READY_BUDGET_SECONDS=20

for tool in "$SSHD" "$KEYGEN"; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "loopback-sshd: $tool was not found." >&2
    exit 1
  fi
done

user="$(id -un)"
dir="${TMPDIR:-/tmp}/ompos-loopback-sshd.$$.$RANDOM$RANDOM"
mkdir -m 700 "$dir"
pid=""

cleanup() {
  if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi
  rm -rf "$dir"
}
trap cleanup EXIT
trap 'exit 0' INT TERM

# The privilege separation directory sshd insists on when it runs as root.
if [ "$(id -u)" = "0" ]; then
  mkdir -p /run/sshd
fi

"$KEYGEN" -q -t ed25519 -N '' -C ompos-loopback-host -f "$dir/host_key"
"$KEYGEN" -q -t ed25519 -N '' -C ompos-loopback-client -f "$dir/client_key"
cp "$dir/client_key.pub" "$dir/authorized_keys"
chmod 600 "$dir/authorized_keys"

# A random port between 20000 and 59999, retried when the server cannot bind it.
random_port() {
  local value
  value="$(od -An -N2 -tu2 /dev/urandom | tr -d ' ')"
  echo $((20000 + value % 40000))
}

port=""
for _ in $(seq 1 "$ATTEMPTS"); do
  candidate="$(random_port)"
  cat > "$dir/sshd_config" <<EOF
Port $candidate
ListenAddress 127.0.0.1
HostKey $dir/host_key
PidFile $dir/sshd.pid
AuthorizedKeysFile $dir/authorized_keys
AllowUsers $user
StrictModes no
PasswordAuthentication no
KbdInteractiveAuthentication no
PubkeyAuthentication yes
PermitRootLogin prohibit-password
UsePAM no
PermitUserEnvironment no
X11Forwarding no
LogLevel ERROR
EOF
  "$SSHD" -t -f "$dir/sshd_config"
  "$SSHD" -D -e -f "$dir/sshd_config" &
  pid=$!
  deadline=$((SECONDS + READY_BUDGET_SECONDS))
  while [ "$SECONDS" -lt "$deadline" ]; do
    if ! kill -0 "$pid" 2>/dev/null; then
      break
    fi
    if { exec 3<>"/dev/tcp/127.0.0.1/$candidate"; } 2>/dev/null; then
      exec 3>&-
      port="$candidate"
      break
    fi
    sleep 0.2
  done
  if [ -n "$port" ]; then
    break
  fi
  wait "$pid" 2>/dev/null || true
  pid=""
done

if [ -z "$port" ]; then
  echo "loopback-sshd: sshd did not start on any of $ATTEMPTS random ports." >&2
  exit 1
fi

# The client trusts exactly the host key generated above: known_hosts is built from it, never learned from the network.
printf '[127.0.0.1]:%s %s\n' "$port" "$(cut -d' ' -f1,2 "$dir/host_key.pub")" > "$dir/known_hosts"


echo "ssh -p $port -i $dir/client_key -o UserKnownHostsFile=$dir/known_hosts -o StrictHostKeyChecking=yes -o BatchMode=yes $user@127.0.0.1"
echo "OMPOS_LIVE_ENV OMPOS_LIVE_SSH=1 OMPOS_LIVE_SSH_PORT=$port OMPOS_LIVE_SSH_KEY=$dir/client_key OMPOS_LIVE_SSH_KNOWN_HOSTS=$dir/known_hosts OMPOS_LIVE_SSH_USER=$user"

wait "$pid" || true
