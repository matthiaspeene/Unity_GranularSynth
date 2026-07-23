using System;
using System.Collections.Generic;
using UnityEngine;

namespace NotJustSound.GranularSynth
{
    public enum ModulationTarget
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

    public static class ModulationSettingRanges
    {
        private static readonly Dictionary<ModulationTarget, Vector2> AmountRanges = new()
        {
            { ModulationTarget.GrainRate, new Vector2(-1000f, 1000f) },
            { ModulationTarget.GrainPositionOffset, new Vector2(-1f, 1f) },
            { ModulationTarget.GrainPositionWidth, new Vector2(0f, 1f) },
            { ModulationTarget.GrainPitchOffset, new Vector2(-24f, 24f) },
            { ModulationTarget.GrainPitchWidth, new Vector2(0f, 24f) },
            { ModulationTarget.GrainVolumeOffset, new Vector2(-60f, 60f) },
            { ModulationTarget.GrainVolumeWidth, new Vector2(0f, 60f) },
            { ModulationTarget.GrainPanOffset, new Vector2(-1f, 1f) },
            { ModulationTarget.GrainPanWidth, new Vector2(0f, 1f) },
            { ModulationTarget.GrainVolumePower, new Vector2(0.01f, 10f) },
            { ModulationTarget.GrainDurationOffset, new Vector2(-1000f, 1000f) },
            { ModulationTarget.GrainDurationWidth, new Vector2(0f, 1000f) },
            { ModulationTarget.GrainShapePower, new Vector2(0.01f, 10f) }
        };

        public static Vector2 GetAmountRange(ModulationTarget target)
        {
            return AmountRanges.TryGetValue(target, out Vector2 range)
                ? range
                : new Vector2(-1f, 1f);
        }
    }

    [Serializable]
    struct ModulationSetting
    {
        public ModulationSource source;
        public ModulationTarget target;
        public float amount;
    }
}
