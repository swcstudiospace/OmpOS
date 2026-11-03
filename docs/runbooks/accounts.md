---
description: "Accounts runbook: owner sign-in setup for email, Google, X and the Ethereum wallet, the owner actions and what the product does when each is not done, and what is blocked."
---

# Accounts runbook (phase 67)

Status: The sign-in core (GoTrue client, session manager, sign-in coordinator) and the web sign-in options (Google, X and an Ethereum wallet) are built and tested offline against fakes. No live Supabase call has ever been made; live rows are blocked on the owner's publishable key (OQ-301) and the dashboard actions below. Desktop provider sign-in is not wired.

Last verified: 2026-10-29 (offline tests only)

Applies to: [ADR-0030](../decisions/ADR-0030-accounts-supabase-passwordless.md), the library `Autonogrammer.Accounts` and the contract folder `Autonogrammer.Contracts.Accounts`.

## What exists

- `Autonogrammer.Accounts` holds six plain `HttpClient` calls to the shared Supabase auth host (`SendEmailOtpAsync`, `VerifyEmailOtpAsync`, `ExchangeCodeAsync`, `RefreshAsync`, `SignOutAsync(scope)`, `GetUserAsync`), the session manager (`AccountSessionManager`, behind `IAccountSession`) and the sign-in coordinator (`SignInCoordinator`). There is no Supabase SDK.
- The project URL and the publishable key are not compiled in. The API serves them on `GET /auth/config` (`Api:Auth:Client`, plan 67-03 and 67-05) and `GoTrueOptions.Create` validates them: an `sb_secret_` key or a `service_role` JWT is refused, naming the field and never the value.
- The refresh token is kept in the secret store under the name `autonogrammer.account.session` as `{schema, issuer, refreshToken, userId, email}`. The access token is never stored.
- No log line, failure or message carries an email address, code, token, verifier, URL query or server message text. Failures are one of 16 `AuthErrorKind` values with fixed copy.
- The web head offers Google and X only when `GET /auth/v1/settings` reports the provider enabled. `GoTrueClient.BuildProviderAuthorizeUri` allows exactly `google` and `x`, sends a PKCE S256 challenge, and keeps the verifier in memory in the asking tab. The return reaches that tab through the magic-link hand-off, never through browser storage.
- The web head offers an Ethereum wallet only when a wallet is present. `SiweMessage` builds the EIP-4361 text, the browser signs it with `personal_sign`, and `GoTrueClient.SignInWithWeb3Async` sends `grant_type=web3` with `chain: "ethereum"` and the CAPTCHA token when one is set. The desktop head offers no wallet.
- Owner-token sign-in is gone from the web head. The API keeps owner-token mode for operators and tests; the web head explains that sign-in is not available for that mode.

## Owner actions and what happens when each is not done

We change no dashboard setting. The owner decides, per action; the product degrades as follows. (Source: ADR-0030 section D1, research report 05 section "Owner actions".)

| Action | What it is | Needed for | Not done |
| --- | --- | --- |
| O1 | Add the redirect URLs `http://127.0.0.1:*/**` and `https://<web head origin>/**` (Authentication, URL Configuration). Additive; keeps every existing entry. | The magic link on desktop and web | The operator leaves `Api:Auth:Client:MagicLinkEnabled` false (the default): no PKCE challenge is sent, no redirect, no loopback listener runs, and the code is the only way in. |
| O3 | Raise `rate_limit_email_sent` above 2 per hour (Authentication, Rate Limits). The limit is shared with the website and every other app on the project. | Any real use | A 429 `over_email_send_rate_limit` becomes `EmailRateLimited` with its own message. A returning person who already holds a code still signs in. |
| O4 | Show the one-time code in the website's `ConfirmSignup` email template (website repository and deploy, not a dashboard toggle). | Code sign-in for first-time email users | `Api:Auth:Client:SignupEmailHasCode` stays false (the default). The panel tells a new person to open the link in the first email once and then request a new code; the second email is a `magiclink` email that carries a code. [INFERENCE: opening a signup link whose redirect is not allow-listed still confirms the user; this is the first live check.] |
| O5 | Enable Google and X (Authentication, Providers) with the client ID and secret from each developer console. Use `https://<project-ref>.supabase.co/auth/v1/callback` as the redirect URI in both consoles. | The Google and X buttons | No buttons. The panel shows only the providers the project reports as enabled. |
| O6 | Enable Ethereum Web3 sign-in for the project (Authentication, Web3). | The wallet button | No wallet button. |
| O7 | Turn on CAPTCHA protection for sign-ins with Cloudflare Turnstile. The widget and its site key in the web head are not built. | Wallet sign-in on a project that requires CAPTCHA | A 400 `captcha_failed` shows the wallet failure sentence. CAPTCHA protection is project-wide, so it can also gate the email code path, which sends no token [INFERENCE: which endpoints require a token]. Leave it off until the widget covers every sign-in path. |

With none of the actions done, a returning person signs in by code inside the 2 per hour limit and a first-time person follows the link-then-new-code path of O4. SMS is not built. Leave Site URL, hooks, signing keys and manual linking untouched, and keep "Single session per user" off. The Google, X and wallet buttons stay hidden until their actions (O5 to O7) are done.

## Owner manual sign-in check

Run this by hand after the API is deployed with `Api__Auth__Client__ProjectUrl` and `Api__Auth__Client__PublishableKey` set. It sends real emails against a shared quota of 2 per hour (until O3), so run it once and do not script it.

1. Optional before: run the read-only `auth-doctor.mjs` with `SUPABASE_ACCESS_TOKEN` in a local shell and keep its `config.*` output (email rate, redirects, hooks). It never writes. Run it again after any owner action and compare.
2. Open `GET <api>/auth/config`. Expect `mode: "oidc"`, the `authority` and `audience` of the project, and a `projectUrl`, `publishableKey`, `methods`, `emailCode` and `signupEmailHasCode` block. A missing block means `Api:Auth:Client` is not configured and heads report `ConfigUnavailable`.
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
- Google, X and the Ethereum wallet have no live run. The provider IDs, the authorize parameters, the `grant_type=web3` body and the `external` map of `/auth/v1/settings` were checked against the GoTrue source, not against a live project.
- The wallet CAPTCHA widget and its Content Security Policy entries are not built. The CAPTCHA token is sent only when one is set.
- Desktop provider sign-in is not wired: the account view is not placed in the desktop shell.

## Where the pieces live

| Piece | Path |
| --- | --- |
| Client, manager, coordinator | `src/Autonogrammer.Accounts` |
| Contracts (`AuthPhase`, `AuthErrorKind`, `AuthConfigResponse`, `AccountResponse`, `AccountSessionList`) | `src/Autonogrammer.Contracts/Accounts` |
| Offline suite and fake service | `tests/Autonogrammer.Accounts.Tests` (`Support/FakeGoTrueHandler.cs`) |
| Decision | [ADR-0030](../decisions/ADR-0030-accounts-supabase-passwordless.md) |
| Web sign-in options (Google, X, wallet) | `apps/Autonogrammer.Web/Services/WebAccountSession.cs`, `src/Autonogrammer.UI/Components/Account/SignInPanel.razor`, `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-auth.js` |
| Provider and wallet hosts | `src/Autonogrammer.Presentation/Accounts/IProviderSignInHost.cs`, `IWalletSignInHost.cs`, `WalletSignInOutcome.cs` |

Run the offline suite with `dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"`.
