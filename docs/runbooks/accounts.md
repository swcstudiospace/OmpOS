---
description: "Accounts runbook: owner sign-in setup for email, Google, X and the Ethereum wallet, the owner actions and what the product does when each is not done, and what is blocked."
---

# Accounts runbook (phase 67)

Status: The sign-in core (GoTrue client, session manager, sign-in coordinator) and the web sign-in options (Google, X and an Ethereum wallet) are built and tested offline against fakes. No live Supabase call has ever been made; live rows are blocked on the owner's publishable key (OQ-301) and the dashboard actions below. Desktop provider sign-in is not wired.

Last verified: 2026-10-29 (offline tests only)

Applies to: [ADR-0030](../decisions/ADR-0030-accounts-supabase-passwordless.md), the library `Autonogrammer.Accounts` and the contract folder `Autonogrammer.Contracts.Accounts`.

## What exists

- `Autonogrammer.Accounts` holds the plain `HttpClient` calls to the shared Supabase auth host: the email code and link calls (`SendEmailOtpAsync`, `VerifyEmailOtpAsync`, `ExchangeCodeAsync`, `RefreshAsync`, `SignOutAsync(scope)`, `GetUserAsync`) and the wallet grant (`SignInWithWeb3Async`). The session manager (`AccountSessionManager`, behind `IAccountSession`) and the sign-in coordinator (`SignInCoordinator`) sit on top. There is no Supabase SDK.
- The project URL and the publishable key are not compiled in. The API serves them on `GET /auth/config` (`Api:Auth:Client`, plan 67-03 and 67-05) and `GoTrueOptions.Create` validates them: an `sb_secret_` key or a `service_role` JWT is refused, naming the field and never the value.
- The offered sign-in methods come from the operator's declaration, not from the identity project. `Api:Auth:Client:Providers` lists `google` and/or `x`, and `Api:Auth:Client:WalletEnabled` offers the wallet. The API serves both in `methods` of `/auth/config`. The identity project's settings endpoint does not report `x` or Web3 (GoTrue `internal/api/settings.go`), so the web head does not read it.
- Google and X start a PKCE sign-in in a new tab. `GoTrueClient.BuildProviderAuthorizeUri` allows exactly `google` and `x`. The verifier stays in memory in the asking tab; the return reaches that tab through the magic-link hand-off, never through browser storage.
- The wallet button appears only when it is declared and a browser wallet is present. The address is written in EIP-55 checksum form (`src/Autonogrammer.Accounts/EthereumAddress.cs`). GoTrue keys a wallet identity by the address exactly as the message writes it (`web3:ethereum:<address>`), and the website's client writes the checksum form, so a lowercase address would make a second user.
- Owner-token sign-in is gone from the web head. The API keeps owner-token mode for operators and tests, and the web head explains that sign-in is not available for that mode.
- The refresh token is kept in the secret store under the name `autonogrammer.account.session` as `{schema, issuer, refreshToken, userId, email}`. The access token is never stored.
- No log line, failure or message carries an email address, code, token, verifier, URL query or server message text. Failures are one of 16 `AuthErrorKind` values with fixed copy.

## Owner actions and what happens when each is not done

We change no dashboard setting. The owner decides, per action; the product degrades as follows. (Source: ADR-0030 section D1, research report 05 section "Owner actions".)

| Action | What it is | Needed for | Not done |
| --- | --- | --- | --- |
| O1 | Add the redirect URLs `http://127.0.0.1:*/**` and `https://<web head origin>/**` (Authentication, URL Configuration). Additive; keeps every existing entry. | The magic link on desktop and web, and the redirect of the Google, X and wallet sign-ins (O5, O6) | The operator leaves `Api:Auth:Client:MagicLinkEnabled` false (the default): no PKCE challenge is sent, no redirect, no loopback listener runs, and the code is the only way in. A wallet sign-in whose URI is not on the list is refused (GoTrue `web3.go`). A provider sign-in whose `redirect_to` is not on the list does not return to the web head; GoTrue falls back to the Site URL, which is the shared website [INFERENCE: GoTrue redirect handling, not run]. |
| O3 | Raise `rate_limit_email_sent` above 2 per hour (Authentication, Rate Limits). The limit is shared with the website and every other app on the project. | Any real use | A 429 `over_email_send_rate_limit` becomes `EmailRateLimited` with its own message. A returning person who already holds a code still signs in. |
| O4 | Show the one-time code in the website's `ConfirmSignup` email template (website repository and deploy, not a dashboard toggle). | Code sign-in for first-time email users | `Api:Auth:Client:SignupEmailHasCode` stays false (the default). The panel tells a new person to open the link in the first email once and then request a new code; the second email is a `magiclink` email that carries a code. [INFERENCE: opening a signup link whose redirect is not allow-listed still confirms the user; this is the first live check.] |
| O5 | Google and X. Enable Google (Authentication, Providers) with its client ID and secret. Enable X / Twitter (OAuth 2.0) with the client ID and secret of an X developer app. In that app's User authentication settings turn on **Request email from users**: without it X sign-in fails for lack of an email. Use `https://<project-ref>.supabase.co/auth/v1/callback` as the redirect URI in both consoles. Then declare what is enabled in the API: `Api__Auth__Client__Providers__0=google` and/or `Api__Auth__Client__Providers__1=x`. | The Google and X buttons | No button. The web head offers only what `Api:Auth:Client:Providers` declares. A declared provider that the dashboard has not enabled shows the "could not start" sentence on click, so declare only what is enabled. |
| O6 | Ethereum wallet. Enable Web3 Ethereum sign-in for the project, set the Web3 sign-in rate limit (GoTrue `RATE_LIMIT_WEB3`; the source default is 30, the dashboard label is not verified), then set `Api__Auth__Client__WalletEnabled=true`. | The wallet button | No wallet button. A wallet sign-in while Web3 is off in the dashboard fails with the wallet failure sentence. The rate limit is shared by every wallet sign-in on the project. |
| O7 | CAPTCHA (Cloudflare Turnstile) for sign-ins. Supabase CAPTCHA protection is project-wide: once it is on, every sign-in call of every app on the project must send a token, including the email code path, the owner's website and the desktop head. The web head has no Turnstile widget yet, and the desktop head has none. | Wallet sign-in on a project that requires CAPTCHA (owner decision) | Leave it off. With it on and no token, the email code and wallet sign-ins answer `captcha_failed`; the wallet shows its failure sentence, and the other apps' sign-ins fail as well. |

With none of the actions done, a returning person signs in by code inside the 2 per hour limit and a first-time person follows the link-then-new-code path of O4. SMS is not built. Leave Site URL, hooks, signing keys and manual linking untouched, and keep "Single session per user" off. The Google, X and wallet buttons stay hidden until their actions (O5 to O7) are done, and O5 and O6 need O1.

The shared project `xpfoidcvbwmpbwodjvqb` also serves the Spectrum Web Co website and other apps (ADR-0030, line 15). Every switch in O5 to O7 changes sign-in for all of them, and the one-account-per-person rule applies to all of them (OQ-308, OQ-309).

## Owner manual sign-in check

Run this by hand after the API runs in `Oidc` mode: the switch is in `docs/runbooks/vps-development-host.md`, section "Switch the API to Oidc". It sends real emails against a shared quota of 2 per hour (until O3), so run it once and do not script it.

1. Optional before: run the read-only `auth-doctor.mjs` with `SUPABASE_ACCESS_TOKEN` in a local shell and keep its `config.*` output (email rate, redirects, hooks). It never writes. Run it again after any owner action and compare.
2. Open `GET <api>/auth/config`. Expect `mode: "oidc"`, the `authority` and `audience` of the project, and a `projectUrl`, `publishableKey` and `methods` block with `emailCode`, `magicLink`, `providers` and `wallet` as declared. `owner-token` means the API is still in owner-token mode; a missing client block means `Api:Auth:Client` is not configured and heads report `ConfigUnavailable`.
3. On a returning address: request a code in the desktop head, type the code from the email (spaces and hyphens are ignored; it has 8 digits today although the documentation says 6), expect `SignedIn` with the address shown.
4. On a first-time address: expect the hint, open the link in the first email once, then request a new code; the second email carries a code. Record whether the first link confirmed the user even though `redirect_to` was not allow-listed (the INFERENCE of O4).
5. If O1 is done and `MagicLinkEnabled` is true: open the link from the mail client on the same machine and expect `SignedIn` without typing a code; a link opened on another device must not sign in and the code must still work. Record whether `127.0.0.1:*` matched the ephemeral port.
6. Restart the desktop head: expect `SignedIn` at once and `Online` after the background refresh, with no sign-in prompt. Disconnect the network and restart: expect `SignedIn` with the offline notice, never a sign-out.
7. Sign out: expect `SignedOut` immediately. The local entry is cleared first and the service is told as a best effort; confirm the website session of the same account is still signed in (the scope is `local`). "Sign out everywhere" is the scope that ends the website session too.
8. Delete the secret `autonogrammer.account.session` and restart: expect `SignedOut` and no error.

## Not verified

- No Live Supabase row exists. Every behaviour above is proven only against `FakeGoTrueHandler`; the live rows (`Category=Live`, `AUTONOGRAMMER_LIVE_SUPABASE=1`) are blocked on the owner's publishable key (OQ-301) and on unpaused hosting. They never send an email.
- Three hosted behaviours stay [INFERENCE] until a live run: a code requested together with a PKCE challenge still verifies through `POST /verify`; the O4-less first-time path confirms the user; `http://127.0.0.1:*/**` matches an ephemeral port.
- The refresh path was never exercised against the real project's reuse interval (10 seconds is taken from the reference report).
- Google, X and the Ethereum wallet have no live run. The provider IDs and the authorize parameters were checked against the GoTrue source. The `grant_type=web3` body, the SIWE checks and the EIP-55 checksum were checked against the GoTrue source and against the published EIP-55 examples (with an independent Keccak-256 cross-check), not against a live project.
- The settings endpoint does not report `x` or Web3 (GoTrue `internal/api/settings.go`). That is why the offer is declared, and why no button appears without O5 and O6.
- The wallet CAPTCHA widget and its Content Security Policy entries are not built. The CAPTCHA token is sent only when one is set.
- Desktop provider sign-in is not wired: the account view is not placed in the desktop shell.
- One account per person holds for identities that carry an email. A wallet identity has no email, and an X identity carries one only with O5's setting. Linking those is not verified (OQ-308).

## Where the pieces live

| Piece | Path |
| --- | --- |
| Client, manager, coordinator | `src/Autonogrammer.Accounts` |
| Checksum address and Keccak-256 | `src/Autonogrammer.Accounts/EthereumAddress.cs`, `Keccak256.cs` |
| Contracts (`AuthPhase`, `AuthErrorKind`, `AuthConfigResponse`, `AuthMethods`, `AccountResponse`, `AccountSessionList`) | `src/Autonogrammer.Contracts/Accounts` |
| Declared providers and wallet (API options, validation, `/auth/config`) | `services/Autonogrammer.Api/Options/ApiAuthClientOptions.cs`, `ApiAuthOptionsValidator.cs`, `Endpoints/AuthConfigEndpoints.cs` |
| Offline suite and fake service | `tests/Autonogrammer.Accounts.Tests` (`Support/FakeGoTrueHandler.cs`) |
| Web sign-in options (Google, X, wallet) | `apps/Autonogrammer.Web/Services/WebAccountSession.cs`, `src/Autonogrammer.UI/Components/Account/SignInPanel.razor`, `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-auth.js` |
| Provider and wallet hosts | `src/Autonogrammer.Presentation/Accounts/IProviderSignInHost.cs`, `IWalletSignInHost.cs`, `WalletSignInOutcome.cs` |
| Decision | [ADR-0030](../decisions/ADR-0030-accounts-supabase-passwordless.md) |

Run the offline suite with `dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"`.
