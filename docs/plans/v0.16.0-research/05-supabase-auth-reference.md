# 05 — Supabase auth reference and desktop sign-in recommendation (R5)

Date: 2026-10-28. Author: recon-wave-5. Read-only study; no code, dashboard or hosted setting was changed.

Evidence tags: **[WEB]** = file read in `/root/src/repos/spectrumwebco-marketing` (cited `path:line`); **[DOC]** = Supabase documentation URL; **[SRC]** = `supabase/auth` (GoTrue) source on `master`, fetched 2026-10-28 (the hosted service may lag master: that is an **[INFERENCE]** wherever a hosted behaviour is claimed from source); **[LIVE]** = value the website repo's docs record as read from the live project on 2026-10-28 (I did not re-read it: no token, no `.env*` opened); **[INFERENCE]** = my reasoning. No secret value was read or is recorded; env names only.

---

## 1. What the website already does (spectrumwebco-marketing)

### 1.1 Project and public identifiers

| Item | Value | Source |
|---|---|---|
| Supabase project (shared with other apps, name "ClippyOS") | ref `xpfoidcvbwmpbwodjvqb` | [WEB] `AUTH-SETUP.md:3`, `docs/auth/owner-checklist.md:5` |
| Project URL | `https://xpfoidcvbwmpbwodjvqb.supabase.co` | derived from ref; [WEB] `AUTH-SETUP.md:104,107` |
| JWT issuer (`iss`) | `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1` | [DOC] https://supabase.com/docs/guides/auth/jwts |
| JWKS | `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1/.well-known/jwks.json`; I fetched it unauthenticated: one key, `alg ES256`, `crv P-256`, `kid b8e7e582-4142-4c1a-954b-971f0fbd43be`, `key_ops [verify]` | fetched 2026-10-28; [WEB] `AUTH-SETUP.md:135` says ES256 |
| Access-token audience | `authenticated` (anon key tokens: `anon`) | [DOC] https://supabase.com/docs/guides/auth/jwt-fields |
| Key model in use | env `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` (a publishable key; the e2e dummy is `sb_publishable_…`) | [WEB] `lib/supabase/config.ts:1-2`, `package.json:12`; whether the live key is `sb_publishable_` or legacy `anon` is **[INFERENCE: not checked]** |
| Libraries | `@supabase/ssr ^0.12.7`, `@supabase/supabase-js ^2.117.2` | [WEB] `package.json:23-24` |

The project is shared: `AUTH-SETUP.md:3` says every project-wide toggle (Phone, rate limits, CAPTCHA, redirect list) "changes them too: coordinate before you touch it". `docs/auth/convex.md:53` adds the authorisation corollary: every signed-in user of *any* app on the project carries a token with the same issuer and audience.

### 1.2 Sign-in methods on `/login`

| Method | Entry | Supabase call | State today |
|---|---|---|---|
| Google, X (OAuth 2.0, provider name `x`) | `app/auth/start/[provider]/route.ts:16-46` (server-side, PKCE) | `signInWithOAuth` | on [LIVE] (`owner-checklist.md:15`) |
| Email link + code | `components/auth/EmailPanel.tsx:16` | `signInWithOtp({ email, options:{ emailRedirectTo, shouldCreateUser:true }})` | on; email limit **2/hour project-wide** [LIVE] |
| Text code (SMS) | `components/auth/phone/*`; `lib/auth/sms/otp.ts:6-10` | `signInWithOtp({phone})` + `verifyOtp({type:"sms"})` | **Phone provider OFF**, Send SMS hook ON [LIVE]; Australian mobiles only (`lib/auth/sms/phone.ts:12`) |
| Wallets (Ethereum, Solana) | `lib/auth/wallet/solana.ts:137` etc. | `signInWithWeb3` | on [LIVE] |
| Recovery | `app/api/auth/recovery/route.ts` | `admin.generateLink` (service role, if set) else `resetPasswordForEmail` | n/a for desktop |

Which keys render is computed from `/auth/v1/settings` by `lib/auth/core/methods.ts:79-108` (cache 300 s). No passwords are offered; "Confirm email" is on (`AUTH-SETUP.md:59`).

### 1.3 Redirect, callback and PKCE handling

- Cookie sessions through `@supabase/ssr`, flow type PKCE (`docs/auth/architecture.md:42`). Verifier cookie `sb-<project ref>-auth-token-code-verifier` lives between start and callback (`architecture.md:43`). Server client: `lib/supabase/server.ts:8-24` (cookie sink copies no-store headers onto redirects). `proxy.ts:13-23` refreshes the session via `supabase.auth.getUser()` on `/account`, `/guides`, `/login`, `/auth/*`, `/api/account/*` (matcher `proxy.ts:28`).
- `app/auth/callback/route.ts:14-50`: reads `code`, `exchangeCodeForSession(code)`, falls back to `getUser()` if the URL is replayed, calls `ensureMember`, redirects to `next` (default `/account`, validated by `safeNext` = same-origin path only, `lib/http.ts` / `lib/login-entry.ts:38-50`). Shared by Google, X, email link, recovery.
- `app/auth/confirm/route.ts:8-19`: `GET ?token_hash&type&next` -> `verifyOtp({token_hash,type})` on the server -> `ensureMember` -> redirect. This is the branded email-link target.
- `app/auth/signout/route.ts` (15 lines) — website sign-out (not read in detail; see §3.4 for why desktop must NOT copy the default scope).
- Redirect origin: `lib/http.ts` `origin()` = `NEXT_PUBLIC_SITE_URL || request origin`. Production uses `https://www.spectrumweb.co`; Preview must leave `NEXT_PUBLIC_SITE_URL` unset so cookie host and callback host agree (`AUTH-SETUP.md:145`).
- Dashboard URL configuration the site expects (`AUTH-SETUP.md:119-124`): Site URL `https://www.spectrumweb.co`; Redirect URLs `https://www.spectrumweb.co/auth/callback`, `/auth/confirm`, `https://www.spectrumweb.co/**`, `http://localhost:3000/**` and preview patterns; "leave in place the URLs other apps on this shared project already use".

### 1.4 The two Supabase hooks

**Send Email hook** (`app/api/auth/email-hook/route.ts:14-53`, URL `https://www.spectrumweb.co/api/auth/email-hook`, env `SEND_EMAIL_HOOK_SECRET`, `RESEND_API_KEY`, `EMAIL_FROM`):
- Verifies Standard Webhooks HMAC-SHA256 over `id.timestamp.body`, 5-minute skew (`lib/webhook.ts`; `AUTH-SETUP.md:33`). Secrets rotate with `|` separator.
- **Project-level, always calls production**, so a desktop sign-in on the shared project goes through this hook and Resend as "Spectrum Web Co <ove@spectrumweb.co>" (`AUTH-SETUP.md:36,154`). While it is on, Supabase does not use SMTP or dashboard templates (`AUTH-SETUP.md:62`).
- Link construction `lib/auth-email.ts:125-137` (`authActionLink`): if `redirect_to` is missing or on the site origin -> `https://www.spectrumweb.co/auth/confirm?token_hash=…&type=…&next=…`; **any other origin -> the Supabase verify URL** `https://<ref>.supabase.co/auth/v1/verify?token=<token_hash>&type=…&redirect_to=…`. So a desktop `redirect_to` (loopback) is honoured by the hook.
- Templates: `magiclink`/`email` -> `MagicLink` shows **button + code (`token`)** (`lib/auth-email.ts:196-205`, `emails/auth/MagicLink.tsx`); **`signup` -> `ConfirmSignup` shows a button only, NO code** (`lib/auth-email.ts:185-194`, `emails/auth/ConfirmSignup.tsx`, 28 lines, props `confirmationUrl` only). See finding F1.
- `reauthentication` -> `VerificationCode` (code only).

**Send SMS hook** (`app/api/auth/sms-hook/route.ts`, URL `…/api/auth/sms-hook`, env `SEND_SMS_HOOK_SECRET`, `AUTH_SMS_SENDER`, `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_FROM`): signature check, code shape `^\d{4,10}$`, **refuses non-Australian numbers** (`lib/auth/sms/phone.ts:12`, `route.ts` "country" step), sends via Twilio. Hook budget 5 s (`route.ts:12`). Phone provider is OFF so no SMS code is sent today; with the hook on, Supabase ignores its own `sms_provider` (`AUTH-SETUP.md:89`).

### 1.5 What exists after first sign-in

- Supabase creates `auth.users` (+ `auth.identities`) itself on first OTP/OAuth/web3 sign-in. `shouldCreateUser:true` on the website's email panel.
- `ensureMember` (`lib/members.ts:59-85`) runs **only** in `/auth/callback`, `/auth/confirm`, or on the `/account` render for phone/wallet (`architecture.md:34-35`): read `site_profiles` row; if absent, `upsert({id, display_name}, {ignoreDuplicates:true})`; the call that creates the row also sends the welcome mail (`mailWelcome`) when the user has an email. It never throws and never blocks sign-in.
- Tables/RLS: `supabase/migrations/20251003140000_site_core.sql:87-94` `site_profiles(id uuid pk references auth.users on delete cascade, display_name, github_username, newsletter, …)`; RLS on (`:107-112`); policies `select/insert/update to authenticated using (auth.uid() = id)` (`:117-128`); `site_repo_requests` own-row select/insert with `status='pending'` (`:130-136`); `site_config`, `site_contacts`, `site_meetings`, `site_newsletter` have no policies (deny-all, reached via `site_*` SECURITY DEFINER RPCs). **No trigger on `auth.users` exists in this repo's migration** (grep of `auth.users`/`trigger`: only the two FKs) — other apps' triggers on the shared project are **unknown [INFERENCE: cannot be read from this repo]**.
- Consequence: a desktop-only sign-in creates an `auth.users` row but no `site_profiles` row and no welcome mail until the person visits the website. Identity linking is **off** (`security_manual_linking_enabled=false` [LIVE], `architecture.md:57`), so the same person signing in by email, Google, X, phone or wallet gets **separate users**.
- `user_metadata` is user-editable and never decides access (`architecture.md:56`). Same rule must hold for Autonogrammer.

### 1.6 Live project settings recorded by the website docs (2026-10-28, [LIVE], not re-verified)

`external_phone_enabled` false; `hook_send_sms_enabled` true; Send Email hook on; `rate_limit_email_sent` **2/h**; `rate_limit_sms_sent` 30/h; CAPTCHA off; manual linking off; SMS OTP 6 digits/300 s; **email OTP 8 digits/3600 s**; `external_x_email_optional` on (`owner-checklist.md:13-26`). The read-only checker is `node --env-file=.env.local scripts/auth-doctor.mjs` (`AUTH-SETUP.md:21`); its `config.*` checks need `SUPABASE_ACCESS_TOKEN` in a local shell.

### 1.7 Env variable NAMES (website)

Public: `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY`, `NEXT_PUBLIC_SITE_URL`, `NEXT_PUBLIC_TURNSTILE_SITE_KEY`, `NEXT_PUBLIC_REOWN_PROJECT_ID`. Server: `SEND_EMAIL_HOOK_SECRET`, `RESEND_API_KEY`, `EMAIL_FROM`, `SEND_SMS_HOOK_SECRET`, `AUTH_SMS_SENDER`, `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_FROM`, `SUPABASE_SERVICE_ROLE_KEY` (recovery route only), `SITE_SERVER_SECRET` (Convex path). Doctor only: `SUPABASE_ACCESS_TOKEN` (never in Vercel).

---

## 2. Current Supabase Auth (GoTrue) REST reference

Base: `https://<ref>.supabase.co/auth/v1`. Every call needs header `apikey: <publishable key>` (OpenAPI `APIKeyAuth`); user-scoped calls also `Authorization: Bearer <access_token>`. Publishable/secret keys are not JWTs: send them on `apikey`, not as Bearer ([DOC] https://supabase.com/docs/guides/getting-started/api-keys). Sources for this section: OpenAPI `https://github.com/supabase/auth/blob/master/openapi.yaml` and handlers under `internal/api/` ([SRC]).

| Endpoint | Request | Behaviour that matters to us |
|---|---|---|
| `POST /otp` | JSON `{email}` **or** `{phone, channel?: sms\|whatsapp}`; `create_user` (default **true**, `internal/api/otp.go:63-65`); `data`; `code_challenge` + `code_challenge_method` (`s256`\|`plain`; both or neither, `pkce.go:67-80`; length/charset RFC 7636); `gotrue_meta_security.captcha_token`. Redirect: query/form `redirect_to` or header `redirect_to` (`internal/utilities/request.go:142-153`) | 200 `{}` for email (same answer whether the user exists); phone returns `message_id`. 422 `otp_disabled` when `create_user=false` and user absent. 400 `phone_provider_disabled` if Phone provider off (`otp.go:107-108`). 429 on limits. A `code_challenge` selects the PKCE flow (`magic_link.go:69`). |
| `POST /verify` | JSON `{type, email\|phone, token}` **or** `{type, token_hash}` (exactly one of token/token_hash, `verify.go:64`); optional `redirect_to`. `type` values accepted by the handler: `email` (any email OTP: tries signup then magic-link token, `verify.go:676-690,739-748`), `signup`, `invite`, `recovery`, `magiclink`, `email_change`, `sms`, `phone_change` | **POST returns a session directly** (`AccessTokenResponse`: `access_token`, `token_type:"bearer"`, `expires_in`, `expires_at`, `refresh_token`, `user`) — no PKCE verifier needed, even if the OTP was requested with a `code_challenge` (accepted because `"pkce_"+token` is compared, `verify.go:793`). Wrong/expired -> 403 `otp_expired`. Note the OpenAPI enum omits `email`; the handler accepts it and supabase-js sends it. |
| `GET /verify?token&type&redirect_to` | The clickable link. `token` = token hash | Implicit flow: 302 to `redirect_to#access_token=…&refresh_token=…` (a **fragment** — a loopback HTTP listener cannot read it). PKCE flow (token hash starts `pkce_`): 302 to `redirect_to?code=<auth code>` (`verify.go:137,190-220`). Invalid `redirect_to` -> falls back to Site URL (`request.go:75-91`). |
| `POST /token?grant_type=pkce` | `{auth_code, code_verifier}` | Exchanges the `code` from the redirect (valid 5 min, once: [DOC] https://supabase.com/docs/guides/auth/sessions/pkce-flow) for a session. |
| `POST /token?grant_type=refresh_token` | `{refresh_token}` (legacy 12-char or newer longer format, `token_refresh.go:20-35`) | New access + refresh token. Refresh tokens never expire but are single-use; **reuse interval 10 s by default**; reusing a revoked parent outside it terminates the whole session ([DOC] https://supabase.com/docs/guides/auth/sessions "refresh token reuse detection"). Limit: 150 req/5 min/IP, bursts 30 ([DOC] https://supabase.com/docs/guides/auth/rate-limits). CAPTCHA does not apply to refresh (OpenAPI). |
| `POST /logout?scope=global\|local\|others` | `Authorization: Bearer <access_token>`; 204 | **Default scope is `global` = all sessions of the user** (`logout.go:21-67`; omitted/`""` -> global). `local` = this session; `others` = all except this. Invalid scope -> 400. |
| `GET /user` | Bearer | Server-side token validation (round trip). |
| `GET /settings` | `apikey` | Public provider flags (what the website's `authMethods()` reads). |
| `GET /.well-known/jwks.json` | none | Public signing keys. |

### 2.1 Access-token claims ([DOC] https://supabase.com/docs/guides/auth/jwt-fields)

Always present: `iss`, `aud`, `exp`, `iat`, `sub` (user UUID), `role` (`authenticated`/`anon`/`service_role`), `aal` (`aal1`/`aal2`), `session_id`, `email`, `phone`, `is_anonymous`. Optional: `jti`, `nbf`, `app_metadata` (e.g. `{provider}`), `user_metadata` (user-editable, untrusted), `amr` = array of `{method, timestamp}` with methods `oauth`, `password`, `otp`, `totp`, `recovery`, `invite`, `sso/saml`, `magiclink`, `email/signup`, `email_change`, `token_refresh`, `anonymous`. Header: `alg` (here ES256), `kid`, `typ JWT`. A Custom Access Token Hook can add claims (not enabled per any record I read).

### 2.2 JWKS validation ([DOC] https://supabase.com/docs/guides/auth/signing-keys)

Validate locally: signature via JWKS `kid`, `alg` pinned to `ES256` (never `HS256`; the doc "strongly recommend[s] against" shared-secret verification), `iss` == project issuer, `aud` == `authenticated`, `exp`/`nbf`, then reject `is_anonymous=true` unless wanted. Edge cache of the JWKS is 10 min, client libs may cache another 10 min, caches cleared every 20 min; revocation is instant only for Supabase's own products, so an API that caches the JWKS needs its own cache-bust on unknown `kid` (same doc). Official C# client exposes `GetClaimsAsync` (see §2.6).

### 2.3 OTP expiry, length and limits

| Setting | Value | Source |
|---|---|---|
| Email OTP / magic link expiry | default 3600 s; **one setting governs OTP, magic links, signup, recovery, invite, email-change links**; >86 400 s discouraged | [DOC] https://supabase.com/docs/guides/auth/auth-email-passwordless; live 3600 s [LIVE] |
| Email OTP length | docs say six digits; **live is 8** [LIVE] — UI must accept 6–10 digits, never hard-code 6 | [WEB] `owner-checklist.md:24` |
| SMS OTP | doc default expiry 1 h; **live 300 s, 6 digits** [LIVE] | [DOC] https://supabase.com/docs/guides/auth/phone-login; [WEB] `sms-decision.md` |
| Per-user resend window | 60 s (`/otp`) | [DOC] rate-limits |
| Emails sent by Auth | **project-wide**; 2/h built-in; configurable with custom SMTP or Send Email hook; live **2/h** | [DOC] rate-limits; [LIVE] |
| SMS sent | project-wide; default 30/h; live 30/h | same |
| Sign-up/sign-in endpoints (`/signup /recover /resend /magiclink /otp /user`) | per IP, 30 / 5 min, burst 30 | same |
| `/verify` | per IP, 30 / 5 min | same |
| `/token` (password, refresh, ID token, PKCE) | per IP, 150 / 5 min, burst 30 | same |
| Web3 | per IP, 30 / 5 min | same |

429 bodies carry `over_email_send_rate_limit`, `over_sms_send_rate_limit`, `over_request_rate_limit` (website maps these: `lib/auth/core/errors.ts:45-52`). Behind a NAT or a server proxy, per-IP buckets are shared; IP forwarding exists only with a **secret key** + `Sb-Forwarded-For` and must be enabled in the dashboard (rate-limits doc) — not applicable to a desktop calling directly.

### 2.4 API keys

Publishable key (`sb_publishable_…`) is "safe to expose… mobile or desktop app, CLIs"; secret key (`sb_secret_…`) bypasses RLS, must never ship, and a secret key sent from a browser User-Agent gets 401 ([DOC] https://supabase.com/docs/guides/getting-started/api-keys). Legacy `anon`/`service_role` JWT keys are deprecated "by the end of 2026" and work in parallel with the new ones until deactivated. Desktop and web heads embed the publishable key and project URL only; the Railway API never needs the secret key for verification (JWKS suffices).

### 2.5 Native and desktop guidance

Supabase documents **deep links / custom URL schemes** and universal links for mobile (Expo, Swift, Kotlin, Flutter) and notes Windows protocol registration is manual (`url_protocol`/registry) ([DOC] https://supabase.com/docs/guides/auth/native-mobile-deep-linking; redirect-urls doc "Mobile deep linking URIs": e.g. `com.supabase://**`). The documented native pattern receives `access_token`/`refresh_token` on the URL and calls `setSession`; for PKCE it receives `code` and calls `exchangeCodeForSession`. There is **no Supabase page for loopback desktop flows**; loopback is supported by GoTrue code: `IsRedirectURLValid` accepts any loopback **IP literal** (`127.0.0.1`, `::1`) and skips the port check for localhost, citing RFC 8252 §7.3 (`internal/utilities/request.go:94-125`; stricter referrer check commit 2026-06-01, PR #2479). Whether the hosted service has this exact version is **[INFERENCE]**, so **do not rely on it; add an explicit allow-list entry** (§4). Allow-list wildcard rules: `*` matches non-separator characters, `**` anything; separators are `.` and `/` ([DOC] https://supabase.com/docs/guides/auth/redirect-urls), so `http://127.0.0.1:*/**` matches any port and path (the `:54321` segment contains no separator) — **[INFERENCE]** on the exact host:port match, test before rollout.

### 2.6 C# client option

Official `supabase/supabase-csharp` (monorepo, active: push 2026-10-27, release `v8.5.1` 2026-10-26; the older `supabase-community/gotrue-csharp` is **archived**). NuGet `Supabase.Gotrue` 8.5.1, MIT, netstandard2.1, depends on `System.IdentityModel.Tokens.Jwt 7.5.1` (nuspec). `IGotrueClient` (packages/Gotrue/Gotrue/Interfaces/IGotrueClient.cs) has `SignInWithOtp(SignInWithPasswordlessEmailOptions|PhoneOptions)` (l.218/234), `VerifyOTP(email|phone, token, type)` (l.422/431; **email default type is `MagicLink` — pass the `Email` type**, see F1), `ExchangeCodeForSession(verifier, code)` (l.329), `RefreshSession` (l.124), `SignOut(SignOutScope scope = Global)` (l.406 — **default Global**), `GetClaimsAsync` (l.517). Alternative: ~6 plain `HttpClient` calls against the table in §2; the surface needed is small and avoids pulling the Realtime/Postgrest packages. Choice deferred to implementation (D5).

---

## 3. Findings that change the design

**F1 — First-time email users get a `signup` email with no code.** `magic_link.go:80-130` signs a new (or unconfirmed) user up and the "confirmation email already contains the magic link". The website's hook renders that as `ConfirmSignup` (button only, `emails/auth/ConfirmSignup.tsx`), while an existing user gets `MagicLink` (button + code). So "email code first" works for returning users only, until the template includes `token`. The hook payload does carry `email_data.token` for `signup` (the code is `need()`-ed only for magiclink/email/reauthentication, `lib/auth-email.ts:196-217`). On the server side `type:"email"` verifies both a signup and a magic-link token (`verify.go:739-748`); `type:"magiclink"` would **not** match a signup token (`verify.go:752`). Desktop must therefore use `type:"email"`, and the website hook needs a 2-line change (add the code to `ConfirmSignup`) — a website code change, not a dashboard setting.

**F2 — Default `logout` is global.** `POST /logout` with no scope kills every session of that user, including the website cookie session; the C# `SignOut` also defaults to `Global`. Desktop sign-out must send `scope=local`.

**F3 — The Send Email hook, rate limit and redirect list are project-wide and live on the website.** Every desktop OTP uses the website's Resend hook (branding "Spectrum Web Co") and the shared **2 emails/hour project limit** (live) — one person's desktop sign-in competes with every other app's emails. Raise before launch (owner action O3).

**F4 — Phone/SMS is dark and AU-only.** Phone provider off [LIVE]; the hook rejects non-AU numbers; sender/provider undecided (`sms-decision.md`). Desktop SMS cannot ship before that decision and is not useful outside Australia without a hook change.

**F5 — Magic link on desktop needs PKCE.** Without `code_challenge` the verify redirect puts tokens in the URL fragment, which a loopback HTTP listener never receives. With PKCE it arrives as `?code=`. PKCE flow-state is looked up by user id and auth method at verify time (`issueAuthCode` -> `FindFlowStateByUserID`, `pkce.go:42-45`): **[INFERENCE]** two concurrent PKCE magic-link requests for the same user (website tab + desktop) can cross-wire, and only one outstanding token per type exists per user (`ClearOneTimeTokenForUser`, `models/one_time_token.go:136`), so the newer email invalidates the older. This is why the code path (POST `/verify`, no verifier) is the primary flow.

**F6 — Redirect target mismatch silently falls back to the website.** If `redirect_to` is not allowed, GoTrue redirects to the Site URL (`request.go:75-91`): the person lands on spectrumweb.co with a `code` it cannot use. Harmless (PKCE), confusing.

**F7 — Shared project means a valid JWT is not an Autonogrammer account.** Any ClippyOS or website user holds a valid ES256 token with `aud=authenticated`. The Railway API must authorise via its own account/membership table keyed by `sub`, never by "token verified" (same warning as `convex.md:53`). `user_metadata` is untrusted. Treat `is_anonymous` and `amr` as inputs, not permissions.

**F8 — No identity linking.** Desktop email sign-in and website Google sign-in for the same person are two users. Product must say so or enable linking (decision D6); linking needs `linkIdentity` product work that is not built on the site (`owner-checklist.md:70`).

**F9 — First sign-in on desktop skips the website's `ensureMember`.** No `site_profiles` row, no welcome mail until the website is visited. Harmless to the website (it creates the row on first visit); Autonogrammer should keep its own `accounts` row, created by its API on first authenticated call.

**F10 — CAPTCHA, if ever enabled, breaks desktop too.** CAPTCHA is project-wide (`AUTH-SETUP.md:131`); desktop would then have to send `gotrue_meta_security.captcha_token` (no Turnstile widget in a native shell without a WebView).

**F11 — Email OTP length is 8 live.** Fixed 6-digit input would fail; accept 6–10 digits and read length from the first send.

---

## 4. Recommended desktop sign-in flows

Common: Avalonia/Blazor heads call Supabase Auth directly with the publishable key (no secret in any client); the **Autonogrammer API (Railway) validates the access token** (JWKS, §2.2) and owns account authorisation (F7). Session = `{access_token, refresh_token, expires_at}`; refresh at ~80% of `expires_in` through `grant_type=refresh_token`, serialise refreshes (one in flight; the 10 s reuse window forgives races, not duplicates); store the refresh token in the OS keyring (never in logs, URLs, or the omp engine's config); if refresh returns an invalid/revoked token, drop the session and sign in again.

### Flow A (primary): email OTP code
1. `POST /otp` `{email, create_user:true, code_challenge, code_challenge_method:"s256"}` with `redirect_to=http://127.0.0.1:<port>/auth/callback` (query param). Sending a PKCE challenge makes the email's link usable on the same desktop (Flow B) without hurting the code.
2. UI: email -> code entry (6–10 digits, F11) with 60 s resend countdown, attempt limit, expiry text from the live OTP expiry setting.
3. `POST /verify` `{type:"email", email, token}` -> session. Treat 403 `otp_expired`, 429, 422 `otp_disabled` (only if `create_user:false`).
4. Preconditions: **F1 template fix** (else new users see no code) and **raised email rate limit** (F3). Until F1 ships, new users must click the link (Flow B).

### Flow B (second): magic link via loopback PKCE
1. Generate verifier (43–128 chars) + S256 challenge; start an HttpListener on `127.0.0.1:<random port>` (RFC 8252 §7.3; bind loopback only, one-shot, ~10 min lifetime, random `state`-like path segment checked on callback).
2. `POST /otp` as in A. The email button (hook) points at `…/auth/v1/verify?token=pkce_…&type=…&redirect_to=http://127.0.0.1:<port>/…` (hook rule, §1.4).
3. Listener receives `?code=`; respond with a small "you can close this tab" page; `POST /token?grant_type=pkce` `{auth_code, code_verifier}`; 5-minute single-use code.
4. If the link is opened on another device the loopback is unreachable: the user falls back to the code (A). Keep both visible on the same screen.

### Flow C (third): SMS code
`POST /otp` `{phone}` -> `POST /verify` `{type:"sms", phone, token}` -> session. Blocked until D3/D4 (provider, sender, Phone toggle, AU-only scope) are decided. Do not build UI that promises international SMS.

### Flow D (optional, later): Google/X
Same loopback pattern with `GET /authorize?provider=google&redirect_to=http://127.0.0.1:<port>/…&code_challenge=…&code_challenge_method=s256` in the system browser; the website's providers and X credit balance are shared dependencies. Not requested; listed because the loopback allow-list from Flow B covers it.

### Sign-out and device model
`POST /logout?scope=local` (F2). "Sign out everywhere" is an explicit separate action (`scope=global`) and also logs the website out. Dispatch (desktop/mobile/web) uses one Supabase user with several sessions (each desktop gets its own `session_id`); keep the dashboard option "Single session per user" **off** so devices coexist. Time-box and inactivity-timeout options exist under Authentication → Sessions ([DOC] https://supabase.com/docs/guides/auth/sessions); their plan availability was not checked. If the owner enables them, a failed refresh must lead to a normal sign-in prompt.

### API-side validation (Railway)
Fetch `/.well-known/jwks.json` (cache ≤10 min, refresh on unknown `kid`), accept only `ES256`, check `iss` = `https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1`, `aud=authenticated`, `exp`; map `sub` -> `autonogrammer_accounts` row (create on first call, `amr`/`email`/`phone` stored as display only); reject `is_anonymous`. Keep the verification config (issuer, JWKS URL) as environment configuration, not code, so a project move (see `SUPABASE-CONSOLIDATION.md`, which already moved the site between projects) needs no release. [WEB] `SUPABASE-CONSOLIDATION.md:1-40` shows the site was consolidated onto this project, i.e. the issuer *has changed before*.

---

## 5. Owner changes in the shared project, and exact risk to the website

| # | Change (where) | Needed for | Risk to the website / other apps |
|---|---|---|---|
| O1 | **Add redirect URLs** (Authentication → URL Configuration): `http://127.0.0.1:*/**` (and `http://[::1]:*/**` if IPv6 loopback is used). Keep all existing entries. | Flow B/D loopback | Allow-list is additive; it affects only which `redirect_to` values are honoured. Exposure is a local process on a user's own machine receiving a PKCE `code` that is useless without the verifier. Does **not** change the Site URL, so existing website flows are unaffected. Without it (hosted version unknown, §2.5) the redirect falls back to the website (F6). |
| O2 | (Optional) custom scheme URL, e.g. `autonogrammer://auth/**`, for a mobile/Dispatch head | Mobile deep link | Additive, same as O1. Not needed for desktop loopback. |
| O3 | **Raise `rate_limit_email_sent`** well above 2/h (Authentication → Rate Limits) — the website's own checklist item 5 already asks for it (`owner-checklist.md:56-60`; doctor warns below 10) | Any real email sign-in | Higher volume through the **website's Resend account** (cost, deliverability, abuse surface); less protection against email-bombing the shared Resend quota. Shared with other apps: coordinate the number. The per-IP and per-user (60 s) limits stay as the other brake. |
| O4 | **Edit the website's `emails/auth/ConfirmSignup.tsx` + `lib/auth-email.ts`** so the `signup` email also shows `email_data.token` (code) | Flow A for first-time users (F1) | A website code change + deploy (not a dashboard toggle). Risk: copy change on the signup email; token already present in the payload, nothing new is sent to Resend. Hook remains signature-checked. If the owner prefers not to touch the website, Flow A is returning-users-only and new users use the link (Flow B). |
| O5 | (Only if SMS is wanted) decide provider and sender, set Vercel env names, then switch **Phone provider ON** *after* the hook secret/sender are live (`owner-checklist.md:9`, items 2–4); decide whether to widen the AU-only list (`lib/auth/sms/phone.ts:12`) | Flow C | Project-wide toggle: switching Phone on before hook and sender are live makes **every** text request fail with 500 for all apps; real money per SMS; SMS-pumping fraud if the country list is widened carelessly. Lower `rate_limit_sms_sent` to ~10/h first. |
| O6 | **Do NOT enable CAPTCHA** (Attack Protection) for this work | n/a | Project-wide; breaks every email/phone/password sign-in that sends no token (`AUTH-SETUP.md:131`), including the website unless `NEXT_PUBLIC_TURNSTILE_SITE_KEY` is set and deployed. |
| O7 | **Leave Site URL, hooks, providers, JWT signing keys and manual linking untouched.** Do not rotate the ES256 key, do not "Migrate JWT secret", do not switch the email hook off | n/a | Rotating or revoking signing keys breaks verification caches for the website, other apps and the new API (JWKS cache 10–20 min, §2.2). Turning the Send Email hook off silently falls back to the dashboard SMTP/templates (`AUTH-SETUP.md:62`). |
| O8 | (Optional) Authentication → Sessions: keep "Single session per user" **off** | multi-device Dispatch | The doc says that option keeps only the most recent sign-in's session active and terminates the rest ([DOC] sessions); **[INFERENCE]** enabling it could sign the website session out whenever a desktop signs in. |
| O9 | Read-only: have the owner run `auth-doctor.mjs` with `SUPABASE_ACCESS_TOKEN` in a local shell before and after to confirm `config.*` checks (email rate, redirects, hooks) | Verification | None (read-only, never writes). |

Net website risk if only O1 and O3 are done: very low (additive allow-list entry + higher email limit). The only item that can break production sign-in is O5 done out of order, O6, or touching O7.

---

## 6. Decisions needed and gating

| ID | Decision | Owner | Gates |
|---|---|---|---|
| D1 | Use the shared "ClippyOS" project `xpfoidcvbwmpbwodjvqb` for Autonogrammer accounts (vs. a separate Supabase project)? Shared = one user table, one Resend hook, one rate-limit pool, no cross-app isolation (F3, F7). A separate project removes every risk in §5 but cannot share accounts with the website. | Owner | Everything in §4; the Supabase project ref/issuer is configuration either way |
| D2 | Approve O1 (loopback redirect entry) and O3 (raise email rate limit, number to be chosen) | Owner (dashboard) | Flow A/B in production |
| D3 | Approve O4 (change website signup email to include the code) or accept link-only for first-time users | Owner (website repo/Vercel deploy) | Flow A for new users |
| D4 | SMS: provider, sender, AU-only vs wider, Phone toggle order (O5) | Owner | Flow C; until then the SMS key stays hidden |
| D5 | Client: `Supabase.Gotrue` 8.5.1 vs thin `HttpClient` wrapper over §2 (both viable; Gotrue defaults to Global sign-out and email type `MagicLink`: wrap both defaults) | Engineering | Phase-1 sign-in implementation |
| D6 | Identity linking: accept "same person = separate users per method" for v0.16.0, or schedule `linkIdentity` + enable manual linking (project-wide switch) | Owner/product | Account-merge story; not blocking |
| D7 | Autonogrammer account authorisation model on the shared project (own `accounts` table keyed by `sub`; allow-list vs open sign-up) | Product/engineering | Railway API design (R-data-plane) |
| D8 | Refresh-token storage on desktop (OS keyring) and dispatch session policy (one session per device) | Engineering | Account module |

**Open verification items (not done in this recon; need the owner or a live token):** (a) hosted `/auth/v1/settings` and Management API values were taken from the website docs, not re-read; (b) whether hosted GoTrue already allows loopback IPs without an allow-list entry; (c) exact wildcard behaviour of `http://127.0.0.1:*/**` — test once with a throwaway `redirect_to`; (d) whether any other app's trigger on `auth.users` runs on new desktop users; (e) whether the live publishable key is `sb_publishable_` or legacy `anon`.
