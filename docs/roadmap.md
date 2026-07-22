# Roadmap

The roadmap is organized around proof gates. A phase does not become "done" because code exists; it becomes done when its observable result is repeatable on Windows 11.

## Phase 0 - Foundation

- Buildable .NET 10/WPF solution.
- Rule-engine and DSP boundaries.
- Documentation and architectural decisions.
- Windows CI and dependency-free smoke tests.

Exit gate: a clean checkout builds in CI and the rule/DSP smoke tests pass.

## Phase 1 - Reliable session control

- [x] Enumerate the default render endpoint and its shared-mode sessions.
- [x] Display application identity without blocking the UI.
- [x] Set per-session volume and mute.
- [x] Refresh session state and recover from ordinary session disappearance.
- [x] React to endpoint/default-device and new-session lifecycle notifications, with recovery polling.
- [ ] Cover non-default render endpoints.
- [x] Reapply application-wide rules after session and default-device recreation.

Exit gate: a saved rule survives application restart, default-device changes, and endpoint reconnects.

## Phase 2 - Persistent policy

- [x] Store rules atomically in a human-readable schema.
- Match packaged and unpackaged applications safely.
- [x] Resolve conflicts predictably by priority and specificity.
- [x] Preserve malformed input as a recovery copy and continue with safe defaults.
- Add tray controls, bypass, import, and export.

Exit gate: persistence behavior is deterministic and recoverable after a malformed settings file.

## Phase 3 - Comfort DSP prototype

- [x] Detect a supported external DSP backend without making it mandatory.
- [x] Generate a reversible high-shelf profile with instant bypass and a main-config backup.
- [x] Scope and clearly display which endpoint is affected.
- Measure latency, clipping, and CPU cost.

Exit gate: the filter is audibly and measurably correct, survives device changes, and never strands the user without audio.

Implementation gate: the adapter and its file-safety tests pass. Audible behavior remains unverified until tested on a Windows output with Equalizer APO installed through Configurator.

Research result: a fixed high-frequency roll-off failed the transparency goal by audibly reducing wanted music treble. Keep the existing adapter as a reversible experiment, not the final treatment model.

## Phase 4 - Measurement and adaptive treatment

- Add opt-in process-loopback capture for analysis.
- Build spectrogram and short-event measurement tools.
- Test transient-aware or dynamic high-frequency reduction.
- Keep capture and treatment as separate capabilities.
- Establish a hard-negative corpus and a false-trigger-per-hour budget before live treatment.

Exit gate: detection performance is measured against a labeled corpus, including false positives.

## Later, only if justified

- A custom Audio Processing Object.
- A virtual audio endpoint.
- Per-process DSP independent of endpoint-wide system effects.

These require substantially more deployment, signing, compatibility, and recovery work. They are not version-1 assumptions.
