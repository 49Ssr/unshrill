# ADR 0004: Use NAudio.Wasapi at the interop boundary

- Status: accepted
- Date: 2026-07-21

## Context

Reliable Windows session control requires many COM interfaces with exact vtable layouts, HRESULT handling, reference lifetime, and callback rules. Handwritten declarations would add a large unsafe surface before Unshrill reaches its distinctive policy and comfort features.

NAudio.Wasapi 2.3.0 is a stable, MIT-licensed package released in March 2026. It provides managed Core Audio wrappers, session enumeration, and `ISimpleAudioVolume` access while remaining narrow enough to isolate inside `Unshrill.WindowsAudio`.

## Decision

Pin `NAudio.Wasapi` 2.3.0 as a NuGet dependency of `Unshrill.WindowsAudio`. Keep its types out of Core, DSP, and UI contracts. Commit the NuGet lock file and preserve license attribution in `THIRD_PARTY_NOTICES.md`.

## Consequences

- The first test build can enumerate and control real Windows sessions without maintaining duplicate COM declarations.
- Unshrill still owns lifecycle, concurrency, application identity, recovery, and policy behavior.
- The interop implementation can be replaced without changing the rest of the application.
- Package updates require release-note review, Windows runtime testing, and an explicit lock-file change.
