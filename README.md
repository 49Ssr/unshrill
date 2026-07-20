# Unshrill

Make Windows audio easier to live with.

Unshrill is a Windows audio comfort project. Its first job is to make per-application volume, mute, device-routing, and comfort-profile rules reliable. Its longer-term job is to soften needlessly harsh sound without damaging everything else a program plays.

> [!IMPORTANT]
> This repository is at the foundation stage. The window, rule engine, DSP primitives, documentation, and build pipeline exist; live Windows audio control is the next implementation milestone.

## What it is building toward

- Persistent per-application and per-output-device volume rules.
- Rules reapplied when applications, sessions, or devices reappear.
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

The controller comes first. A custom driver does not. Process-loopback capture is reserved for measurement and classification because it does not replace an application's original output path.

## Build

Requirements:

- Windows 11
- Visual Studio with the .NET desktop workload, or the .NET 10 SDK

```powershell
dotnet restore Unshrill.slnx
dotnet build Unshrill.slnx
dotnet run --project tests/Unshrill.Tests
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

See the [roadmap](docs/roadmap.md). Architectural decisions are recorded under [`docs/decisions`](docs/decisions), including why there are currently no Git submodules.

## License

[MIT](LICENSE). External projects discussed in the knowledge base retain their own licenses; none are vendored here.
