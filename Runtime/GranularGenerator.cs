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
        private ModulationSetting[] m_LastModulationSettings;

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
                new Control(
                    this,
                    modulationSettings,
                    grainRate,
                    StartPositionRange,
                    GrainPitchRange,
                    GrainVolumeRange,
                    GrainPanRange,
                    grainVolumePower,
                    GrainDurationRange,
                    grainShapePower),
                nestedConfiguration,
                creationParameters);

            m_ActiveInstances.Add(instance);
            return instance;
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

            SyncRateOverlapDuration(out bool rateChanged, out bool overlapChanged, out bool durationChanged);
            BroadcastChangedParameters(rateChanged, overlapChanged, durationChanged);

            if (HasModulationMatrixChanged())
            {
                BroadcastModulationMatrix();
            }

            ValidateGrainShape();
        }

        #endregion

        #region Helper Methods

        private const float ChangeEpsilon = 1e-5f;

        private void SyncRateOverlapDuration(out bool rateChanged, out bool overlapChanged, out bool durationChanged)
        {
            overlapChanged = IsChanged(grainOverlap, m_LastGrainOverlap);

            UpdateDurationRange();
            durationChanged = IsChanged(GrainDurationRange, m_LastGrainDuration);
            rateChanged = IsChanged(grainRate, m_LastGrainRate);

            float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
            if (avgDuration <= 0f)
            {
                return;
            }

            if (overlapChanged || durationChanged)
            {
                grainRate = grainOverlap / avgDuration;
                rateChanged = true;
            }
            else if (rateChanged)
            {
                grainOverlap = grainRate * avgDuration;
                overlapChanged = true;
            }
        }

        private void BroadcastChangedParameters(bool rateChanged, bool overlapChanged, bool durationChanged)
        {
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

            if (UpdateIfChanged(ref m_LastStartPosition, StartPositionRange))
            {
                BroadcastMessage(new GrainPositionEvent(StartPositionRange.x, StartPositionRange.y));
            }

            if (UpdateIfChanged(ref m_LastGrainPitch, GrainPitchRange))
            {
                BroadcastMessage(new GrainPitchEvent(GrainPitchRange.x, GrainPitchRange.y));
            }

            if (UpdateIfChanged(ref m_LastGrainVolume, GrainVolumeRange))
            {
                BroadcastMessage(new GrainVolumeEvent(GrainVolumeRange.x, GrainVolumeRange.y));
            }

            if (UpdateIfChanged(ref m_LastGrainVolumePower, grainVolumePower))
            {
                BroadcastMessage(new GrainVolumePowerEvent(grainVolumePower));
            }

            if (UpdateIfChanged(ref m_LastGrainPan, GrainPanRange))
            {
                BroadcastMessage(new GrainPanEvent(GrainPanRange.x, GrainPanRange.y));
            }
        }

        private void UpdateDurationRange()
        {
            float offset = grainDurationBase * (grainDurationRandom * 0.01f);
            GrainDurationRange = new Vector2(
                Mathf.Max(1f, grainDurationBase - offset),
                grainDurationBase + offset
            );
        }

        private static bool IsChanged(float current, float previous)
        {
            return Mathf.Abs(current - previous) > ChangeEpsilon;
        }

        private static bool IsChanged(Vector2 current, Vector2 previous)
        {
            return current != previous;
        }

        private static bool UpdateIfChanged(ref float previous, float current)
        {
            if (!IsChanged(current, previous))
            {
                return false;
            }

            previous = current;
            return true;
        }

        private static bool UpdateIfChanged(ref Vector2 previous, Vector2 current)
        {
            if (!IsChanged(current, previous))
            {
                return false;
            }

            previous = current;
            return true;
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

        private bool HasModulationMatrixChanged()
        {
            if (m_LastModulationSettings == null)
            {
                return modulationSettings != null && modulationSettings.Length > 0;
            }

            if (modulationSettings == null)
            {
                return m_LastModulationSettings.Length > 0;
            }

            if (m_LastModulationSettings.Length != modulationSettings.Length)
            {
                return true;
            }

            for (int i = 0; i < modulationSettings.Length; i++)
            {
                if (m_LastModulationSettings[i].source != modulationSettings[i].source ||
                    m_LastModulationSettings[i].target != modulationSettings[i].target ||
                    !Mathf.Approximately(m_LastModulationSettings[i].amount, modulationSettings[i].amount))
                {
                    return true;
                }
            }

            return false;
        }

        private void BroadcastModulationMatrix()
        {
            if (!liveUpdate || !HasLiveInstances())
            {
                return;
            }

            m_LastModulationSettings = modulationSettings != null
                ? (ModulationSetting[])modulationSettings.Clone()
                : null;

            var matrix = modulationSettings != null
                ? new NativeArray<ModulationSetting>(modulationSettings, Allocator.Persistent)
                : new NativeArray<ModulationSetting>(0, Allocator.Persistent);

            BroadcastMessage(new ModulationMatrixEvent(matrix));
        }

        private bool HasLiveInstances()
        {
            for (int i = m_ActiveInstances.Count - 1; i >= 0; i--)
            {
                if (ControlContext.builtIn.Exists(m_ActiveInstances[i]))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
