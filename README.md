# Unshrill

Make Windows audio easier to live with.

Unshrill is a Windows audio comfort project. Its first job is to make per-application volume, mute, device-routing, and comfort-profile rules reliable. Its longer-term job is to soften needlessly harsh sound without damaging everything else a program plays.

> [!IMPORTANT]
> Unshrill is an early test build. Persistent application volume rules work on the current default Windows output. Mercy Mode is an optional endpoint-wide prototype that requires a separate Equalizer APO installation.

## Current test build

The GUI currently:

- discovers shared-mode audio sessions on the default multimedia output;
- reacts to default-device and new-session notifications, with a five-second recovery refresh;
- follows applications as their sessions appear or disappear;
- reads and changes each session's Windows volume;
- reads and changes each session's mute state;
- remembers an application's volume and mute state across restarts;
- preserves malformed settings and starts safely with defaults;
- detects Equalizer APO and can generate a reversible, endpoint-scoped high shelf;
- leaves ordinary Windows audio untouched when Unshrill is closed.

Applications using exclusive-mode or non-default outputs will not appear in this build. Multiple sessions from one application are shown separately because Windows exposes them separately.

Saved rules live in `%LocalAppData%\Unshrill\settings.json`. The current Remember button creates an application-wide rule, so it survives a default-device change. Device-specific rule fields exist in the schema but are not exposed in the GUI yet.

Mercy Mode starts at a 5 kHz, -6 dB high shelf. It does not intercept or rewrite an application's stream: it manages a small `unshrill.txt` include for an existing [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) installation. The app scopes that include to the currently displayed endpoint, backs up `config.txt` before first integration, and bypasses by writing a valid `OFF` filter. Equalizer APO must first be installed and assigned to the output with its Configurator.

## What it is building toward

- Richer per-output and packaged-application identity controls.
- Import, export, tray controls, and a global policy bypass.
- Optional comfort profiles such as a gentle high-frequency shelf above roughly 5 kHz.
- A clear boundary between Windows session control, DSP algorithms, and whichever audio-processing backend is selected.
- Research notes that distinguish verified Windows behavior from hypotheses.

## Architecture

```text
Unshrill.App
	|
	+-- Unshrill.Core -------- rules, identity, policy
	+-- Unshrill.WindowsAudio - Core Audio discovery and control
	+-- Unshrill.Dsp ---------- testable signal-processing primitives
			|
			+-- backend adapter (Equalizer APO first candidate)
			+-- custom APO or virtual endpoint only if later justified
```

The controller comes first. A custom driver does not. Process-loopback capture is reserved for measurement and classification because it does not replace an application's original output path. The Windows interop boundary uses the pinned MIT-licensed `NAudio.Wasapi` package; policy and UI code do not depend on it.

## Build

Requirements:

- Windows 11
- Visual Studio with the .NET desktop workload, or the .NET 10 SDK

```powershell
dotnet restore Unshrill.slnx
dotnet build Unshrill.slnx
dotnet run --project tests/Unshrill.Tests
dotnet run --project src/Unshrill.App
```

The executable project is `src/Unshrill.App`. Continuous integration performs the same build and smoke-test sequence on Windows.

## Repository map

- [`docs/`](docs/README.md) - architecture, research, decisions, and roadmap.
- [`src/Unshrill.Core`](src/Unshrill.Core) - platform-independent rule policy.
- [`src/Unshrill.WindowsAudio`](src/Unshrill.WindowsAudio) - Windows audio boundary.
- [`src/Unshrill.Dsp`](src/Unshrill.Dsp) - pure DSP code.
- [`src/Unshrill.App`](src/Unshrill.App) - Windows desktop shell.
- [`tests/Unshrill.Tests`](tests/Unshrill.Tests) - dependency-free smoke tests.

## Principles

1. Observe before intercepting.
2. Keep the audio callback allocation-free and log-free.
3. Preserve an untouched path and an immediate bypass.
4. Treat device changes and session recreation as normal operation.
5. Add a dependency only when it has a defined boundary and lifecycle.
6. Do not call capture, filtering, or routing "working" until it is tested on Windows.

## Status

See the [roadmap](docs/roadmap.md). The current milestone needs an audible Equalizer APO test and persistence testing across real application/device recreation. Architectural decisions are recorded under [`docs/decisions`](docs/decisions).

## License

[MIT](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md). External sources are not vendored or included as Git submodules.
