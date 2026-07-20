# Comfort filter

The first comfort profile is a broad, gentle high-frequency shelf, not a brick-wall low-pass.

Proposed starting point:

- Turnover frequency: 5 kHz.
- Shelf gain: -6 dB.
- Slope: 0.7.
- Bypass on by default until the affected output is explicit.

A shelf preserves more engine, speech, and ambience detail than deleting everything above a cutoff. These values are product defaults to test, not a medical recommendation or a universal hearing profile.

`Unshrill.Dsp` contains a normalized biquad designer and stateful mono processor. Deployment code must create one filter state per channel. Coefficient changes should be smoothed or swapped between buffers to avoid clicks.

Before shipping any profile, measure:

- magnitude response at low, turnover, and high frequencies;
- peak output and headroom;
- denormal and NaN behavior;
- stereo/channel independence;
- parameter-change artifacts;
- CPU cost at common sample rates and buffer sizes.

Adaptive treatment is a later phase. A detector that mistakes tyre noise, consonants, or rain for an annoying UI beep is not a comfort feature.
