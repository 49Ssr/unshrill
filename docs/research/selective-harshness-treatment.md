# Selective treatment of harsh and intrusive sounds

This note records the research direction that replaced Unshrill's original fixed high-frequency shelf. The motivating experiment was simple and decisive: a steep roll-off made the unwanted UI sounds less aggressive, but it also removed useful treble, clarity, transients, and musical "air". Frequency was part of the problem, not a sufficient definition of it.

The durable goal is therefore:

> Leave ordinary audio untouched and briefly attenuate only events that match a user-confirmed intrusive sound.

This is a research target, not a claim that annoyance can be inferred universally from a waveform. The same cymbal, consonant, warning, or UI chirp can be useful in one context and intolerable in another.

## Confidence labels

- **Verified** - supported by a standard, official documentation, peer-reviewed work, or a repeatable observation.
- **Inferred** - supported by several observations but still needs a controlled test.
- **Proposed** - an engineering experiment, not an established perceptual fact.

## Findings that change the design

### 1. There is no honest "bad frequency" cutoff

**Verified:** ISO 226 specifies equal-loudness relationships for pure tones from 20 Hz through 12.5 kHz under tightly defined conditions. It does not define frequencies above 5 kHz, 10 kHz, or any other boundary as useless or annoying. It also describes young, otologically normal listeners in a controlled free field, so it is not an individualized hearing model.

**Verified by experiment:** The user's fixed attenuation above roughly 5 kHz reduced the target sounds and audibly degraded music. Disabling it restored clarity and fullness.

**Conclusion:** Do not encode "useful content stops at 10 kHz" as a product assumption. A static high shelf remains useful as a diagnostic or manually toggled emergency profile, but not as transparent automatic treatment.

### 2. Harshness is multidimensional

There is no single standardized metric named "this sound will annoy this listener." Relevant measurable dimensions include:

- **Loudness and emergence:** how strongly the event rises above its immediate context.
- **Sharpness:** how the event's perceptually weighted loudness is distributed toward higher auditory bands.
- **Tonality or prominence:** whether a narrow tone or elevated narrow band stands out from nearby noise.
- **Impulsiveness or transientness:** how abruptly energy or spectral content changes.
- **Roughness:** rapid envelope modulation within auditory bands. ECMA-418-2 describes prominent roughness over roughly 20-300 Hz modulation, with the steady-sound percept peaking near 70 Hz.
- **Fluctuation strength:** slower envelope variation; ECMA-418-2 places its steady-sound perceptual maximum near 4 Hz.
- **Temporal pattern and repetition:** warning-sound research shows that pitch, speed, rhythm, pitch range, and melodic structure can change perceived urgency. Repetition and context can therefore matter even when the spectrum is unchanged.

ECMA-418 is especially instructive because it treats loudness, tonality, roughness, and fluctuation strength as separate psychoacoustic quantities. Its tonality model covers pure tones, multiple tones, narrow elevated bands, spectral slopes, and time-varying combinations. A simple FFT peak is only an approximation of this richer problem.

### 3. Relative measures are useful even when absolute psychoacoustics are unavailable

WASAPI loopback produces the digital mix sent through the Windows audio engine. It does not measure sound pressure at the ear and does not include the acoustic response of the DAC, amplifier, headphones or speakers, fit, room, or listener.

Consequences:

- Digital peak, RMS, spectral ratios, and clip-to-clip differences are valid within a controlled capture chain.
- Absolute phon, sone, sharpness, or audibility claims require the calibration and listening assumptions of their respective standards.
- An uncalibrated loopback may rank two captures for one user, but should not present itself as a laboratory sound-quality meter.
- ITU-R BS.1770 loudness and true-peak measurements remain useful engineering descriptors, not a universal annoyance score.

### 4. Observation and treatment are different Windows capabilities

**Verified:** WASAPI endpoint loopback can capture the system mix. Microsoft's application-loopback sample can instead include or exclude a target process tree on Windows 10 build 20348 or later.

**Verified:** Capture does not replace the rendered stream. It provides a copy for analysis. Transparent treatment still requires an audio-processing path such as an endpoint APO, a virtual endpoint, a supported host/plugin chain, or a cooperating application.

**Implication:** Build and validate the detector offline first. Do not couple classifier research to risky Windows audio interception.

## A practical signal model

The target should be represented as a short time-frequency event, not a static EQ curve.

### Time-frequency representation

An STFT spectrogram provides a sequence of short spectra. Short windows localize abrupt events better; longer windows resolve nearby frequencies better. One resolution cannot optimize both.

**Proposed analysis pair at 48 kHz:**

- Short path: 512-sample Hann window, 128- or 256-sample hop, for onset and duration.
- Spectral path: 2,048-sample Hann window, 256- or 512-sample hop, for narrow tones and band shape.

These are starting points. They correspond to approximately 10.7 ms and 42.7 ms windows and must be tested against the actual UI clips. Zero padding may make plots smoother but does not create new frequency resolution.

### Candidate event features

No feature below is sufficient alone.

#### Level and emergence

- Peak and RMS level.
- Crest factor: peak divided by RMS for a frame or event.
- Short-event loudness proxy.
- Event level relative to the preceding local background.
- High-band level relative to the same band's recent baseline.

Emergence is likely more useful than an absolute threshold when the same beep occurs over both silence and music.

#### Spectral location and shape

- Energy ratio for a configurable band such as 5-10 kHz against broadband energy.
- Spectral centroid, the magnitude-weighted mean frequency.
- Spectral roll-off and bandwidth.
- Spectral flatness, to distinguish noise-like energy from a concentrated spectrum.
- Peak prominence relative to neighboring bins or perceptual bands.
- Harmonic spacing or multiple related peaks where present.

The 5-10 kHz band is a user-derived search region, not a permanent limit. The analyzer should show where each labeled sound actually differs from its hard negatives.

#### Temporal shape

- Positive spectral flux between adjacent frames.
- Rise time and attack slope.
- Event duration and decay slope.
- Number and spacing of sub-onsets.
- Envelope modulation spectrum for buzzy or rattling events.

Onset-detection literature treats transients as possible changes in energy, spectrum, phase, or statistical properties. That is why a single amplitude threshold will miss some sharp events and falsely catch ordinary drum hits.

#### Repetition and identity

- Normalized correlation between event envelopes.
- Similarity between normalized log-spectrogram patches.
- Repetition interval and count.
- A user-labeled template score for a known recurring UI sound.

Template similarity may outperform a universal harshness score for a small set of repeated offenders. It also provides a safer product statement: "this resembles a sound you marked" rather than "this sound is objectively bad."

## Detector progression

### Stage A: measurement-only workbench

Build an offline analyzer before any real-time processor.

Inputs:

- Lossless process-loopback recordings where possible.
- A timestamp or marker for each user-labeled offending event.
- Clean comparison material captured through the same path.
- Original sample rate, channel layout, application identity, endpoint identity, volume state, and capture date.

Outputs:

- Waveform and log-frequency spectrogram.
- Candidate event boundaries.
- Per-event feature table.
- Side-by-side target and hard-negative distributions.
- Exported labels and configuration in a human-readable format.

Do not peak-normalize the only stored copy. Keep untouched captures and derive normalized analysis copies, because absolute digital level and local contrast may be useful features.

### Stage B: transparent rule-based detector

Start with an explainable conjunction rather than machine learning:

1. Detect an onset or rapid spectral change.
2. Measure high-band emergence relative to the recent context.
3. Require either tonal prominence, a known spectral shape, or strong template similarity.
4. Enforce plausible duration limits.
5. Apply hysteresis and a short refractory period so one event does not chatter on and off.

Every threshold must be learned from the labeled corpus or exposed as a user control. The detector should report which conditions fired.

### Stage C: user-specific similarity model

Only after the rule baseline is measured, consider a small model over normalized log-mel or log-frequency spectrogram patches.

Requirements:

- User-confirmed positive examples.
- Hard negatives from the same applications and devices.
- Source-separated train, validation, and test groups so copies of one sound do not leak across splits.
- Probability calibration and a deliberately conservative operating point.
- A visible undo/bypass path and no autonomous permanent learning from unlabeled audio.

A model trained on one game's UI beeps must not be advertised as a general detector of harmful or annoying sound.

## Treatment choices

Treatment should be selected from the detected event's shape.

### Narrow tonal attenuation

Use one or more bell/notch filters when the offender is a stable prominent tone. This preserves more unrelated treble than a shelf. The center frequency and bandwidth should follow the measured event, then return smoothly to unity gain.

### Dynamic high shelf

Use a shelf only when the offending event is broadly bright rather than narrowly tonal. The shelf should be detector-controlled and limited in depth.

### Split-band compression or de-essing

Send the target band to a detector and reduce only that band when its level or emergence exceeds a threshold. This is conceptually close to a de-esser, but the target may not be speech sibilance and should not inherit speech-specific defaults without testing.

### Template-triggered attenuation

For a small repeated set, a similarity match can trigger a preconfigured filter envelope. This does not isolate the sound from a simultaneous music mix; it attenuates the corresponding frequencies in the whole mix for the event window. Collateral damage remains possible and must be measured.

### Proposed conservative starting envelope

These are intentionally test values, not recommendations:

- Maximum reduction: 3-9 dB.
- Attack: 1-5 ms, or a small look-ahead buffer if latency is acceptable.
- Hold: 10-40 ms when needed to prevent chatter.
- Release: 60-200 ms, tuned to avoid pumping and clipped tails.
- Dry bypass: sample-transparent when treatment is inactive.

Fast attack without look-ahead may miss the leading edge. Look-ahead adds latency. Very fast release can modulate or distort the event; very slow release can dull following music or speech.

## Why Equalizer APO is useful but not the complete detector

Equalizer APO's documented native configuration primitives are static gain, IIR filters, graphic EQ, convolution, routing, delay, and configuration controls. They are excellent for:

- Reproducing the fixed-EQ negative result.
- Auditioning candidate bands and filter shapes.
- Hosting a manually toggled comfort profile.
- Serving as a possible endpoint-wide host for a separately validated dynamic processor.

Its documented filter language does not itself provide an event classifier, envelope detector, or native dynamic EQ primitive. Community VST hosting exists, but plugin compatibility and state management need to be tested rather than assumed. Endpoint processing also remains semantically blind: it sees mixed samples, not "menu beep" and "cymbal" objects.

## Dataset design

### Positive examples

- Every known offending sound in isolation if obtainable.
- The same event over silence, music, speech, and gameplay ambience.
- Different Windows and in-game volume settings.
- Repeated examples from separate launches and menus.

### Hard negatives

- Cymbals, hi-hats, shakers, and bright synth attacks.
- Speech sibilants and radio static.
- Rain, tyre squeal, impacts, glass, alarms, and scanner tones.
- Engine harmonics, turbo hiss, and exhaust transients.
- Desired UI feedback that happens to occupy the same band.
- Entire music tracks with the fixed shelf bypassed.

### Split discipline

Do not randomly divide near-identical repetitions of one asset across training and test sets. Hold out whole sound identities, applications, scenes, or recording sessions. Otherwise a detector can appear excellent by memorizing duplicated assets.

## Evaluation gates

### Detection

- Event precision and recall.
- False triggers per hour of ordinary listening.
- Missed user-labeled events.
- Trigger timing error and detection latency.
- Performance by application and negative category.
- Stability across volume changes.

For a comfort processor, false triggers deserve unusually high cost. A system that catches every UI beep but repeatedly dulls music has repeated the fixed-EQ failure in a more complicated form.

### Treatment

- Maximum and accumulated gain reduction.
- Time spent away from unity gain.
- True peak before and after treatment.
- Added latency and CPU cost.
- Clicks, pumping, stereo-image movement, phase effects, and clipped tails.
- Null or difference signal on negative material.

### Listening tests

Use level-matched, randomized comparisons with the untreated signal as a hidden reference. ITU-R BS.1116 is the more relevant formal reference when looking for small impairments; MUSHRA in ITU-R BS.1534 is intended for intermediate quality differences. A personal development test need not claim formal compliance, but should borrow the disciplines of hidden references, anchors where appropriate, controlled level, and randomized presentation.

Ask two separate questions:

1. Did treatment reduce the labeled intrusive event?
2. Did treatment damage anything the listener wanted to keep?

Combining those into one score can hide a bad trade-off.

## Safety and product-language boundaries

- Do not claim hearing protection, diagnosis, or treatment of a medical condition.
- Do not infer a listener's hearing threshold from uncalibrated loopback audio.
- Preserve a one-action bypass and fail open to unprocessed audio.
- Avoid positive filter gain in an automatic comfort profile unless headroom is explicitly managed.
- Log detector decisions without retaining audio unless the user explicitly opts in.
- Keep capture, analysis, and treatment permissions separate.
- Describe results as preference-based attenuation or user-trained comfort processing.

## Recommended next experiment

This is the smallest experiment capable of validating the new direction:

1. Capture 10-20 examples of the known HPR sounds with application loopback.
2. Capture at least 30 minutes of hard-negative music, speech, and gameplay from the same signal path.
3. Label event start, end, and subjective severity without changing the source audio.
4. Generate a dual-resolution spectrogram and the feature set above.
5. Identify the smallest feature combination that separates targets from hard negatives.
6. Simulate gain reduction offline; do not process the live Windows endpoint yet.
7. Run randomized, level-matched listening comparisons.
8. Proceed to a real-time host only if the offline detector meets an agreed false-positive budget.

Suggested initial success gate:

- No more than one obvious false treatment per hour of the collected hard-negative set.
- At least 90% of labeled repeated target events detected in held-out sessions.
- Bypass is bit-identical or sample-identical when no event is detected.
- The listener prefers treated target clips and cannot reliably identify damage on untreated hard negatives.

The numbers are **proposed product gates**, not scientific standards. They should become stricter as the corpus grows.

## Candidate research tools

No new runtime dependency is approved by this note.

- **Microsoft ApplicationLoopback sample:** authoritative reference for process-tree capture on supported Windows builds.
- **SciPy ShortTimeFFT:** suitable for transparent offline STFT and spectrogram experiments.
- **librosa:** provides documented spectral centroid, flatness, roll-off, contrast, and spectral-flux onset features.
- **MoSQITo:** a Python sound-quality research library with implementations of ISO 532-1 loudness, DIN 45692 sharpness, ECMA-418 roughness, prominence ratio, and tone-to-noise ratio. Its metrics still need correct calibration and applicability checks.
- **Equalizer APO:** useful for manual filter audition and endpoint-wide prototypes, not evidence that a classifier exists.

If the repository is revived, evaluate these as disposable research tooling before adding packages or submodules. Preserve exact versions and licenses in `docs/dependencies.md`.

## Sources

- [ISO 226:2023 - Normal equal-loudness-level contours](https://www.iso.org/standard/83117.html)
- [ISO 532-1:2017 - Zwicker loudness for stationary and time-varying sounds](https://www.iso.org/standard/63077.html)
- [DIN 45692 - Measurement technique for the auditory sensation of sharpness](https://www.dinmedia.de/en/standard/din-45692/117635111)
- [ECMA-418 - Psychoacoustic metrics, prominent tones, tonality, roughness, and fluctuation strength](https://ecma-international.org/publications-and-standards/standards/ecma-418/)
- [ITU-R BS.1770-5 - Programme loudness and true-peak algorithms](https://www.itu.int/rec/R-REC-BS.1770/en)
- [EBU R 128 - Loudness normalization and permitted maximum level](https://tech.ebu.ch/publications/r128)
- [ITU-R BS.1116-3 - Subjective assessment of small audio impairments](https://www.itu.int/rec/R-REC-BS.1116-3-201502-I/en)
- [ITU-R BS.1534-3 - MUSHRA assessment of intermediate audio quality](https://www.itu.int/rec/R-REC-BS.1534/)
- [Microsoft - WASAPI loopback recording](https://learn.microsoft.com/windows/win32/coreaudio/loopback-recording)
- [Microsoft - Application loopback audio capture sample](https://learn.microsoft.com/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/)
- [Microsoft - Process loopback modes](https://learn.microsoft.com/windows/win32/api/audioclientactivationparams/ne-audioclientactivationparams-process_loopback_mode)
- [Bello et al. - A tutorial on onset detection in music signals](https://doi.org/10.1109/TSA.2005.851998)
- [SciPy - ShortTimeFFT spectrogram](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.ShortTimeFFT.spectrogram.html)
- [librosa - Feature extraction](https://librosa.org/doc/latest/feature.html)
- [librosa - Spectral-flux onset strength](https://librosa.org/doc/latest/onset.html)
- [MoSQITo - Sound quality metrics](https://mosqito.readthedocs.io/en/latest/source/reference/mosqito.sq_metrics.html)
- [Equalizer APO - Configuration reference](https://sourceforge.net/p/equalizerapo/wiki/Configuration%20reference/)
- [Edworthy, Loxley, and Dennis - Warning parameters and perceived urgency](https://doi.org/10.1177/001872089103300206)

Sources were reviewed on 2026-07-22. Standards define their own scopes and measurement conditions; this note does not extend them into a universal annoyance classifier.
