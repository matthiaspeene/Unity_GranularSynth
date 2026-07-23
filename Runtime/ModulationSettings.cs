using System;
using UnityEngine;

namespace NotJustSound.GranularSynth
{
    enum ModulationTarget
    {
        GrainRate,
        GrainPositionOffset,
        GrainPositionWidth,
        GrainPitchOffset,
        GrainPitchWidth,
        GrainVolumeOffset,
        GrainVolumeWidth,
        GrainPanOffset,
        GrainPanWidth,
        GrainVolumePower,
        GrainDurationOffset,
        GrainDurationWidth,
        GrainShapePower
    }

    enum ModulationSource
    {
        Mod1,
        Mod2,
        Mod3
        // Add more modulation sources as needed
    }

    [Serializable]
    struct ModulationSetting
    {
        public ModulationSource source;
        public ModulationTarget target;
        [Range(-1f, 1f)] public float amount;
    }
}
