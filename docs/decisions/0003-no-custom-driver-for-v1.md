# ADR 0003: No custom driver for version 1

- Status: accepted
- Date: 2026-07-20

## Context

A virtual audio endpoint offers a controllable render path, but brings driver development, signing, installation, device routing, update, and recovery responsibilities.

## Decision

Do not make a custom driver or virtual endpoint a version-1 dependency. Research it only after the controller and external DSP prototype have measurable limits.

## Consequences

- The initial application remains easy to uninstall and recover from.
- Per-process filtering may remain unavailable in early versions.
- Any future driver proposal must include a deployment and audio-recovery design, not only DSP code.

