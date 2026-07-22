# ADR 0005: Persistent policy and managed Equalizer APO configuration

- Status: accepted for prototype testing
- Date: 2026-07-21

## Context

Windows session instance identifiers are temporary, and the built-in mixer does not reliably provide the application/device persistence expected by the project. Frequency treatment is a different problem: session volume APIs cannot alter PCM samples, while writing an in-process hook or custom APO would add significant crash, signing, deployment, and recovery risk.

Equalizer APO supports endpoint selection, separate included configuration files, high-shelf filters, and automatic reload after configuration changes. Its main configuration can also contain unrelated user work that Unshrill must not replace.

## Decision

- Store versioned settings in `%LocalAppData%\Unshrill\settings.json` through a temporary-file-and-replace operation.
- Match the first GUI rules by executable name, not ephemeral session identifier. Keep endpoint identity available for later device-specific rules.
- Preserve malformed settings under a unique recovery filename and start with defaults.
- Keep Equalizer APO optional and external.
- On explicit user action, append one clearly delimited block to `config.txt`, scoped to the current endpoint, which includes `unshrill.txt` at the post-mix stage.
- Back up `config.txt` before the first managed edit. Subsequent changes replace only the marked block and managed include.
- Bypass with `Filter: OFF`, leaving a valid audio graph and immediate path back to untreated audio.

## Consequences

- Application levels can survive session and default-device recreation without depending on a particular audio backend.
- Mercy Mode is endpoint-wide and may affect every application using that endpoint.
- Equalizer APO still has to be installed and assigned to the endpoint with its own Configurator.
- Unshrill does not redistribute or link against Equalizer APO.
- A custom APO, virtual endpoint, or process-specific treatment remains a separate future decision.

## Primary references

- [Microsoft: RegisterSessionNotification](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessionmanager2-registersessionnotification)
- [Equalizer APO configuration reference](https://sourceforge.net/p/equalizerapo/wiki/Configuration%20reference/)
- [Equalizer APO user documentation](https://sourceforge.net/p/equalizerapo/wiki/Documentation/)
