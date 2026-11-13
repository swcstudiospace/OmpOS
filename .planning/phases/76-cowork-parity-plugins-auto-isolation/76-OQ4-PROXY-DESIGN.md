# 76-OQ4-PROXY-DESIGN: Network and abstract sockets for the bubblewrap session

Status: DESIGN ONLY. No source, test, fixture or planning-state file is changed. `--unshare-net` is NOT implemented. Nothing here was built or run; every claim about bwrap, the kernel, clients, pasta or omp that the repo does not prove is marked [INFERENCE].

Scope: `src/Autonogrammer.Harness.Isolation` (Linux bubblewrap path). Seatbelt and Windows are out of scope.

Question: `76-SEC-MOUNT-DESIGN.md` OQ4 asks whether to accept unrestricted network and abstract sockets, or to add `--unshare-net` with a proxy design. This document answers "what would the proxy design be", so the owner can decide.

## 0. Constraints already decided

- Owner decision (this unit): draft the proxy design only. Do not implement `--unshare-net`.
- `76-CONTEXT.md:47` (D-20): network is not isolated; `network: unrestricted` always shows; no `--unshare-net`, no egress proxy, no seccomp. Items are deferred under OQ-452 (`76-CONTEXT.md:108`).
- Implementing this design therefore reopens OQ-452, a locked phase decision. The owner must lift D-20 for that scope. See section 9, Q1.
- SEC design P3 (`76-SEC-MOUNT-DESIGN.md:49`): no reachable socket may remain connectable except by approved exception. Abstract-namespace sockets are the one class no mount hides; only a network namespace does.

## 1. What the repo does today (verified in source)

| Fact | Where |
|---|---|
| Session argv unshares pid, ipc and uts only. No `--unshare-net`. | `BubblewrapArgvBuilder.cs:68-70` |
| Probe self-test also has no network flag. | `IsolationProbe.cs:10` |
| Socket denies are path masks only (`/run/user`, docker sockets). | `IsolationDeniedDirectories.cs:10`, SEC design 2 P3 |
| The agent process talks to the host over stdin and stdout, one JSON object per line. | `AcpStdioTransport.cs:14` |
| The engine environment allow-list passes `HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY` and lower-case forms into the child. | `EngineEnvironmentPolicy.cs:33-38` |
| The UI says `network: unrestricted` in three places. | `IsolationChipModel.cs:14,48`, `SettingsCopy.cs:69`, `PermissionStatementModel.cs:10` |

Consequences:

- The stdio control channel does not need the network. A new network namespace should not break the host-to-agent channel. [INFERENCE: inherited pipe file descriptors survive `unshare(CLONE_NEWNET)`.]
- A host-side `HTTPS_PROXY` such as `http://127.0.0.1:3128` is passed through today and works, because the child shares the host network namespace. After `--unshare-net`, that loopback address points into the empty namespace. This is a regression to design for, not a hypothetical. [INFERENCE: omp honours the proxy variables.]

## 2. What is open today

Egress is unrestricted, and so is every abstract-namespace unix socket on the host. Abstract sockets are addressed by name (leading NUL byte, shown as `@name`) and are scoped to the network namespace, not the filesystem. A read-only root and a `--tmpfs` mask do not hide them.

Likely reachable examples [INFERENCE: depends on the desktop and distribution]:

- a session D-Bus address using `unix:abstract=`
- X11 abstract sockets (`@/tmp/.X11-unix/X<n>`)

If the session can reach a host session bus, it can call services that the filesystem masks do not block. This is the gap OQ4 names. It is not a path-based gap; only a network namespace closes it.

## 3. Options

### Option A: proxy inside the sandbox with an allow-list

As worded in the owner question, this does not work by itself. With `--unshare-net` the sandbox namespace has no route out. A proxy started inside it has nothing to forward to, so it can enforce nothing. [INFERENCE: bwrap's `--unshare-net` gives a fresh namespace with at most a loopback interface.]

The only way to make an in-sandbox proxy useful is to give the namespace an uplink (a veth pair or a tap). That is option D below, and it adds a host component. So option A reduces to option D.

### Option B: host-side proxy reached over a bound unix socket

Shape:

1. The host starts one egress proxy per session, outside the sandbox, in the host network namespace.
2. The proxy listens on a unix socket in a per-session directory under the session's scratch area (not `/tmp`; see section 6).
3. `--unshare-net` is added to the session argv, and the socket directory is bound into the sandbox at the same path (the same-path convention already used for grants, `BubblewrapArgvBuilder.cs`).
4. Inside the sandbox, a small bridge process listens on `127.0.0.1:<port>` in the new namespace and forwards bytes to the bound unix socket. [INFERENCE: the bridge must be shipped with the app or be a known binary. It cannot assume `socat`.]
5. The child is started with `HTTPS_PROXY` and `HTTP_PROXY` pointing at the bridge. The proxy speaks HTTP `CONNECT` and plain HTTP forward requests.
6. The proxy resolves each destination host itself, checks the resolved address against the deny ranges (section 5), connects, and copies bytes. TLS is not terminated, so certificate trust is unchanged.

Advantages: the policy and its audit trail live on the host, in the app; no kernel network configuration is needed; the bridge is the only sandbox-side code.

Costs: the bridge is a new binary to ship and version; any client that ignores proxy variables gets no network at all (fail closed); DNS does not work inside the namespace unless the client sends hostnames through `CONNECT`. [INFERENCE: most HTTP clients do; raw resolvers and `ssh` with `ProxyCommand` need configuring.]

### Option C: no proxy

Two sub-cases:

- C1: keep the status quo. The chip stays `network: unrestricted`. Abstract-socket exposure stays open and must be recorded as a known limit (P3 remains incomplete).
- C2: `--unshare-net` alone. Closes abstract sockets and all egress in one step. It breaks model API access and every network tool, so it is not usable for omp without option B or D. Not recommended on its own.

### Option D (not requested; named because options A and B depend on it)

A user-mode network stack with NAT and filtering (for example pasta or slirp4netns) gives the namespace a tap interface, so ordinary clients work without environment variables and DNS resolves normally. [INFERENCE: pasta and slirp4netns are not in the repo and are not verified on the supported host.] This adds an external tool dependency and a second supported-binary matrix (OQ7 currently names only bwrap 0.9.0). It is the better long-term path if the owner wants transparent egress.

## 4. Trade-offs for omp

| Concern | A (as worded) | B (bridge + proxy) | C1 (status quo) | C2 (`--unshare-net` alone) | D (tap + filter) |
|---|---|---|---|---|---|
| Model API calls | not viable | works if omp honours `HTTPS_PROXY` [INFERENCE] | works | fails closed | works [INFERENCE] |
| Abstract sockets closed | n/a | yes | no | yes | yes |
| Raw TCP tools (git ssh, custom clients) | n/a | need `ProxyCommand` or fail | work | fail | work |
| UDP (DNS, QUIC) | n/a | blocked; QUIC falls back to TCP [INFERENCE] | works | blocked | filterable |
| Existing host `HTTPS_PROXY` | n/a | must be re-pointed to the bridge | works | breaks | must be routed [INFERENCE] |
| New shipped component | n/a | bridge binary | none | none | pasta or slirp4netns |
| Audit of destinations | n/a | proxy log, host side | none | none | filter log |

## 5. What the proxy must allow and deny

Allow (default allow-list, owner to confirm):

- The model provider endpoints omp is configured to call. The list must come from configuration, not from code constants. Each entry is an exact hostname with port 443.
- Hosts named in the user's enabled plugin or MCP server configuration, when they are remote. [INFERENCE: those servers run on the host or in the sandbox; only remote ones need egress.]
- Git hosts and package registries, only if the owner adds them. Default off.

Deny (always, regardless of the allow-list):

- Loopback addresses (`127.0.0.0/8`, `::1`) on the host, so the sandbox cannot reach host services through the proxy.
- Link-local and cloud metadata (`169.254.0.0/16`, `fe80::/10`).
- Private ranges (`10/8`, `172.16/12`, `192.168/16`, `fc00::/7`) unless the owner adds a specific entry.
- Any `CONNECT` to an IP literal that is not on the allow-list.
- Any hostname whose resolved address falls in a denied range (DNS rebinding check). The check must use the resolved address the proxy actually connects to, not a second lookup.
- Plain HTTP without an explicit allow entry.

Proxy behaviour:

- Do not terminate TLS. Do not alter certificate handling. The existing CA variables (`NODE_EXTRA_CA_CERTS`, `SSL_CERT_FILE`, `SSL_CERT_DIR`, `EngineEnvironmentPolicy.cs:30-32`) keep working unchanged.
- Log each allowed and denied destination (host, port, decision) to the host session log. Do not log request bodies or headers.
- Enforce connection limits and idle timeouts so a stuck client cannot hold the proxy open. [INFERENCE: values to be set by the owner.]

## 6. How the proxy socket is exposed

- Location: a per-session directory under the session scratch path, created by the host with mode `0700`. The socket inside it has mode `0600`. Not `/tmp`: the host `/tmp` is not bound (OQ3, owner decision) and the sandbox `/tmp` is a fresh tmpfs, so a socket there is unreachable by design.
- Binding: the directory is bound into the sandbox at the same absolute path, so the path in the environment variables matches on both sides. Bind the directory, not the socket file, so the mount point is inside a writable scratch path. [INFERENCE: a file bind onto a read-only root can fail for the reason SEC design 1.4(i)-(ii) describes. Binding a directory under scratch avoids that class.]
- Lifetime: created before the sandbox starts, removed when the session ends. A stale socket from a crashed run must be unlinked before bind.
- Identity: the proxy checks `SO_PEERCRED` on each accepted connection and refuses peers whose uid differs from the session uid. [INFERENCE: peer credentials are visible across the bind because `--unshare-user` is not used by the session.] This does not stop other processes of the same user on the host from connecting, because the file mode permits the owner. Whether that is acceptable is owner question Q4.
- Process isolation: the proxy runs as a separate host process, not inside the sandbox, so a sandboxed process cannot alter its policy. [INFERENCE: the sandbox cannot signal host processes outside its pid namespace; the proxy is not inside it.]

## 7. Interaction with SEC-MOUNT-DESIGN

- P3 remains incomplete for abstract sockets until option B or D is live. Option C1 leaves it incomplete by design. The chip copy must then keep saying so.
- The session canary (SEC design section 4) must gain a network check if B or D is adopted: the child cannot connect to a host loopback port, and it can reach an allowed host through the proxy. Each check must be an exact argv, the same argv that is launched.
- The probe (`IsolationProbe.cs:10`) does not exercise networking. Under OQ5 it stays as it is; the canary carries the network check.
- Step 3 of the SEC design mount order gains one bind (the socket directory). It belongs in the grant group, after the scratch bind, because it is a scratch-area path.
- The three `network: unrestricted` copies (section 1) must change together if the network becomes restricted. A chip that says "unrestricted" while the namespace is unshared would be false in the other direction.

## 8. Verification to run later (not run now)

These are the controls the owner would run after implementation, all on a host with bwrap 0.9.0 (OQ7):

- N-net1: from inside the sandbox, a connection to a host loopback port fails.
- N-net2: a connection to an abstract socket created on the host (`@test-abstract`) fails.
- N-net3: a `CONNECT` to an allowed host succeeds through the bridge; to a denied host, it fails with a logged denial.
- N-net4: a hostname that resolves into a denied range fails (rebinding).
- N-net5: a socket file from another host user is refused by the peer-credential check. [INFERENCE: needs a second account; may be impractical.]
- P-net1: a model API request completes through the bridge with the real provider configuration, using a synthetic fixture home (OQ6).

Each control states its expected result before it runs. None is run by this document.

## 9. Open questions for the owner

- Q1. Lift D-20 and OQ-452 for this scope? This design cannot be built without it. Options: B, D, or keep C1 and record the abstract-socket gap.
- Q2. Which option? Recommendation: B, staged behind a default-off setting, with the chip unchanged until it is live. D is the better end state if transparent egress is required. A is not viable as worded.
- Q3. Allow-list source and default: provider endpoints from configuration only, or also plugin and MCP hosts? Do any git hosts or registries need default access?
- Q4. Same-user host processes can use a `0600` socket owned by the same uid. Accept, or require a token in the first protocol line?
- Q5. Accept DNS rebinding and TCP-only coverage as the stated limits, or require UDP filtering (option D)?
- Q6. Should the session refuse to start if the proxy cannot start, or degrade to C1 with consent? The SEC design's OQ11 pattern (`session_policy_unenforced`) would apply.
- Q7. Bridge binary: ship a small helper in the app, or use a binary the host already has? Shipping adds a versioned artifact to maintain.
- Q8. Does the owner accept the option D dependency (pasta or slirp4netns) and its bwrap-style version pinning?

## 10. Not verified here

- bwrap behaviour with `--unshare-net` on the supported host, including whether a loopback interface is up in the new namespace [INFERENCE].
- Whether omp honours `HTTPS_PROXY` for every provider it calls [INFERENCE].
- Whether abstract sockets are actually reachable from the current session on a real desktop [INFERENCE].
- Client behaviour for raw TCP tools, `ssh` and DNS inside a namespace with no resolver [INFERENCE].
- pasta and slirp4netns behaviour, availability and versions [INFERENCE].
- The peer-credential check across the bind [INFERENCE].
- Nothing in this document was built, run or tested.
