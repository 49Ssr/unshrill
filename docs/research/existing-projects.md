# Existing projects

These projects answer different parts of the problem. None is currently embedded in Unshrill.

## NAudio.Wasapi

[NAudio.Wasapi](https://www.nuget.org/packages/NAudio.Wasapi/) provides the managed Core Audio interop used by the working session-control prototype. Version 2.3.0 is pinned as a NuGet dependency under the MIT license. Its types are confined to `Unshrill.WindowsAudio`; the source repository is not vendored or added as a submodule.

## Equalizer APO

[Equalizer APO](https://sourceforge.net/projects/equalizerapo/) is a mature, low-latency system-effect equalizer for Windows. It is the leading first DSP backend because it can validate endpoint-wide comfort profiles without a custom driver. Its processing is endpoint-oriented, so it does not by itself provide clean per-process filtering. Exclusive-mode and ASIO paths can bypass the normal Windows system-effect graph.

## EarTrumpet

[EarTrumpet](https://github.com/File-New-Project/EarTrumpet) is a strong reference for Windows session management, device routing, and the practical need to reapply persisted intent as sessions change. It is research material, not a code dependency.

## Microsoft samples

- The [application-loopback sample](https://learn.microsoft.com/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/) demonstrates process-tree capture on Windows build 20348 and later. It is useful for measurement, not transparent in-place filtering.
- [SysVAD](https://learn.microsoft.com/samples/microsoft/windows-driver-samples/sysvad-virtual-audio-device-driver-sample/) demonstrates virtual audio devices. It also demonstrates how much driver-specific work version 1 should avoid.

## Offline analysis candidates

These are research references, not application dependencies:

- [SciPy ShortTimeFFT](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.ShortTimeFFT.html) provides a documented STFT and spectrogram implementation for offline time-frequency analysis.
- [librosa](https://librosa.org/doc/latest/feature.html) exposes spectral centroid, bandwidth, contrast, flatness, roll-off, and spectral-flux onset features useful for an explainable baseline.
- [MoSQITo](https://github.com/Eomys/MoSQITo) is an Apache-2.0 Python project implementing and validating sound-quality metrics including ISO 532-1 loudness, DIN 45692 sharpness, ECMA-418 roughness, prominence ratio, and tone-to-noise ratio.

MoSQITo does not turn an uncalibrated digital loopback into an absolute psychoacoustic instrument. It is a candidate for reproducible comparisons after its input assumptions and calibration requirements are satisfied.

## Lessons carried forward

- Windows sessions are ephemeral; persisted user intent cannot be keyed to a session object alone.
- Endpoint-wide DSP must be labeled honestly as endpoint-wide.
- Capturing an application does not suppress or rewrite its existing output.
- Device and session churn are ordinary lifecycle events, not edge cases.
- An audio safety tool needs a bypass path that does not depend on the failing backend.
- A static high-frequency shelf cannot distinguish a target UI transient from wanted cymbals, consonants, or musical brightness occupying the same band.

