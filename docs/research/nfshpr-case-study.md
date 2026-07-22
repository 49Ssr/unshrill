# Case study: Need for Speed Hot Pursuit Remastered

Unshrill grew out of an attempt to remove unusually bright frontend sounds from Need for Speed Hot Pursuit Remastered while preserving its vehicle, weather, music, radio, and gameplay audio.

## What the investigation established

- Criterion `bnd2` bundles can contain individually compressed Wave resources and GeneSys metadata.
- EA SPS/EALayer3 assets can be extracted and decoded with external tools.
- The same resource IDs and event graphs may appear in multiple bundles.
- A structurally valid repack does not prove that the game uses the modified copy at runtime.
- Truncating a timed audio stream can break the UI sequence that waits on it.
- Broad COM/audio hooks can destabilize startup before revealing the useful internal event layer.
- Final-output filtering would be an acceptable temporary comfort feature, even when surgical asset replacement is not economical.
- A static endpoint-wide high-frequency roll-off reduces the target sounds but also removes wanted musical treble and clarity.
- The target is better described as a repeated, short, spectro-temporal event than as "everything above 5 kHz."

## Reusable engineering lessons

1. Separate container validity, decoder validity, runtime selection, and audible effect as four independent claims.
2. Require an untouched round trip before testing a mutation.
3. Use one-variable experiments and restore a known-good baseline between them.
4. Log at the narrowest useful boundary; global hooks produce noise and reentrancy risk.
5. Stop reverse engineering when the solution cost exceeds the value of a general audio-control path.
6. Treat a degraded workaround as an experiment: preserving the target audio is as important as reducing the offender.

The original game-specific tools and repositories remain separate from Unshrill. This case study preserves the methodological value without coupling the new product to one title. The generalized follow-up is documented in [Selective treatment of harsh and intrusive sounds](selective-harshness-treatment.md).

