# Architecture overview

## Components

### Unshrill.App

The WPF desktop shell. It presents state and sends user intent to services. It must not perform Core Audio enumeration or DSP work on the UI thread.

### Unshrill.Core

Platform-independent policy: application identity, rule matching, conflict resolution, and persisted settings contracts. This layer must remain testable without Windows audio devices.

### Unshrill.WindowsAudio

The boundary around Windows Core Audio. It owns endpoint/session enumeration, wrapper lifetime, error recovery, and volume/mute application. It uses the narrow `NAudio.Wasapi` package instead of duplicating fragile COM layouts. Audio state is converted to small immutable snapshots before crossing into the rest of the app.

### Unshrill.Dsp

Pure signal-processing primitives. The first primitive is a biquad high shelf. This project knows nothing about endpoints, processes, COM, configuration files, or user interface state.

### Backend adapters

DSP deployment belongs behind an adapter. The first candidate is an external Equalizer APO integration because it can prove the product experience before Unshrill assumes the cost of an APO or virtual-device implementation.

## Concurrency rules

- Core Audio session notifications run from a dedicated multithreaded COM apartment.
- The UI receives immutable snapshots through an explicit dispatcher boundary.
- Real-time audio processing performs no logging, allocation, locking, file access, or UI work.
- Configuration generation and backend restart operations run outside audio callbacks.

## Failure posture

Unshrill must fail open: when the controller, rule store, or optional DSP backend fails, ordinary Windows audio continues. Every treatment has a visible bypass and every generated configuration has a recoverable previous version.

