# Existing projects

These projects answer different parts of the problem. None is currently embedded in Unshrill.

## Equalizer APO

[Equalizer APO](https://sourceforge.net/projects/equalizerapo/) is a mature, low-latency system-effect equalizer for Windows. It is the leading first DSP backend because it can validate endpoint-wide comfort profiles without a custom driver. Its processing is endpoint-oriented, so it does not by itself provide clean per-process filtering. Exclusive-mode and ASIO paths can bypass the normal Windows system-effect graph.

## EarTrumpet

[EarTrumpet](https://github.com/File-New-Project/EarTrumpet) is a strong reference for Windows session management, device routing, and the practical need to reapply persisted intent as sessions change. It is research material, not a code dependency.

## Microsoft samples

- The [application-loopback sample](https://learn.microsoft.com/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/) demonstrates process-tree capture on Windows build 20348 and later. It is useful for measurement, not transparent in-place filtering.
- [SysVAD](https://learn.microsoft.com/samples/microsoft/windows-driver-samples/sysvad-virtual-audio-device-driver-sample/) demonstrates virtual audio devices. It also demonstrates how much driver-specific work version 1 should avoid.

## Lessons carried forward

- Windows sessions are ephemeral; persisted user intent cannot be keyed to a session object alone.
- Endpoint-wide DSP must be labeled honestly as endpoint-wide.
- Capturing an application does not suppress or rewrite its existing output.
- Device and session churn are ordinary lifecycle events, not edge cases.
- An audio safety tool needs a bypass path that does not depend on the failing backend.

