# Phase 40 Verification: Settings Window and Model Providers

How to re-run each proof and what it proves. Run from the repository root with the .NET environment set as in `.planning/phases/44-plugin-submodules-launch-contract/44-VERIFICATION.md`. Tests not tagged Live are hermetic: fake omp scripts, fake servers, temporary directories, fake keys. Live tests run the real omp in a sandbox with fake keys, send no prompt and make no call that needs a credential.

Results recorded on 2026-10-26 are in `40-SUMMARY.md`. Project totals recorded after the Settings and Models wave (F6b): `OmpOS.Harness.OmpConfig.Tests` 314 (4 live skipped by design), `OmpOS.Harness.Secrets.Tests` 69, `OmpOS.Presentation.Tests` 975, `OmpOS.Native.Tests` 1087, `OmpOS.Harness.Acp.Tests` 100. After the native steering work the whole-solution run passed 5409 tests with 0 failed and 10 skipped (`Category=Live`) (F7).

## Gate tests (no omp needed)

| Criterion | Command | What it proves |
|-----------|---------|----------------|
| 1 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*RuntimeSettingsModelTests*" --filter-class "*RuntimePageTests*" --filter-class "*SettingsWindowModelTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsWindowTests*" --filter-class "*SettingsClosePromptTests*"` | Defaults 24 hours and 7 days, inclusive bounds, inline rejection and correction, reset per section, the unsaved-changes prompt (save, discard, keep editing) and its focus handling. |
| 1 (applied) | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SessionLifecycleSweeperTests*" --filter-class "*RuntimeSettingsBindingTests*" --filter-class "*FileSettingsStoreTests*"` | A saved value is written to `settings.json` and put in force; an idle session sleeps, a Completed session is removed after the retention window, a running session is never touched. |
| 2 | `dotnet test tests/OmpOS.Harness.Providers.Tests -c Release -- --filter-class "*ProviderCatalogueTests*" --filter-class "*LocalProviderProbeTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ModelsSettingsSourceProbeTests*"` | Every catalogue entry has a source and a date; local entries listen on loopback; a probe against a fake OpenAI-style server and against nothing (not running) behaves as described. |
| 3 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*CustomProviderRulesTests*" --filter-class "*CustomProviderDraftTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ModelsCustomProviderTests*" --filter-class "*ModelsSettingsSourceCustomProviderTests*"` | The form rules, the write of a keyed or keyless block (the key variable name, never the key), refusal of ids omp owns, rollback when omp refuses a block, edit and remove. |
| 4 | `dotnet test tests/OmpOS.Harness.Providers.Tests -c Release -- --filter-class "*VerifiedProviderRowTests*" --filter-class "*ProviderErrorMapperTests*" --filter-class "*ProviderEntryValidatorTests*" --filter-class "*ProviderEndpointValidatorTests*"` | The research rows are accepted as written, https is accepted and plain http only to loopback, quota exhaustion and plan expiry give actionable text, a plan with no published cap says so. |
| 6 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*SettingsPageSetTests*" --filter-class "*PluginsPageTests*" --filter-class "*AgentsPageTests*" --filter-class "*GeneralPageTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsPageStateTests*" --filter-class "*ModelsPageViewTests*"` | Eleven pages in order (Network and Keyboard added in `a12871a`; see also `NetworkSettingsPageTests`, `NetworkDiagnosticsSourceTests`, `ShortcutCatalogueTests`), every editable control reaches its source, the Plugins page has no switch, each page shows its states, a Models card shows only the actions omp can do. The 34 rows themselves are in `docs/settings-parity.md`; no test enumerates them. |
| 7 | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsFontTests*" --filter-class "*SettingsMarkupTests*"` | Every page, including the Models page with every part open, draws only the token families; no literal family, hard-coded colour or literal copy in the Settings files. |
| Keys | `dotnet test tests/OmpOS.Harness.Secrets.Tests -c Release` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ModelsSecretLeakTests*" --filter-class "*ProviderKeyEnvironmentTests*" --filter-class "*ProviderKeyHeadTests*" --filter-class "*ModelsSettingsSourceKeyTests*" --filter-class "*ModelsApiKeyTests*"` | The owner-only file (modes, atomic replace, no reset of a corrupt file); a built-in key travels on standard input only; a custom provider's key is stored under its variable name and reaches the chat engine and the terminal omp, never the plan; no output carries a key; the key entry is masked with no reveal switch. |
| omp configuration | `dotnet test tests/OmpOS.Harness.OmpConfig.Tests -c Release -- --filter-not-trait "Category=Live"` | The command runner, the listing and credential presence, import, the default model with the other roles kept, the managed block editor (round trip byte for byte, comments and line endings kept, unsupported layouts refused). |
| Accessibility | `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*SettingsKeyboardTests*" --filter-class "*SettingsAccessibilityTests*"` | Navigation keys, tab order, named controls and the 24 px target floor in the Settings window. |
| gate | `./bootstrap.sh`, or `dotnet build OmpOS.Linux.slnf -c Release` then `dotnet test OmpOS.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` | Build with warnings as errors, analyzers and every non-live test. |

## Live tests (real omp, no model turn)

Need `omp` on `PATH`, Linux and `OMPOS_LIVE_OMP=1`. They are excluded from the gate by the trait.

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 3 | `OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Harness.OmpConfig.Tests -c Release --no-build -- --filter-trait "Category=Live"` | `AManagedProviderAppearsInTheRealModelsListingAndLeavesWithItsBlock`; `AChangeTheRealOmpRejectsIsRolledBackByteForByte`; `AnImportedKeyIsResolvedByOmpAndKeptOwnerOnly` (a fake key, `omp token` resolves it, the file is owner-only); `SettingTheDefaultModelKeepsTheOtherRolesInTheRealOmp`. | 4 of 4 passed on 2026-10-26 against omp 18.12.6 in a sandbox. |
| 4 (built-in table) | `OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Native.Tests -c Release --no-build -- --filter-trait "Category=Live"` (`OmpBuiltInProvidersLiveTests`) | Each provider of the built-in table lists models with its own variable set and none without. The first omp run needs the network (cold cache). | The table was checked in a sandbox on 2026-10-26 (header of `OmpBuiltInProviders.cs`); a run of this test was not recorded separately. |
| model chip | the same filter (`LiveOmpOptionChipsTests`) | The composed engine signs in with `agent`, opens a session without a model turn and fills the mode (Default, Plan) and thinking chips from the real omp. | Passed on 2026-10-26; omp's models were listed in the model chip of the real application under Xvfb (`acp-fix-2-model-chip-lists-omp-models.png`). |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/phase-40-omp-providers-2026-10-26/omp-provider-surface.md` | How omp 18.12.6 holds providers, credentials and the default model: the offline read paths, the `models.yml` schema and its behaviour when broken, `omp auth import -`, `agent.db` stored unencrypted with owner-only mode, the ACP `model` option and `session/set_config_option`. |
| `docs/runbooks/evidence/phase-40-settings-2026-10-26/` | The Release build under Xvfb with the real omp as engine in a sandbox: `settings-1-runtime.png` to `settings-9-plugins.png` (the first nine pages in order; Network and Keyboard are in `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/settings-10-network.png` and `settings-11-keyboard.png`), `models-a` to `models-d` (provider templates, a template picked, the blank add-provider form), `chat-a-new-agent-session.png`, `chat-b-model-chip-flyout.png`, `00-main-window-empty.png`, and a list of twelve findings. No key was entered and no login flow was started. |
| `docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/acp-fix-1-new-agent-composer.png`, `acp-fix-2-model-chip-lists-omp-models.png` | The chat session after the authentication fix: composer with the model chip, and omp's catalogue grouped by provider in the flyout. |

What the Settings screenshots show for AL-19: body text, labels and descriptions in the shapes of DM Sans, ids, versions and endpoints in a monospace face consistent with Space Mono (judged by eye). Page headings render in a thin narrow face (Anybody Thin default instance), which is not claimed as DM Sans. The General page in these screenshots offers Motion and Privacy only; the Theme list came with Phase 50 and is not in them.

## Known limits of this verification

- No credential of value was used: nothing shows that a key works, a quota holds or a plan has not expired (OQ-71).
- No model turn (OD-07).
- Linux only; the owner-only file modes are Unix modes. Windows, macOS, Android and hosted deployments were not run.
- A cold first `omp models --json` reached the network and took more than 10 seconds in the sandbox; the Models page shows a loading line meanwhile.
- The twelve findings of the screenshots README are partly fixed (1 and 2); the rest were not re-checked here.
