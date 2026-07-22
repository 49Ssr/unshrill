# Comfort filter

The first comfort profile was a broad, gentle high-frequency shelf, not a brick-wall low-pass.

## Experimental result

The fixed-filter approach failed the transparency goal. A steep high-frequency roll-off softened the motivating UI sounds, but disabling it restored clearly audible musical treble, clarity, and fullness. This is a useful negative result: the unwanted events share spectrum with content the listener wants to preserve.

The shelf is therefore retained only as:

- a diagnostic for finding relevant bands;
- a manually toggled emergency profile;
- a control condition for comparing selective treatment.

It is no longer the intended automatic solution. See [Selective treatment of harsh and intrusive sounds](../research/selective-harshness-treatment.md) for the measurement-first direction.

Historical proposed starting point:

- Turnover frequency: 5 kHz.
- Shelf gain: -6 dB.
- Slope: 0.7.
- Bypass on by default until the affected output is explicit.

A shelf preserves more engine, speech, and ambience detail than deleting everything above a cutoff. These values were product defaults to test, not a medical recommendation or a universal hearing profile. They should not be enabled silently or presented as transparent.

`Unshrill.Dsp` contains a normalized biquad designer and stateful mono processor. Deployment code must create one filter state per channel. Coefficient changes should be smoothed or swapped between buffers to avoid clicks.

Before shipping any profile, measure:

- magnitude response at low, turnover, and high frequencies;
- peak output and headroom;
- denormal and NaN behavior;
- stereo/channel independence;
- parameter-change artifacts;
- CPU cost at common sample rates and buffer sizes.

Adaptive treatment is a research phase. A detector that mistakes cymbals, tyre noise, consonants, alarms, or rain for an annoying UI beep is not a comfort feature.
