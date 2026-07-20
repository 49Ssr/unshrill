# Knowledge base

This directory is the durable memory of Unshrill. Research notes link to primary sources, architecture pages describe the intended system, and decision records explain choices that would otherwise be rediscovered later.

## Start here

- [Roadmap](roadmap.md)
- [Architecture overview](architecture/overview.md)
- [Windows audio pipeline](architecture/windows-audio-pipeline.md)
- [Comfort filter](dsp/comfort-filter.md)
- [Existing projects](research/existing-projects.md)
- [NFS:HPR case study](research/nfshpr-case-study.md)
- [Dependency policy](dependencies.md)

## Decisions

- [ADR 0001: Controller-first architecture](decisions/0001-controller-first.md)
- [ADR 0002: External DSP adapter before a custom APO](decisions/0002-external-dsp-adapter.md)
- [ADR 0003: No custom driver for version 1](decisions/0003-no-custom-driver-for-v1.md)

Research notes use three confidence labels:

- **Verified** - supported by documentation, source, or a repeatable test.
- **Inferred** - the evidence points this way, but a test is still required.
- **Proposed** - a design choice, not a fact about Windows.
