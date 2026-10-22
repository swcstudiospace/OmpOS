# Fixture run

## User

Summarise the parser changes.

## Assistant

The parser now **recognises** magic keywords.

- Adds `PromptParser` tests
- Keeps the old path

## Code: csharp

```csharp
Console.WriteLine("parsed");
```

## Tool call: read

Status: completed

Call ID: call-1

Arguments:

```json
{"path":"src/Parser.cs"}
```

## Permission request

Allow write to src/Parser.cs?

- Allow once
- Deny

## Plan

1. Read the parser
2. Write the exporter

## Diff: src/Parser.cs

Diff preview is not available yet.

## Interface: summary

**Parser summary**

Two recognisers were added.

- one
- two

[Open]

Unsupported component: chart

## Error

Provider timed out.

Retryable: yes

## Unknown block: future.widget

```json
{"kind":"future.widget"}
```

## Assistant (in progress)

Still writing the summary
