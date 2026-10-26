# ADR-0008: Contract versioning policy for plugin contracts

- Status: Accepted 2026-10-22
- Date: 2026-10-22
- Owner: ompos-maintainers
- Deciders: ompos-maintainers
- Scope: the version rules for presentation plugin contracts and harness plugin contracts in `src/OmpOS.Contracts/Plugins` and the plugin host. Gated by risk R-11 (`docs/plans/ade-harness-plan.md`, section 4 "Contract versioning"). Does not cover the HTTP API contract, MCP protocol versions or out-of-process tools (see Consequences).

## Context

In-process plugins are loaded into the host process as .NET assemblies (plan section 4, "In-process first"). They are compiled against a contract assembly. If a contract changes shape and nothing records the change, a plugin built against the old shape fails at run time, or worse, silently binds to a different member. R-11 records this risk and requires a written policy before Phase 21 starts. Phase 21 is the parity surface, and the host must be able to reject a plugin it cannot run.

The repository already has contract types for plugins: `PluginDescriptor` (identifier, name and the plugin's own `Version`), `CommandContribution`, `PanelContribution`, `ToolContribution` and `AgentModeContribution`, all in `src/OmpOS.Contracts/Plugins`. None of them declares the contract version range a plugin needs, and the host does not yet check one. `ContractVersion` (`src/OmpOS.Contracts/ContractVersion.cs`, value `1.0.0`) is the version of the HTTP contract header, not of the plugin contracts, and is outside this ADR.

The plan's lifecycle has three steps this policy governs: discovery (a manifest declares the contract version range), load (the host checks the range; a plugin outside it is not loaded and the reason is logged), and activation.

## Decision

### 1. Versioned contract assemblies

Each contract assembly that plugins compile against carries a version that follows SemVer 2.0.0. A change to a contract assembly is classified by the rules in sections 2 and 3, and the version moves in the same change.

### 2. Major version: breaking change

A change is breaking, and raises the major version, when it does any of the following to a contract a plugin implements or consumes:

- removes, renames or changes the signature of a public member;
- changes the meaning or the required behaviour of a member;
- adds a required member to an interface, abstract class or record a plugin must supply;
- makes a previously optional member required;
- changes the identity of a contribution point (its name or the type a plugin returns).

Resetting the minor and patch numbers to zero is part of the major bump.

### 3. Minor version: additive change

A change is additive, and raises the minor version, when it does either of the following:

- adds a new contribution point (for example a new kind of contribution the host can register);
- adds an optional member with a default that keeps existing plugins working unchanged.

A patch change fixes documentation or an implementation detail that does not change any contract member. A patch change does not affect the range check in section 5.

### 4. Host declares a supported range

The host declares a contiguous range of contract versions it supports, as `[minimum, maximum]` on major and minor. The declaration is a single source of truth in the host and is not repeated in each plugin. A host that supports `1.0` through `1.2` supports every contract version in that range.

### 5. Load rule

A plugin declares, in its manifest, the contract version range it requires. At load the host checks the declared range against its supported range:

- when the ranges overlap, the plugin loads and activates by its lifecycle;
- when they do not overlap, the plugin is not loaded. The host logs a warning naming the plugin identifier, the declared range, and the host's supported range. The plugin's contributions are not registered and no code of the plugin runs.

A refused plugin does not stop the host or any other plugin from loading.

### 6. Contract test in the gate

The gate runs a contract test that asserts the host's supported range includes every range declared by a built-in plugin. A change that narrows the host range, or that raises a built-in plugin's declared minimum above the host's maximum, fails the gate.

### 7. Classification of contract families

Both families are covered by this policy:

- Presentation plugin contracts: the contribution points a head renders, which are commands and panels. These are `CommandContribution` and `PanelContribution`.
- Harness plugin contracts: the contribution points that feed the agent harness, which are tools and agent modes. These are `ToolContribution` and `AgentModeContribution`. The editor provider and terminal provider contribution points (ADE-09, ADE-14) are harness plugin contracts once they exist.

A new contribution point is classified into one of these two families when it is added. A contract that fits neither family is classified by the maintainers in the change that adds it, and the change records the classification in this ADR's amendment list.

### 8. The manifest changes

`PluginDescriptor` gains the declared contract version range when the plugin host is built (Phase 20 and Phase 21 work). This ADR does not change `PluginDescriptor`. The existing `Version` member remains the plugin's own version and is not the contract range.

## Consequences

- Any change to a plugin contract needs a version decision in the same change. The contract test in section 6 catches a mismatch in the gate.
- The host can reject a plugin it cannot run, with a logged reason, instead of failing at run time.
- Plugins built against the current `1.0` shape stay loadable while the host range includes `1.0`.
- The policy does not cover:
  - the HTTP contract (`docs/api/openapi.json` and the `X-Ompos-Contract-Version` header, which is `ContractVersion.Current`). Whether the same rules apply to it is an open decision for the owner (`docs/api/ADE-07-run-contract-change-request.md`, section 7, item 8);
  - MCP protocol versions, which ADE-16 negotiates with each MCP server;
  - process isolation. Plugins are trusted in-process code under the permission model in the plan.
- Implementation is not part of this ADR. The manifest range, the load check, the warning log and the gate test are later work. Until they exist, R-11 remains open in the plan and this ADR records only the policy.

## Amendments

None.
