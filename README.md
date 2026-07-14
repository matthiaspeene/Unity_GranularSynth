# Granular Synth

A Granular Synthesis package for Unity.

## Installation

This package can be [installed via the Unity Package Manager](https://docs.unity3d.com/6000.5/Documentation/Manual/upm-ui-giturl.html).

## Features

- Granular Synth, configurable as a ScriptableObject
- Controls: Rate, Playback Position Range, Pitch Range, Volume Range, Pan Range, Grain Size Range
- Configurable Grain Shape
- Stereo Sample Support
- Live Preview
- Toggleable Anti-Phasing
- Range Control script that acts as a macro modulator

## Usage

### Basic Setup

1. Create a Granular Generator by right-clicking in the Assets folder and selecting **Audio → Generators → Granular Generator**.
2. Add the audio file you want to granulate to the **AudioClip** field.
3. Assign the ScriptableObject to an AudioSource in the scene. *Make sure the AudioSource is audible to your AudioListener.*
4. With **Live Preview** enabled, play the scene to hear the granular synth in action.
5. Configure the different parameters of the scriptable object until you achieve the desired result.

### Dynamic Input

*Granular synthesis is best suited for dynamic sounds such as vehicles, scraping, and creaky doors. You may want to configure dynamic control using the* `GranularRangeControl` *MonoBehaviour.*
Attach the script to an AudioSource with a granulator and modulate the input by calling `.setInput(float input)`.

For advanced modulation, you can send your own modulation messages as demonstrated in the `GranularRangeControl` script.

To add additional features or write custom messages, see the [Unity Documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/audio-scriptable-processors.html).
