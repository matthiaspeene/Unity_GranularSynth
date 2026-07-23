// GranularGenerator.Realtime.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Audio;
using Unity.IntegerTime;
using static UnityEngine.Audio.ProcessorInstance;

namespace NotJustSound.GranularSynth
{
    public partial class GranularGenerator
    {
        [BurstCompile(CompileSynchronously = true)]
        public struct Realtime : GeneratorInstance.IRealtime
        {
            #region Parameters
            public float grainRate;
            public float2 grainPositionRange;
            public float2 grainPitchRange;
            public float2 grainVolumeRange;
            public float grainVolumePower;
            public float2 grainPanRange;
            [ReadOnly] public float2 grainDurationRange; // Baked to seconds
            #endregion

            #region Baked Data
            [ReadOnly] public NativeArray<float> grainShape;
            [ReadOnly] public int grainShapeLength;

            [ReadOnly] public NativeArray<float> sample;
            [ReadOnly] public int sampleClipRate;
            [ReadOnly] public int sampleLength;
            [ReadOnly] public int sampleChannels;
            #endregion

            #region Configuration & State
            public int voiceCount;
            public int maxGrainsPerVoice;
            public int outputSampleRate;
            public bool antiPhasing;
            #endregion

            #region Voice State
            public NativeArray<int> voiceGrainIntervalSamples;
            public NativeArray<int> voiceRemainingDelaySamples;

            public NativeArray<float> voicePitch;
            public NativeArray<float> voiceGain;

            public NativeArray<uint> voiceRngState;
            #endregion

            #region Grain State (SoA)
            public NativeArray<byte> grainActive;
            public NativeArray<int> activeGrainIndices;
            public NativeArray<int> activeGrainCounts;

            public NativeArray<float> grainPositions;
            public NativeArray<int> grainDelaysSamples;
            public NativeArray<float> grainPlayrates;
            public NativeArray<float> grainGains;
            public NativeArray<float> grainPans;
            public NativeArray<float> grainShapePlaybackRates;
            public NativeArray<float> grainShapeIndices;
            #endregion

            #region IRealtime Implementation
            public bool isFinite => false;
            public bool isRealtime => false;
            public DiscreteTime? length => null;

            public void Update(UpdatedDataContext context, Pipe pipe)
            {
                foreach (var element in pipe.GetAvailableData(context))
                {
                    if (element.TryGetData(out GrainRateEvent rateEvt))
                    {
                        grainRate = rateEvt.value;
                        int interval = grainRate > 0f
                            ? math.max(1, (int)math.round(outputSampleRate / grainRate))
                            : int.MaxValue;
                        for (int i = 0; i < voiceGrainIntervalSamples.Length; i++)
                            voiceGrainIntervalSamples[i] = interval;
                    }
                    if (element.TryGetData(out GrainPositionEvent posEvt))
                    {
                        grainPositionRange = new float2(posEvt.min, posEvt.max);
                    }
                    if (element.TryGetData(out GrainPitchEvent pitchEvt))
                    {
                        grainPitchRange = new float2(
                            math.pow(2.0f, pitchEvt.min / 12.0f),
                            math.pow(2.0f, pitchEvt.max / 12.0f)
                        );
                    }
                    if (element.TryGetData(out GrainVolumeEvent volEvt))
                    {
                        grainVolumeRange = new float2(
                            math.pow(10.0f, volEvt.min / 20.0f),
                            math.pow(10.0f, volEvt.max / 20.0f)
                        );
                    }
                    if (element.TryGetData(out GrainVolumePowerEvent volPowerEvt))
                    {
                        grainVolumePower = volPowerEvt.value;
                    }
                    if (element.TryGetData(out GrainPanEvent panEvt))
                    {
                        grainPanRange = new float2(panEvt.min, panEvt.max);
                    }
                    if (element.TryGetData(out GrainDurationEvent durEvt))
                    {
                        grainDurationRange = new float2(durEvt.min * 0.001f, durEvt.max * 0.001f);
                    }
                    if (element.TryGetData(out GrainShapeEvent shapeEvt))
                    {
                        if (shapeEvt.shape.Length == grainShapeLength)
                        {
                            grainShape.CopyFrom(shapeEvt.shape);
                        }
                        // End of GrainShapeEvent ownership chain: always dispose here.
                        shapeEvt.shape.Dispose();
                    }
                }
            }

            public GeneratorInstance.Result Process(
                in RealtimeContext context,
                Pipe pipe,
                ChannelBuffer buffer,
                GeneratorInstance.Arguments args)
            {
                // Clear buffer
                for (int i = 0; i < buffer.frameCount; i++)
                {
                    buffer[0, i] = 0f;
                    buffer[1, i] = 0f;
                }

                for (int v = 0; v < voiceCount; v++)
                {
                    int grainBase = v * maxGrainsPerVoice;
                    ScheduleGrains(v, buffer.frameCount, grainBase);
                }

                RenderGrains(buffer);

                return buffer.frameCount;
            }
            #endregion

            #region Internal Logic

            private void ScheduleGrains(int voiceIndex, int bufferSize, int grainBase)
            {
                int interval = voiceGrainIntervalSamples[voiceIndex];
                if (interval <= 0)
                {
                    return;
                }

                int remaining = voiceRemainingDelaySamples[voiceIndex];
                if (remaining >= bufferSize)
                {
                    voiceRemainingDelaySamples[voiceIndex] -= bufferSize;
                    return;
                }

                var rng = new Unity.Mathematics.Random(voiceRngState[voiceIndex]);
                int nextGrainSample = remaining;

                while (nextGrainSample < bufferSize)
                {
                    int grainIndex = FindFreeGrain(grainBase);
                    if (grainIndex < 0)
                    {
                        nextGrainSample += interval;
                        continue;
                    }

                    InitializeGrain(voiceIndex, grainBase, grainIndex, nextGrainSample, ref rng);
                    nextGrainSample += interval;
                }

                voiceRemainingDelaySamples[voiceIndex] = nextGrainSample - bufferSize;
                voiceRngState[voiceIndex] = rng.state;
            }

            private void InitializeGrain(int voiceIndex, int grainBase, int grainIndex, int sampleDelay, ref Unity.Mathematics.Random rng)
            {
                int absoluteIndex = grainBase + grainIndex;
                grainActive[absoluteIndex] = 1;

                int activeCount = activeGrainCounts[voiceIndex];
                if (activeCount < maxGrainsPerVoice)
                {
                    activeGrainIndices[grainBase + activeCount] = grainIndex;
                    activeGrainCounts[voiceIndex] = activeCount + 1;
                }

                // Calculate playrate from duration
                float duration = math.lerp(grainDurationRange.x, grainDurationRange.y, rng.NextFloat());
                float shapePlaybackRate = grainShapeLength / (duration * outputSampleRate);

                float pitch = math.lerp(grainPitchRange.x, grainPitchRange.y, rng.NextFloat());
                float audioInc = pitch * ((float)sampleClipRate / outputSampleRate);
                float ratio = audioInc / shapePlaybackRate;

                grainPositions[absoluteIndex] = math.lerp(grainPositionRange.x, grainPositionRange.y, rng.NextFloat());
                
                if (antiPhasing)
                {
                    bool collisionFound = true;
                    int attempts = 0;
                    int minCollisionWindow = (int)(outputSampleRate * 0.02f); // 20ms threshold in samples

                    while (collisionFound && attempts < 4) // Cap attempts to avoid infinite loops
                    {
                        collisionFound = false;
                        
                        // Current grain's absolute combined baseline time (delay + position converted to samples)
                        float currentPosInSamples = grainPositions[absoluteIndex] * sampleLength;
                        float currentTimelinePos = sampleDelay + currentPosInSamples;

                        for (int i = 0; i < activeCount; i++)
                        {
                            int otherGrainIndex = activeGrainIndices[grainBase + i];
                            int otherAbsoluteIndex = grainBase + otherGrainIndex;
                            
                            int otherDelay = grainDelaysSamples[otherAbsoluteIndex];
                            float otherPosInSamples = grainPositions[otherAbsoluteIndex] * sampleLength;
                            float otherTimelinePos = otherDelay + otherPosInSamples;

                            // Check if they are decoding the same part of the source file at the same time within 20ms
                            if (math.abs(otherTimelinePos - currentTimelinePos) < minCollisionWindow) 
                            {
                                // 1. Re-randomize playback position
                                grainPositions[absoluteIndex] = math.lerp(grainPositionRange.x, grainPositionRange.y, rng.NextFloat());
                                
                                collisionFound = true;
                                attempts++;
                                break;
                            }
                        }
                    }
                }

                grainDelaysSamples[absoluteIndex] = sampleDelay;
                grainPlayrates[absoluteIndex] = ratio;
                float volumeT = math.pow(rng.NextFloat(), grainVolumePower);
                grainGains[absoluteIndex] = math.lerp(grainVolumeRange.x, grainVolumeRange.y, volumeT);
                grainPans[absoluteIndex] = math.lerp(grainPanRange.x, grainPanRange.y, rng.NextFloat());
                grainShapePlaybackRates[absoluteIndex] = shapePlaybackRate;
                grainShapeIndices[absoluteIndex] = 0f;
            }

            private int FindFreeGrain(int grainBase)
            {
                for (int i = 0; i < maxGrainsPerVoice; i++)
                    if (grainActive[grainBase + i] == 0)
                        return i;

                return -1;
            }

            private void RenderGrains(ChannelBuffer buffer)
            {
                for (int v = 0; v < voiceCount; v++)
                {
                    int grainCount = activeGrainCounts[v];
                    int grainBase = v * maxGrainsPerVoice;

                    for (int i = 0; i < grainCount; i++)
                    {
                        int grainIndex = activeGrainIndices[grainBase + i];
                        int absoluteIndex = grainBase + grainIndex;

                        // Security check: if somehow a non-active grain is in the list
                        if (grainActive[absoluteIndex] == 0) continue;

                        int delay = grainDelaysSamples[absoluteIndex];
                        int startSample = 0;
                        if (delay > 0)
                        {
                            if (delay >= buffer.frameCount)
                            {
                                grainDelaysSamples[absoluteIndex] -= buffer.frameCount;
                                continue;
                            }
                            startSample = delay;
                            grainDelaysSamples[absoluteIndex] = 0;
                        }

                        float grainPos = grainPositions[absoluteIndex];
                        float ratio = grainPlayrates[absoluteIndex];
                        float shapePlaybackRate = grainShapePlaybackRates[absoluteIndex];
                        float currentShapeIndex = grainShapeIndices[absoluteIndex];

                        int sLength = sampleLength;
                        float startSourceIndex = grainPos * sLength;

                        bool finished = false;
                        for (int k = startSample; k < buffer.frameCount; k++)
                        {
                            currentShapeIndex += shapePlaybackRate;

                            if (currentShapeIndex >= grainShapeLength)
                            {
                                finished = true;
                                break;
                            }

                            float envVal = SampleEnvelope(currentShapeIndex);
                            float sourceIndex = startSourceIndex + (currentShapeIndex * ratio);

                            if (sourceIndex >= sLength) sourceIndex %= sLength;
                            else if (sourceIndex < 0) sourceIndex = (sourceIndex % sLength) + sLength;

                            float2 audioVal = SampleAudio(sourceIndex);
                            float gain = envVal * grainGains[absoluteIndex] * voiceGain[v];
                            float pan = grainPans[absoluteIndex];
                            float panL = pan <= 0f ? 1f : 1f - pan;
                            float panR = pan >= 0f ? 1f : 1f + pan;

                            buffer[0, k] += audioVal.x * gain * panL;
                            buffer[1, k] += audioVal.y * gain * panR;
                        }

                        if (finished)
                        {
                            grainActive[absoluteIndex] = 0;
                            // Swap remove from active list
                            activeGrainIndices[grainBase + i] = activeGrainIndices[grainBase + grainCount - 1];
                            activeGrainCounts[v] = --grainCount;


                            i--; // Reprocess this index with the swapped grain
                        }
                        else
                        {
                            grainShapeIndices[absoluteIndex] = currentShapeIndex;
                        }
                    }
                }
            }

            private float SampleEnvelope(float index)
            {
                int idx = (int)index;
                float frac = index - idx;

                // Safety check
                if (idx >= grainShapeLength - 1) return grainShape[grainShapeLength - 1];

                return math.lerp(grainShape[idx], grainShape[idx + 1], frac);
            }

            private float2 SampleAudio(float sampleIndex)
            {
                int idx = (int)sampleIndex;
                if (idx >= sampleLength) idx = idx % sampleLength;
                if (idx < 0) idx = 0;

                float frac = sampleIndex - idx;
                int nextIdx = idx + 1;
                if (nextIdx >= sampleLength) nextIdx = 0;

                int baseIdx1 = idx * sampleChannels;
                int baseIdx2 = nextIdx * sampleChannels;

                float left = math.lerp(sample[baseIdx1], sample[baseIdx2], frac);
                if (sampleChannels < 2)
                    return new float2(left, left);

                float right = math.lerp(sample[baseIdx1 + 1], sample[baseIdx2 + 1], frac);
                return new float2(left, right);
            }
            #endregion
        }
    }
}
