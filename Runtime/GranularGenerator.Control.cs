using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using Unity.Collections;
using static UnityEngine.Audio.ProcessorInstance;

namespace NotJustSound.GranularSynth
{
    public partial class GranularGenerator
    {
        struct Control : GeneratorInstance.IControl<Realtime>
        {
            private IntPtr generatorHandle;
            private NativeArray<ModulationSetting> modulationSettings;

            private float baseGrainRate;
            private Vector2 baseStartPosition;
            private Vector2 baseGrainPitch;
            private Vector2 baseGrainVolume;
            private Vector2 baseGrainPan;
            private float baseGrainVolumePower;
            private Vector2 baseGrainDuration;
            private float baseGrainShapePower;

            private float modulation1;
            private float modulation2;
            private float modulation3;

            private float lastGrainRate;
            private Vector2 lastStartPosition;
            private Vector2 lastGrainPitch;
            private Vector2 lastGrainVolume;
            private Vector2 lastGrainPan;
            private float lastGrainVolumePower;
            private Vector2 lastGrainDuration;
            private float lastGrainShapePower;

            public Control(
                GranularGenerator generator,
                ModulationSetting[] modulationSettings,
                float baseGrainRate,
                Vector2 baseStartPosition,
                Vector2 baseGrainPitch,
                Vector2 baseGrainVolume,
                Vector2 baseGrainPan,
                float baseGrainVolumePower,
                Vector2 baseGrainDuration,
                float baseGrainShapePower)
            {
                generatorHandle = GCHandle.ToIntPtr(GCHandle.Alloc(generator));
                this.modulationSettings = new NativeArray<ModulationSetting>(
                    modulationSettings ?? Array.Empty<ModulationSetting>(),
                    Allocator.Persistent);

                this.baseGrainRate = baseGrainRate;
                this.baseStartPosition = baseStartPosition;
                this.baseGrainPitch = baseGrainPitch;
                this.baseGrainVolume = baseGrainVolume;
                this.baseGrainPan = baseGrainPan;
                this.baseGrainVolumePower = baseGrainVolumePower;
                this.baseGrainDuration = baseGrainDuration;
                this.baseGrainShapePower = baseGrainShapePower;

                modulation1 = 0f;
                modulation2 = 0f;
                modulation3 = 0f;

                lastGrainRate = float.NaN;
                lastStartPosition = new Vector2(float.NaN, float.NaN);
                lastGrainPitch = new Vector2(float.NaN, float.NaN);
                lastGrainVolume = new Vector2(float.NaN, float.NaN);
                lastGrainPan = new Vector2(float.NaN, float.NaN);
                lastGrainVolumePower = float.NaN;
                lastGrainDuration = new Vector2(float.NaN, float.NaN);
                lastGrainShapePower = float.NaN;
            }

            public void Dispose(ControlContext context, ref Realtime realtime)
            {
                if (modulationSettings.IsCreated)
                {
                    modulationSettings.Dispose();
                }

                if (generatorHandle != IntPtr.Zero)
                {
                    GCHandle.FromIntPtr(generatorHandle).Free();
                    generatorHandle = IntPtr.Zero;
                }

                // Voice State
                if (realtime.voiceGrainIntervalSamples.IsCreated) realtime.voiceGrainIntervalSamples.Dispose();
                if (realtime.voiceRemainingDelaySamples.IsCreated) realtime.voiceRemainingDelaySamples.Dispose();
                if (realtime.voicePitch.IsCreated) realtime.voicePitch.Dispose();
                if (realtime.voiceGain.IsCreated) realtime.voiceGain.Dispose();
                if (realtime.voiceRngState.IsCreated) realtime.voiceRngState.Dispose();

                // Grain SoA State
                if (realtime.grainActive.IsCreated) realtime.grainActive.Dispose();
                if (realtime.grainPositions.IsCreated) realtime.grainPositions.Dispose();
                if (realtime.grainDelaysSamples.IsCreated) realtime.grainDelaysSamples.Dispose();
                if (realtime.grainPlayrates.IsCreated) realtime.grainPlayrates.Dispose();
                if (realtime.grainGains.IsCreated) realtime.grainGains.Dispose();
                if (realtime.grainPans.IsCreated) realtime.grainPans.Dispose();
                if (realtime.grainShapePlaybackRates.IsCreated) realtime.grainShapePlaybackRates.Dispose();
                if (realtime.grainShapeIndices.IsCreated) realtime.grainShapeIndices.Dispose();

                if (realtime.activeGrainIndices.IsCreated) realtime.activeGrainIndices.Dispose();
                if (realtime.activeGrainCounts.IsCreated) realtime.activeGrainCounts.Dispose();
            }

            public void Update(ControlContext context, Pipe pipe)
            {

            }

            public Response OnMessage(ControlContext context, Pipe pipe, Message message)
            {
                #region Modulation Messages
                if (message.Is<Modulation1Event>())
                {
                    modulation1 = message.Get<Modulation1Event>().value;
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<Modulation2Event>())
                {
                    modulation2 = message.Get<Modulation2Event>().value;
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<Modulation3Event>())
                {
                    modulation3 = message.Get<Modulation3Event>().value;
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                #endregion
                #region Internal Messages
                if (message.Is<GrainRateEvent>())
                {
                    baseGrainRate = message.Get<GrainRateEvent>().value;
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainPositionEvent>())
                {
                    var evt = message.Get<GrainPositionEvent>();
                    baseStartPosition = new Vector2(evt.min, evt.max);
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainPitchEvent>())
                {
                    var evt = message.Get<GrainPitchEvent>();
                    baseGrainPitch = new Vector2(evt.min, evt.max);
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainVolumeEvent>())
                {
                    var evt = message.Get<GrainVolumeEvent>();
                    baseGrainVolume = new Vector2(evt.min, evt.max);
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainVolumePowerEvent>())
                {
                    baseGrainVolumePower = message.Get<GrainVolumePowerEvent>().value;
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainPanEvent>())
                {
                    var evt = message.Get<GrainPanEvent>();
                    baseGrainPan = new Vector2(evt.min, evt.max);
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainDurationEvent>())
                {
                    var evt = message.Get<GrainDurationEvent>();
                    baseGrainDuration = new Vector2(evt.min, evt.max);
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                if (message.Is<GrainShapeEvent>())
                {
                    // Forward without disposing; ownership continues to Realtime.Update.
                    pipe.SendData(context, message.Get<GrainShapeEvent>());
                    return Response.Handled;
                }
                if (message.Is<ModulationMatrixEvent>())
                {
                    var evt = message.Get<ModulationMatrixEvent>();

                    if (modulationSettings.IsCreated)
                    {
                        modulationSettings.Dispose();
                    }

                    modulationSettings = evt.settings;
                    PushModulatedOutputs(context, pipe);
                    return Response.Handled;
                }
                #endregion
                return Response.Unhandled;
            }

            public void Configure(
                ControlContext context,
                ref Realtime realtime,
                in AudioFormat format,
                out GeneratorInstance.Setup setup,
                ref GeneratorInstance.Properties properties)
            {
                setup = new GeneratorInstance.Setup(AudioSpeakerMode.Stereo, format.sampleRate);

                realtime.outputSampleRate = format.sampleRate;

                int interval = realtime.grainRate > 0f
                    ? math.max(1, (int)math.round(format.sampleRate / realtime.grainRate))
                    : int.MaxValue;


                for (int v = 0; v < realtime.voiceCount; v++)
                {
                    realtime.voiceGrainIntervalSamples[v] = interval;
                    realtime.voiceRemainingDelaySamples[v] = 0;
                    realtime.voicePitch[v] = 1f;
                    realtime.voiceGain[v] = 1f;
                    realtime.voiceRngState[v] = (uint)(v + 1);
                    realtime.activeGrainCounts[v] = 0;
                }

                SeedCurrentOutputs();
            }

            private void SeedCurrentOutputs()
            {
                lastGrainRate = EvaluateScalar(ModulationTarget.GrainRate, baseGrainRate, minimum: 0f);
                lastStartPosition = EvaluateStartPosition();
                lastGrainPitch = EvaluateRange(ModulationTarget.GrainPitchOffset, ModulationTarget.GrainPitchWidth, baseGrainPitch);
                lastGrainVolume = EvaluateRange(ModulationTarget.GrainVolumeOffset, ModulationTarget.GrainVolumeWidth, baseGrainVolume);
                lastGrainPan = EvaluateRange(ModulationTarget.GrainPanOffset, ModulationTarget.GrainPanWidth, baseGrainPan, -1f, 1f);
                lastGrainVolumePower = EvaluateScalar(ModulationTarget.GrainVolumePower, baseGrainVolumePower, minimum: 0.1f, maximum: 5f);
                lastGrainDuration = EvaluateDurationRange();
                lastGrainShapePower = EvaluateScalar(ModulationTarget.GrainShapePower, baseGrainShapePower, minimum: 0.1f, maximum: 2f);
                ApplyGrainShapePower(lastGrainShapePower);
            }

            private void PushModulatedOutputs(ControlContext context, Pipe pipe)
            {
                float grainRate = EvaluateScalar(ModulationTarget.GrainRate, baseGrainRate, minimum: 0f);
                Vector2 startPosition = EvaluateStartPosition();
                Vector2 grainPitch = EvaluateRange(ModulationTarget.GrainPitchOffset, ModulationTarget.GrainPitchWidth, baseGrainPitch);
                Vector2 grainVolume = EvaluateRange(ModulationTarget.GrainVolumeOffset, ModulationTarget.GrainVolumeWidth, baseGrainVolume);
                Vector2 grainPan = EvaluateRange(ModulationTarget.GrainPanOffset, ModulationTarget.GrainPanWidth, baseGrainPan, -1f, 1f);
                float grainVolumePower = EvaluateScalar(ModulationTarget.GrainVolumePower, baseGrainVolumePower, minimum: 0.1f, maximum: 5f);
                Vector2 grainDuration = EvaluateDurationRange();
                float grainShapePower = EvaluateScalar(ModulationTarget.GrainShapePower, baseGrainShapePower, minimum: 0.1f, maximum: 2f);

                if (UpdateIfChanged(ref lastGrainRate, grainRate))
                {
                    pipe.SendData(context, new GrainRateEvent(grainRate));
                }

                if (UpdateIfChanged(ref lastStartPosition, startPosition))
                {
                    pipe.SendData(context, new GrainPositionEvent(startPosition.x, startPosition.y));
                }

                if (UpdateIfChanged(ref lastGrainPitch, grainPitch))
                {
                    pipe.SendData(context, new GrainPitchEvent(grainPitch.x, grainPitch.y));
                }

                if (UpdateIfChanged(ref lastGrainVolume, grainVolume))
                {
                    pipe.SendData(context, new GrainVolumeEvent(grainVolume.x, grainVolume.y));
                }

                if (UpdateIfChanged(ref lastGrainVolumePower, grainVolumePower))
                {
                    pipe.SendData(context, new GrainVolumePowerEvent(grainVolumePower));
                }

                if (UpdateIfChanged(ref lastGrainPan, grainPan))
                {
                    pipe.SendData(context, new GrainPanEvent(grainPan.x, grainPan.y));
                }

                if (UpdateIfChanged(ref lastGrainDuration, grainDuration))
                {
                    pipe.SendData(context, new GrainDurationEvent(grainDuration.x, grainDuration.y));
                }

                if (UpdateIfChanged(ref lastGrainShapePower, grainShapePower))
                {
                    ApplyGrainShapePower(grainShapePower);
                }
            }

            private void ApplyGrainShapePower(float value)
            {
                if (generatorHandle == IntPtr.Zero)
                {
                    return;
                }

                var generator = GCHandle.FromIntPtr(generatorHandle).Target as GranularGenerator;
                if (generator == null)
                {
                    return;
                }

                generator.GrainShapePower = value;
            }

            private float EvaluateScalar(ModulationTarget target, float baseValue, float minimum = float.NegativeInfinity, float maximum = float.PositiveInfinity)
            {
                float value = baseValue + EvaluateContribution(target);
                if (!float.IsNegativeInfinity(minimum) || !float.IsPositiveInfinity(maximum))
                {
                    value = Mathf.Clamp(value, minimum, maximum);
                }

                return value;
            }

            private Vector2 EvaluateStartPosition()
            {
                float offset = EvaluateContribution(ModulationTarget.GrainPositionOffset);
                float widthDelta = EvaluateContribution(ModulationTarget.GrainPositionWidth);

                float width = Mathf.Max(0f, (baseStartPosition.y - baseStartPosition.x) + widthDelta);
                width = Mathf.Min(1f, width);

                float min = baseStartPosition.x + offset;
                float maxMin = Mathf.Max(0f, 1f - width);
                min = Mathf.Clamp(min, 0f, maxMin);

                return new Vector2(min, min + width);
            }

            private Vector2 EvaluateRange(ModulationTarget offsetTarget, ModulationTarget widthTarget, Vector2 baseRange)
            {
                float offset = EvaluateContribution(offsetTarget);
                float widthDelta = EvaluateContribution(widthTarget);

                float min = baseRange.x + offset;
                float max = baseRange.y + offset + widthDelta;
                if (max < min)
                {
                    max = min;
                }

                return new Vector2(min, max);
            }

            private Vector2 EvaluateRange(ModulationTarget offsetTarget, ModulationTarget widthTarget, Vector2 baseRange, float minClamp, float maxClamp)
            {
                Vector2 value = EvaluateRange(offsetTarget, widthTarget, baseRange);
                value.x = Mathf.Clamp(value.x, minClamp, maxClamp);
                value.y = Mathf.Clamp(value.y, minClamp, maxClamp);

                if (value.y < value.x)
                {
                    value.y = value.x;
                }

                return value;
            }

            private Vector2 EvaluateDurationRange()
            {
                Vector2 value = EvaluateRange(ModulationTarget.GrainDurationOffset, ModulationTarget.GrainDurationWidth, baseGrainDuration);
                value.x = Mathf.Max(1f, value.x);
                value.y = Mathf.Max(value.x, value.y);
                return value;
            }

            private float EvaluateContribution(ModulationTarget target)
            {
                if (!modulationSettings.IsCreated || modulationSettings.Length == 0)
                {
                    return 0f;
                }

                float total = 0f;
                for (int i = 0; i < modulationSettings.Length; i++)
                {
                    ModulationSetting setting = modulationSettings[i];
                    if (setting.target != target)
                    {
                        continue;
                    }

                    total += setting.amount * GetModulationValue(setting.source);
                }

                return total;
            }

            private float GetModulationValue(ModulationSource source)
            {
                return source switch
                {
                    ModulationSource.Mod1 => Mathf.Clamp01(modulation1),
                    ModulationSource.Mod2 => Mathf.Clamp01(modulation2),
                    ModulationSource.Mod3 => Mathf.Clamp01(modulation3),
                    _ => 0f
                };
            }

            private static bool UpdateIfChanged(ref float previous, float current)
            {
                if (Mathf.Approximately(previous, current))
                {
                    return false;
                }

                previous = current;
                return true;
            }

            private static bool UpdateIfChanged(ref Vector2 previous, Vector2 current)
            {
                if (previous == current)
                {
                    return false;
                }

                previous = current;
                return true;
            }
        }
    }
}
