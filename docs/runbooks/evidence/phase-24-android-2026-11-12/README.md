# Phase 24 Android head build re-run, 2026-11-12

Command (repository root; DOTNET_ROOT=$HOME/.dotnet, PATH includes $HOME/.dotnet, ANDROID_HOME=/root/Android/Sdk):

```
dotnet build apps/Autonogrammer.Android/Autonogrammer.Android.csproj -c Release -p:TargetLatestRuntimePatch=false -p:RuntimeFrameworkVersion=10.0.13
```

SDK: 10.0.402 (global.json). Android workload 36.1.118 installed. Target: net10.0-android36.0.

Date: 2026-11-12. Exit code: 1 (see exit-code.txt). Warnings: 0. Errors: 1.

Result: FAILED during implicit restore, before compilation. The build stopped at `Failed to restore`.

First error (verbatim):

```
/root/src/repos/autonogrammer/apps/Autonogrammer.Android/Autonogrammer.Android.csproj : error NU1004: The project references autonogrammer.native.views whose dependencies has changed.
```

Second restore error (verbatim, same project):

```
/root/src/repos/autonogrammer/apps/Autonogrammer.Android/Autonogrammer.Android.csproj : error NU1004: A new project reference to Autonogrammer.Cowork.Artifacts was found for net10.0-android36.0 target framework.The packages lock file is inconsistent with the project dependencies so restore can't be run in locked mode. Disable the RestoreLockedMode MSBuild property or pass an explicit --force-evaluate option to run restore to update the lock file.
```

Interpretation (not verified beyond the log): `packages.lock.json` for `Autonogrammer.Android` is stale relative to its project references (a new reference to `Autonogrammer.Cowork.Artifacts` for net10.0-android36.0). Restore runs in locked mode and refuses to update the lock file. Fixing it means regenerating the lock file (restore with --force-evaluate), which edits a project file and was out of scope for this run. No code or project file was changed.

Not run: a `--no-restore` build (the 2026-10-25 evidence used `--no-restore`, which would skip this restore step). Not run because the task specified stop on failure. The `-warnaserror` flag from the 2026-10-25 run was also omitted here.

Stale reference: `docs/runbooks/evidence/phase-24-android-2026-10-25/README.md` names `apps/OmpOS.Android/OmpOS.Android.csproj`. That path no longer exists; the project is `apps/Autonogrammer.Android/Autonogrammer.Android.csproj`.

Full log: android-build.log in this directory.
