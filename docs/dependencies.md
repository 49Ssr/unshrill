# Dependency policy

Unshrill has one narrow third-party runtime dependency and no Git submodule.

That is deliberate. A submodule is appropriate when Unshrill builds, tests, or ships against a specific external source tree. It is not a bookmark and should not be added merely because a project is useful research.

## Current candidates

| Project | Possible role | Current treatment |
| --- | --- | --- |
| NAudio.Wasapi 2.3.0 | Managed Windows Core Audio interop | Pinned NuGet dependency in `Unshrill.WindowsAudio`; MIT license |
| Equalizer APO | First practical endpoint-DSP backend | External installation; document and integrate through an adapter |
| EarTrumpet | Reference for Windows session and route behavior | Research reference only |
| Microsoft Windows classic samples | Reference for process-loopback capture | Research reference only |
| Microsoft SysVAD | Reference if a virtual endpoint becomes justified | Research reference only |

## Admission rules

Before adding a package, vendored directory, or submodule, record:

1. The exact function it provides.
2. Why a system API or small local implementation is insufficient.
3. Its license and redistribution obligations.
4. How versions and security updates will be tracked.
5. How the project behaves when the dependency is absent.

Equalizer APO is GPL-licensed software. The initial design keeps it outside the Unshrill process and talks to it through generated configuration or a narrowly defined adapter. Any deeper integration requires a separate license review.

## NAudio.Wasapi decision

NAudio.Wasapi replaces a large set of handwritten COM interface layouts, reference-counting rules, and HRESULT conversions. Unshrill still owns session lifecycle, polling, error recovery, application identity, and UI behavior. The dependency is pinned to the stable 2.3.0 release and recorded in `THIRD_PARTY_NOTICES.md`.

