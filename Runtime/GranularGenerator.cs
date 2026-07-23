using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Unity.IntegerTime;
using Unity.Mathematics;
using Unity.Collections;
using static UnityEngine.Audio.ProcessorInstance;

namespace NotJustSound.GranularSynth
{
    [CreateAssetMenu(fileName = "NewGranularGenerator", menuName = "Audio/Generators/Granular Generator", order = 1)]
    public partial class GranularGenerator : ScriptableObject, IAudioGenerator
    {
        #region Fields

        [Header("General Settings")]
        [Tooltip("When enabled, changes to parameters will be applied to all active instances in real-time.")]
        [SerializeField] private bool liveUpdate = true;

        [Tooltip("The audio source sample to granulate.")]
        [SerializeField] private AudioClip sample;

        [Tooltip("The rate at which new grains are spawned (grains per second).")]
        [SerializeField] private float grainRate = 10f;

        [Tooltip("Average number of grains active simultaneously. Controls grain density.")]
        [SerializeField] private float grainOverlap = 2f;

        [Tooltip("Maximum number of simultaneous voices.")]
        [SerializeField] private int maxVoices = 1;

        [Header("Grain Settings")]
        [Tooltip("Standardized range (0-1) for picking the start position in the sample.")]
        [SerializeField] private Vector2 StartPositionRange;

        [Tooltip("Pitch variation range in semitones.")]
        [SerializeField] private Vector2 GrainPitchRange;

        [Tooltip("Volume variation range in dB.")]
        [SerializeField] private Vector2 GrainVolumeRange;

        [Tooltip("Pan variation range per grain (-1 = full left, 0 = center, +1 = full right).")]
        [SerializeField] private Vector2 GrainPanRange;

        [Tooltip("Power applied to the volume randomization. Adjusts the distribution of grain volumes.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float grainVolumePower = 1f;

        [Tooltip("Grain base duration in milliseconds.")]
        [SerializeField] private float grainDurationBase = 100f;

        [Tooltip("Grain duration randomization percentage.")]
        [Range(0, 100)]
        [SerializeField] private float grainDurationRandom = 0f;

        [Tooltip("Grain duration range in milliseconds (Calculated).")]
        [SerializeField] private Vector2 GrainDurationRange = new Vector2(100, 100);

        [Tooltip("Envelope curve for the grain volume.")]
        [SerializeField] private AnimationCurve grainShape;

        [Tooltip("Power applied to the grain shape curve to translate visual shape to audible gain.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float grainShapePower = 2f;

        [Tooltip("If enabled, will re-randomise grain positions that start within 20ms of another grain. This can help reduce phasing artifacts.")]
        [SerializeField] private bool antiPhasing = false;

        [Header("Modulation Settings")]
        [Tooltip("Modulation settings for various grain parameters.")]
        [SerializeField] private ModulationSetting[] modulationSettings;

        #endregion

        #region Internal State

        // Cache for change detection
        private float m_LastGrainRate;
        private float m_LastGrainOverlap;
        private Vector2 m_LastStartPosition;
        private Vector2 m_LastGrainPitch;
        private Vector2 m_LastGrainVolume;
        private Vector2 m_LastGrainPan;
        private float m_LastGrainVolumePower;
        private Vector2 m_LastGrainDuration;

        private List<GeneratorInstance> m_ActiveInstances = new List<GeneratorInstance>();

        #endregion

        #region Baked Data

        private NativeArray<float> bakedSample;
        private int bakedSampleRate;
        private int bakedSampleLength;
        private int bakedSampleChannels;

        private NativeArray<float> bakedGrainShape;
        private int bakedGrainShapeLength;

        #endregion

        #region IAudioGenerator Implementation

        public bool isFinite => false;
        public bool isRealtime => false;
        public DiscreteTime? length => null;

        private const int MAX_OVERLAP = 20;

        public GeneratorInstance CreateInstance(
            ControlContext context,
            AudioFormat? nestedConfiguration,
            CreationParameters creationParameters)
        {
            BakeSample();
            BakeGrainShape();

            int voiceCount = Mathf.Max(1, maxVoices);
            // Use a fixed pool size based on the maximum supported overlap
            int maxGrainsPerVoice = MAX_OVERLAP + 2; // +2 safety buffer
            int totalGrains = voiceCount * maxGrainsPerVoice;

            // Use AudioSettings.outputSampleRate as fallback
            int outputSampleRate = AudioSettings.outputSampleRate;

            var realtime = new Realtime
            {
                outputSampleRate = outputSampleRate,
                grainRate = grainRate,

                grainPositionRange = StartPositionRange,
                // Pitch: Semitones -> Playrate Multiplier
                grainPitchRange = new float2(
                    math.pow(2.0f, GrainPitchRange.x / 12.0f),
                    math.pow(2.0f, GrainPitchRange.y / 12.0f)
                ),
                // Volume: dB -> Linear Gain
                grainVolumeRange = new float2(
                    math.pow(10.0f, GrainVolumeRange.x / 20.0f),
                    math.pow(10.0f, GrainVolumeRange.y / 20.0f)
                ),
                grainVolumePower = grainVolumePower,
                grainPanRange = GrainPanRange,
                // Pass Duration Range in Seconds for Realtime to use
                grainDurationRange = GrainDurationRange * 0.001f,

                grainShape = bakedGrainShape,
                grainShapeLength = bakedGrainShapeLength,

                sample = bakedSample,
                sampleClipRate = bakedSampleRate,
                sampleLength = bakedSampleLength,
                sampleChannels = bakedSampleChannels,

                voiceCount = voiceCount,
                maxGrainsPerVoice = maxGrainsPerVoice,

                voiceGrainIntervalSamples =
                    new NativeArray<int>(voiceCount, Allocator.Persistent),
                voiceRemainingDelaySamples =
                    new NativeArray<int>(voiceCount, Allocator.Persistent),
                voicePitch =
                    new NativeArray<float>(voiceCount, Allocator.Persistent),
                voiceGain =
                    new NativeArray<float>(voiceCount, Allocator.Persistent),
                voiceRngState =
                    new NativeArray<uint>(voiceCount, Allocator.Persistent),

                grainActive =
                    new NativeArray<byte>(totalGrains, Allocator.Persistent),
                grainPositions =
                    new NativeArray<float>(totalGrains, Allocator.Persistent),
                grainDelaysSamples =
                    new NativeArray<int>(totalGrains, Allocator.Persistent),
                grainPlayrates =
                    new NativeArray<float>(totalGrains, Allocator.Persistent),
                grainGains =
                    new NativeArray<float>(totalGrains, Allocator.Persistent),
                grainPans =
                    new NativeArray<float>(totalGrains, Allocator.Persistent),
                grainShapePlaybackRates =
                    new NativeArray<float>(totalGrains, Allocator.Persistent),
                grainShapeIndices =
                    new NativeArray<float>(totalGrains, Allocator.Persistent),

                activeGrainIndices =
                    new NativeArray<int>(totalGrains, Allocator.Persistent),
                activeGrainCounts =
                    new NativeArray<int>(voiceCount, Allocator.Persistent),

                antiPhasing = antiPhasing
            };

            var instance = context.AllocateGenerator(
                realtime,
                new Control(),
                nestedConfiguration,
                creationParameters);

            m_ActiveInstances.Add(instance);
            return instance;
        }

        #endregion

        #region Properties

        public bool LiveUpdate
        {
            get => liveUpdate;
            set => liveUpdate = value;
        }

        public float GrainRate
        {
            get => grainRate;
            set
            {
                if (Mathf.Abs(grainRate - value) > Mathf.Epsilon)
                {
                    grainRate = value;
                    // Update overlap based on new rate
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainOverlap = grainRate * avgDuration;
                    }
                    BroadcastMessage(new GrainRateEvent(grainRate));
                }
            }
        }

        public float GrainOverlap
        {
            get => grainOverlap;
            set
            {
                if (Mathf.Abs(grainOverlap - value) > Mathf.Epsilon)
                {
                    grainOverlap = Mathf.Max(0.1f, value);
                    // Calculate rate from overlap and average duration
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainRate = grainOverlap / avgDuration;
                        BroadcastMessage(new GrainRateEvent(grainRate));
                    }
                }
            }
        }

        public Vector2 StartPosition
        {
            get => StartPositionRange;
            set
            {
                StartPositionRange = value;
                BroadcastMessage(new GrainPositionEvent(StartPositionRange.x, StartPositionRange.y));
            }
        }

        public Vector2 GrainPitch
        {
            get => GrainPitchRange;
            set
            {
                GrainPitchRange = value;
                BroadcastMessage(new GrainPitchEvent(GrainPitchRange.x, GrainPitchRange.y));
            }
        }

        public Vector2 GrainVolume
        {
            get => GrainVolumeRange;
            set
            {
                GrainVolumeRange = value;
                BroadcastMessage(new GrainVolumeEvent(GrainVolumeRange.x, GrainVolumeRange.y));
            }
        }

        public float GrainVolumePower
        {
            get => grainVolumePower;
            set
            {
                if (Mathf.Abs(grainVolumePower - value) > Mathf.Epsilon)
                {
                    grainVolumePower = value;
                    BroadcastMessage(new GrainVolumePowerEvent(grainVolumePower));
                }
            }
        }

        public Vector2 GrainPan
        {
            get => GrainPanRange;
            set
            {
                GrainPanRange = value;
                BroadcastMessage(new GrainPanEvent(GrainPanRange.x, GrainPanRange.y));
            }
        }

        public float GrainDurationBase
        {
            get => grainDurationBase;
            set
            {
                if (Mathf.Abs(grainDurationBase - value) > Mathf.Epsilon)
                {
                    grainDurationBase = value;
                    UpdateDurationRange();
                    // Recalculate rate from overlap to maintain consistent overlap
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainRate = grainOverlap / avgDuration;
                    }
                    BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
                }
            }
        }

        public float GrainDurationRandom
        {
            get => grainDurationRandom;
            set
            {
                if (Mathf.Abs(grainDurationRandom - value) > Mathf.Epsilon)
                {
                    grainDurationRandom = value;
                    UpdateDurationRange();
                    // Recalculate rate from overlap to maintain consistent overlap
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainRate = grainOverlap / avgDuration;
                    }
                    BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
                }
            }
        }

        public Vector2 GrainDuration
        {
            get => GrainDurationRange;
            set
            {
                GrainDurationRange = value;
                // Note: Setting GrainDurationRange directly bypasses base/random sliders
                // but we keep it for compatibility if something else sets it.
                // We should probably update base/random to reflect the new range if possible,
                // but it's ambiguous. For now just update rate.
                float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                if (avgDuration > 0)
                {
                    grainRate = grainOverlap / avgDuration;
                }
                BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
            }
        }

        public float GrainShapePower
        {
            get => grainShapePower;
            set
            {
                if (Mathf.Abs(grainShapePower - value) > Mathf.Epsilon)
                {
                    grainShapePower = value;
                    ValidateGrainShape(); // This will bake and broadcast
                }
            }
        }

        #endregion

        #region Lifecycle

        private void OnDisable()
        {
            if (bakedSample.IsCreated)
                bakedSample.Dispose();
            if (bakedGrainShape.IsCreated)
                bakedGrainShape.Dispose();
        }

        private void OnValidate()
        {
            if (!liveUpdate) return;

            // 1. Detect primary changes with a stable epsilon
            bool overlapChanged = Mathf.Abs(grainOverlap - m_LastGrainOverlap) > 1e-5f;

            // Update Duration Range from Base/Random %
            UpdateDurationRange();
            bool durationChanged = m_LastGrainDuration != GrainDurationRange;

            bool rateChanged = Mathf.Abs(grainRate - m_LastGrainRate) > 1e-5f;

            // Calculate average duration for linking
            float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;

            // 2. Sync Logic (Hierarchy of Intent)
            // Priority 1: User explicitly changed Overlap -> Update Rate
            if (overlapChanged)
            {
                if (avgDuration > 0)
                {
                    grainRate = grainOverlap / avgDuration;
                    rateChanged = true;
                }
            }
            // Priority 2: User explicitly changed Duration -> Maintain Overlap by adjusting Rate
            else if (durationChanged)
            {
                if (avgDuration > 0)
                {
                    grainRate = grainOverlap / avgDuration;
                    rateChanged = true;
                }
            }
            // Priority 3: User explicitly changed Rate -> Update Overlap to match
            else if (rateChanged)
            {
                grainOverlap = grainRate * avgDuration;
                overlapChanged = true;
            }

            // 3. Update Caches and Broadcast Events
            if (rateChanged)
            {
                m_LastGrainRate = grainRate;
                BroadcastMessage(new GrainRateEvent(grainRate));
            }

            if (overlapChanged)
            {
                m_LastGrainOverlap = grainOverlap;
            }

            if (durationChanged)
            {
                m_LastGrainDuration = GrainDurationRange;
                BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
            }

            if (m_LastStartPosition != StartPositionRange)
            {
                m_LastStartPosition = StartPositionRange;
                BroadcastMessage(new GrainPositionEvent(StartPositionRange.x, StartPositionRange.y));
            }

            if (m_LastGrainPitch != GrainPitchRange)
            {
                m_LastGrainPitch = GrainPitchRange;
                BroadcastMessage(new GrainPitchEvent(GrainPitchRange.x, GrainPitchRange.y));
            }

            if (m_LastGrainVolume != GrainVolumeRange)
            {
                m_LastGrainVolume = GrainVolumeRange;
                BroadcastMessage(new GrainVolumeEvent(GrainVolumeRange.x, GrainVolumeRange.y));
            }

            if (Mathf.Abs(m_LastGrainVolumePower - grainVolumePower) > 1e-5f)
            {
                m_LastGrainVolumePower = grainVolumePower;
                BroadcastMessage(new GrainVolumePowerEvent(grainVolumePower));
            }

            if (m_LastGrainPan != GrainPanRange)
            {
                m_LastGrainPan = GrainPanRange;
                BroadcastMessage(new GrainPanEvent(GrainPanRange.x, GrainPanRange.y));
            }

            ValidateGrainShape();
        }

        #endregion

        #region Baking

        private void BakeSample()
        {
            if (sample == null) return;
            if (bakedSample.IsCreated) return;

            float[] data = new float[sample.samples * sample.channels];
            sample.GetData(data, 0);

            bakedSample = new NativeArray<float>(data, Allocator.Persistent);
            bakedSampleRate = sample.frequency;
            bakedSampleLength = sample.samples;
            bakedSampleChannels = sample.channels;
        }

        private void BakeGrainShape()
        {
            if (grainShape == null || grainShape.length == 0) return;
            if (bakedGrainShape.IsCreated) return;

            float curveLength = grainShape.keys[grainShape.length - 1].time;
            int resolution = Mathf.Max(512, Mathf.CeilToInt(curveLength * 512));

            bakedGrainShape = new NativeArray<float>(resolution, Allocator.Persistent);
            bakedGrainShapeLength = resolution;

            for (int i = 0; i < resolution; i++)
            {
                float t = (float)i / (resolution - 1) * curveLength;
                bakedGrainShape[i] = Mathf.Pow(grainShape.Evaluate(t), grainShapePower);
            }

        }

        private void ValidateGrainShape()
        {
            if (grainShape == null || grainShape.length == 0) return;
            if (!bakedGrainShape.IsCreated) return; // Not baked yet, nothing to update

            float curveLength = grainShape.keys[grainShape.length - 1].time;
            // Assuming resolution is derived from length as in BakeGrainShape
            int newLength = Mathf.Max(512, Mathf.CeilToInt(curveLength * 512));

            if (newLength != bakedGrainShapeLength)
            {
                Debug.LogWarning("GranularGenerator: GrainShape length changed. Runtime update ignored. Please restart playback to apply length changes.");
                return;
            }

            // Check if contents changed
            bool changed = false;
            // Optimization: check a few points or just re-bake and compare?
            // Re-baking is safer.
            var tempShape = new NativeArray<float>(bakedGrainShapeLength, Allocator.Temp);
            for (int i = 0; i < bakedGrainShapeLength; i++)
            {
                float t = (float)i / (bakedGrainShapeLength - 1) * curveLength;
                tempShape[i] = Mathf.Pow(grainShape.Evaluate(t), grainShapePower);
                if (Mathf.Abs(tempShape[i] - bakedGrainShape[i]) > 1e-4f)
                {
                    changed = true;
                }
            }

            if (changed)
            {
                // Update local cache
                bakedGrainShape.CopyFrom(tempShape);

                // Create persistent array for message
                var msgShape = new NativeArray<float>(bakedGrainShapeLength, Allocator.Persistent);
                msgShape.CopyFrom(tempShape);

                BroadcastMessage(new GrainShapeEvent(msgShape));
            }

            tempShape.Dispose();
        }

        #endregion

        #region Helper Methods

        private void UpdateDurationRange()
        {
            float offset = grainDurationBase * (grainDurationRandom * 0.01f);
            GrainDurationRange = new Vector2(
                Mathf.Max(1f, grainDurationBase - offset),
                grainDurationBase + offset
            );
        }

        private void BroadcastMessage<T>(T message) where T : unmanaged
        {
            if (!liveUpdate) return;

            for (int i = m_ActiveInstances.Count - 1; i >= 0; i--)
            {
                var instance = m_ActiveInstances[i];
                if (!ControlContext.builtIn.Exists(instance))
                {
                    m_ActiveInstances.RemoveAt(i);
                    continue;
                }

                ControlContext.builtIn.SendMessage(instance, ref message);
            }
        }

        #endregion
    }
}
