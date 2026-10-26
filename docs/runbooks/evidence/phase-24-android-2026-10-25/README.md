# Phase 24 Android head build, 2026-10-25

Command (repository root, .NET environment set):

```
ANDROID_HOME=/root/Android/Sdk dotnet build apps/OmpOS.Android/OmpOS.Android.csproj -c Release --no-restore -warnaserror
```

Result: Build succeeded (net10.0-android36.0, Release, warnings as errors). Full log: android-build.log in this directory.

Not run here: the CI job `android` in .github/workflows/ci.yml (not pushed, owner decision G24-2), and the Android 16 accessibility dump (no device; /dev/kvm absent).
