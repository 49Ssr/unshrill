# Offline candidate analyzer

The first selective-treatment implementation is an offline WAV workbench. It measures candidate events without changing the recording or the Windows audio path.

## User flow

1. Run the Unshrill desktop application.
2. Select **Analyze WAV...**.
3. Choose a short, lossless WAV recording.
4. Review candidate time ranges and copy the text report for comparison.

The result window reports:

- candidate start, end, and duration;
- a heuristic candidate score;
- the signal signatures that contributed to the score;
- frame RMS and rise above the recent background;
- energy share in the current 5-10 kHz research band;
- spectral centroid and flatness;
- dominant 1-12 kHz frequency and its prominence.

The words **candidate** and **signature** are deliberate. This build does not know whether the listener dislikes an event.

## Input boundary

`WaveFileLoader` uses the repository's existing NAudio.Core dependency to convert ordinary PCM or IEEE-float WAV data into interleaved floating-point samples.

Current limits:

- WAV input only;
- five minutes per file;
- no more than 50 million decoded samples, approximately 200 MB;
- all channels are averaged to mono for the first analysis pass;
- no calibrated sound-pressure input;
- no live capture or treatment.

The mono average is a known limitation for unusually phase-opposed material. Per-channel analysis should replace it if real corpus files demonstrate cancellation.

## Analysis path

The default analysis uses:

- 2,048-sample Hann windows;
- 512-sample hops;
- a dependency-free radix-2 FFT;
- a slowly adapting background-power estimate;
- adjacent candidate frames merged across gaps up to 80 ms.

For each frame, the analyzer calculates:

- RMS and crest factor;
- rise relative to the preceding background estimate;
- spectral magnitude and power;
- positive normalized spectral flux;
- magnitude-weighted spectral centroid;
- power-spectrum flatness;
- 5-10 kHz power relative to total non-DC power;
- dominant frequency and peak-to-average prominence from 1-12 kHz.

## Candidate families

Four flags are currently available:

- `Bright` - elevated focus-band share or spectral centroid.
- `Tonal` - a prominent frequency above 1.5 kHz.
- `Transient` - spectral change or a large crest factor.
- `SuddenEmergence` - frame power rises above the recent background.

The score compares three patterns and retains the strongest:

1. bright plus transient, tonal, or emerging;
2. tonal plus emerging;
3. strong transient plus emergence at a meaningful level.

Thresholds are heuristic seeds chosen to make corpus collection possible. They are not derived from a listening study and must not quietly become product truth.

## What is intentionally absent

- No universal harshness label.
- No automatic attenuation.
- No psychoacoustic sharpness value presented as calibrated.
- No roughness or repetition classifier yet.
- No learned model.
- No claim that a high score is harmful.

Roughness needs a longer envelope-modulation analysis. Repetition needs event identity or template history. Both should be driven by real labeled recordings rather than added because they appear in the research taxonomy.

## Validation

The smoke-test executable currently verifies that:

- a short synthetic 7 kHz burst over a quiet low tone becomes a bright and tonal candidate;
- a steady 220 Hz tone does not become a candidate;
- a generated stereo PCM WAV round-trips through `WaveFileLoader` with the expected format, duration, sample count, and amplitude.

These tests validate mechanics, not perceptual accuracy.

## Next proof gate

Run the workbench against:

- known HPR UI offenders;
- clean music containing cymbals and bright synthesizers;
- speech sibilants;
- rain, tyres, impacts, engine harmonics, and alarms.

Save the copied reports with user labels. The next implementation decision should come from the observed separation:

- If a small rule set separates targets, retain an explainable detector.
- If only repeated sound identity separates them, build template matching.
- If neither separates them without frequent false positives, do not proceed to live treatment.
