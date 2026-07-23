# Roadmap

## Planned

These features are planned for future releases.

### Fix: Random Seeding

Currently, all synths running at the same time use the same seed, resulting in overlapping grain patterns.

### Fix: Anti-Phasing

We are still experiencing some phasing when grains have little or no pitch randomization and are in close proximity. I want to further improve the anti-phasing system.

### Fix: Sample Memory

The scriptable audio pipeline cant actually load clips. I baked the sample into an array (basically pcm). However the old samples stay there. I want to explicitally unload the original sample from memory after we copied it.

---

## Ideas

These are features I'm currently exploring. They are **not guaranteed** to be added.

### Examples
I want to make some examples so it's more clear what this system can do and how to use it.
Examples:
Vehicle Sound System
Light Saber Sound System
Weather System
Velocity based scraping rock

### Replacing the Grain Curve with Math-Based Shapes

The grain curve currently needs to be read from memory, which isn't ideal.

It also requires users to draw their own curve, which can sometimes be awkward or produce unexpected results.

I'm considering replacing it with a mathematically generated shape that's configurable through a few parameters.

This would likely be both more efficient (fewer memory reads) and easier to use.

I could still keep the drawable curve as an optional mode.

### Different Grain System: Smart Grains

Currently, we load the entire sample.

I'm exploring an alternative granular synth that selects pre-baked transients from a sample and has noise as a background layer.
This is more optimised.

The idea is inspired by **Data-Driven Granular Synthesis**:
https://youtu.be/FEK8Ggg2xxQ?si=DSImpiGxnQgPFHsL&t=347

---

If you're particularly interested in a specific feature, feel free to send me a message.
