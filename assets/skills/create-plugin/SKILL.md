---
name: create-plugin
description: Write an Anthropic-layout plugin bundle with the size caps, and list hooks without running them.
disable-model-invocation: false
user-invocable: true
---

# Create a plugin bundle

A plugin bundle is a folder the user installs. It can hold skills, slash commands, agent definitions, an MCP file and a hooks file. Autonogrammer installs it only after the user asks, into `~/.autonogrammer/plugins/<id>/` or `<folder>/.autonogrammer/plugins/<id>/`. This skill writes that folder. It does not install it and it does not run anything inside it.

## 1. Clarify

Ask in one message only for what you cannot infer:

- The bundle id, and whether it is for this project or for the user in every project. Default to the project folder, and say that a project bundle stays off until the user enables it.
- Which of skills, commands, agents and MCP servers the bundle should contain.
- That hooks, if any, will be listed and never run.

## 2. Name it

The bundle id is the folder name unless `.claude-plugin/plugin.json` sets `name`. Use lowercase letters, digits and hyphens, start with a letter or digit, and keep it to 64 characters. Do not reuse a built-in skill or command id (`create-skill`, `review`, and the other shipped names). A collision is refused at install, and a built-in of the same id still wins if the bundle is already on disk.

## 3. Write the layout

```text
<id>/
  .claude-plugin/plugin.json    optional
  skills/<name>/SKILL.md
  commands/<name>.md
  agents/<name>.md
  .mcp.json                     optional
  hooks/hooks.json              optional, listed only
```

`plugin.json` may set `name`, `description` and `version`. Do not add other keys. A command file `commands/a.md` is offered as `/<id>:a`. An agent file `agents/x.md` is listed as `<id>:x`. In `.mcp.json`, a server `s` is named `<id>.s` and stays off until the user confirms the command line. Write `${CLAUDE_PLUGIN_ROOT}` when a path must point at the installed bundle; Autonogrammer expands it to the install directory.

Caps, which install refuses:

| Limit | Refusal |
| --- | --- |
| A path that leaves the bundle, including `..` | `BundleInvalid(BundleTraversal)` |
| A symbolic link | `BundleInvalid(BundleSymlink)` |
| One file over 2 MiB | `BundleInvalid(BundleTooLarge)` |
| More than 2,000 files | `BundleInvalid(BundleTooManyFiles)` |
| The bundle over 50 MiB | `BundleInvalid(BundleTooBig)` |
| A bad id | `BundleInvalid(BundleBadName)` |
| A malformed `plugin.json` | `BundleInvalid(BundleBadManifest)` |
| A built-in skill or command id | `BundleInvalid(BundleNameCollision)` |

## 4. Hooks

If the user wants hooks, write `hooks/hooks.json` as data only. Tell the user the screen shows `BundleComponentNotLoaded(hooks)` and `hooks listed, not run`. Do not add a step that executes a hook, and do not tell the user a hook will run.

## 5. Verify and report

1. Re-read the tree: no path leaves the folder, no symbolic link, every file is under 2 MiB, and the file count and total size are under the caps.
2. Tell the user the folder path, that install is a separate step in Settings, and that a project-scope bundle shows `BundleProjectScopeOff` until they enable it.
3. Skills from the bundle reach the session only as an overlay of the `skills` folder. A skill the user disables is left out of the launch filter.

## Never

- Never copy a third-party plugin repository into the product or into this folder.
- Never put secrets in the bundle.
- Never run `hooks/hooks.json`, and never start an MCP server from `.mcp.json`. Confirmation is the user's, one server at a time.
- Never overwrite an existing bundle folder without showing what will change and getting a yes.
