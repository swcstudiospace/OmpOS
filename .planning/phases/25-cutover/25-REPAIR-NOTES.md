# Phase 25 repair notes (2026-10-25)

The gate on the parity behaviours failed to compile in the test project only. These are the analyzer errors and the repair rule for each.

- tests/Autonogrammer.Native.Tests/Parity/ParitySessionGapTests.cs(216,25): MA0006, use string.Equals instead of the equality operator. Repair: compare with string.Equals(left, right, StringComparison.Ordinal).
- tests/Autonogrammer.Native.Tests/Parity/ParityShellGapTests.cs(70,23), (104,23) and (145,23): CA2000, a ScriptedPromptChannel is created without disposal in scope. Repair: the test owns the channel. Declare it with await using in the test method before it is passed on, and make ScriptedPromptChannel.DisposeAsync idempotent if it is not already, so the host's own disposal and the test's disposal can both run.

Scope: test files only. No product code changes.
