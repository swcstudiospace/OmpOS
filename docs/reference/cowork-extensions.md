---
description: "Reference for Cowork plugin bundles, skills, connectors and the create-plugin skill."
---

# Cowork extensions

Status: Partial (bundle install, skill overlay and switched-off MCP servers are composed in the desktop head; omp flag consumption is not live-verified)

Last verified: 2026-10-30

Applies to: v0.16.0

Plugin bundles, skills and connectors are desktop surfaces. The web head shows what they are for and says the full surface lives in the desktop head.

## Bundles

`BundleComposition` installs a folder or zip through `BundleInstaller` and records enable flags in `BundleStateStore` and `SkillStateStore`. Enabled bundle directories are `ConventionSources.PluginRoots`. Bundle items show under the project label. Remote sessions load no bundles. Hooks are listed and never run.

## Skills

Enabled bundle skill roots are contributed as `skills.customDirectories` overlay entries by `BundleLaunchContributor`. A disabled skill that can be named is passed with `--skills`. Whether omp consumes `skills.customDirectories` or `--skills` is not live-verified, so those rows stay partial.

## Connectors

Bundle `.mcp.json` servers enter `McpServerCatalog` through `BundleMcpExtraSource`. Each entry has origin `plugin:<id>`, transport stdio, a priority above every file, `EnabledInFile` false and block text `BundleServerNotConfirmed`. Enabling one, and granting consent when the scope requires it, is the only way it is offered. Auto does not trust `readOnlyHint`.

## create-plugin

The shipped skill `create-plugin` is one of the eleven built-in skills. It does not install a bundle by itself. Installation stays on the Bundles settings page.
