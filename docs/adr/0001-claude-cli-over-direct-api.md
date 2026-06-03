# ADR-0001: Use Claude CLI instead of Anthropic API directly

## Status
Accepted

## Context
The extension needs to call Claude to generate commit messages. Two options exist: shell out to the `claude` CLI tool, or call the Anthropic API directly from C# using an HTTP client.

## Decision
Shell out to the `claude` CLI via stdin, capturing stdout.

## Reasons
- CLI handles auth — no API key management in the extension, no secrets stored in VS settings
- Users who install the extension likely already have Claude CLI configured
- CLI handles model selection, rate limiting, and API versioning — extension stays thin
- Removes dependency on Anthropic NuGet SDK and version coupling

## Trade-offs
- Hard dependency on Claude CLI being installed; extension is useless without it
- Less control over request parameters (model, temperature, etc.) unless CLI exposes flags
- Process spawn overhead (~100ms) per generation call

## Alternatives considered
Direct Anthropic API via HTTP: more control, no external dependency, but requires API key storage and adds SDK maintenance burden.
