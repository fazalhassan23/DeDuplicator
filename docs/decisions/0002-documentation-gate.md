# ADR 0002: Generated structure with reviewed semantic impact

Status: accepted for documentation enforcement.

## Context

Manual structure/version copies drift. A change-detection script cannot reliably explain application semantics, while contributors need specific obligations and legitimate exemptions.

## Decision

Generate schema/entities from the SQL initializer, version references from Version.props and Unreleased content from reviewed fragments. Keep semantic descriptions and diagrams in Markdown. Use pinned maintained Markdown linting and official Mermaid rendering. Readable impact mappings and a JSON PR declaration require topic changes or exact-path substantive exemptions.

## Consequences

The gate detects missing artifacts, malformed diagrams, broken local links and structural drift, not dishonest exemptions or semantic inaccuracies. Python/Node/Chromium are development-only dependencies. Read-only hosted PR checks have no writeback, approval or publication capability. [Testing](../testing.md) explains negative fixtures and external-link boundaries.
