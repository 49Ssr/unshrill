# ADR 0001: Controller-first architecture

- Status: accepted
- Date: 2026-07-20

## Context

Unshrill needs persistent per-application volume and device rules as well as optional audio treatment. Windows exposes stable session-control APIs, while in-place per-process DSP requires a more invasive processing path.

## Decision

Build the Core Audio controller and persistence model before selecting or implementing a PCM-processing backend. Keep policy, Windows interop, DSP algorithms, and backend deployment in separate projects.

## Consequences

- The first useful release can improve Windows mixer persistence without solving transparent DSP.
- DSP backends can be compared behind one boundary.
- The UI must clearly distinguish volume/routing rules from comfort filtering.

