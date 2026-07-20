# ADR 0002: External DSP adapter before a custom APO

- Status: accepted
- Date: 2026-07-20

## Context

An Audio Processing Object can operate inside the Windows audio graph, but implementation, registration, compatibility, recovery, and distribution are substantial commitments. Equalizer APO already provides a mature endpoint-level processing environment.

## Decision

Prototype comfort profiles through a replaceable external-backend adapter. Equalizer APO is the first candidate, but is not bundled or required by the foundation.

## Consequences

- The product experience and DSP parameters can be validated early.
- Filtering is endpoint-wide until a later backend provides a narrower scope.
- Licensing and deployment remain clean because the external program is not copied into this repository.
- If the adapter cannot meet reliability or scope requirements, it can be replaced without changing the rule engine.

