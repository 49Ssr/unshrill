# Dependency policy

Unshrill currently has no third-party runtime dependency and no Git submodule.

That is deliberate. A submodule is appropriate when Unshrill builds, tests, or ships against a specific external source tree. It is not a bookmark and should not be added merely because a project is useful research.

## Current candidates

| Project | Possible role | Current treatment |
| --- | --- | --- |
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

