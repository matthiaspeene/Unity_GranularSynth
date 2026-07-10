using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.Audio.ProcessorInstance;

namespace NotJustSound.GranularSynth
{
    public partial class GranularGenerator
    {
        struct Control : GeneratorInstance.IControl<Realtime>
        {
            public void Dispose(ControlContext context, ref Realtime realtime)
            {
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
                if (message.Is<GrainRateEvent>())
                {
                    pipe.SendData(context, message.Get<GrainRateEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainPositionEvent>())
                {
                    pipe.SendData(context, message.Get<GrainPositionEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainPitchEvent>())
                {
                    pipe.SendData(context, message.Get<GrainPitchEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainVolumeEvent>())
                {
                    pipe.SendData(context, message.Get<GrainVolumeEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainVolumePowerEvent>())
                {
                    pipe.SendData(context, message.Get<GrainVolumePowerEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainPanEvent>())
                {
                    pipe.SendData(context, message.Get<GrainPanEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainDurationEvent>())
                {
                    pipe.SendData(context, message.Get<GrainDurationEvent>());
                    return Response.Handled;
                }
                if (message.Is<GrainShapeEvent>())
                {
                    pipe.SendData(context, message.Get<GrainShapeEvent>());
                    return Response.Handled;
                }

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
            }
        }
    }
}
