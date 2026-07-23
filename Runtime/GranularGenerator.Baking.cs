using Unity.Collections;
using UnityEngine;

namespace NotJustSound.GranularSynth
{
    public partial class GranularGenerator
    {
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
            if (!bakedGrainShape.IsCreated) return;

            float curveLength = grainShape.keys[grainShape.length - 1].time;
            int newLength = Mathf.Max(512, Mathf.CeilToInt(curveLength * 512));

            if (newLength != bakedGrainShapeLength)
            {
                Debug.LogWarning("GranularGenerator: GrainShape length changed. Runtime update ignored. Please restart playback to apply length changes.");
                return;
            }

            bool changed = false;
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
                bakedGrainShape.CopyFrom(tempShape);

                if (HasLiveInstances())
                {
                    // Ownership is transferred with this message and disposed in Realtime.Update.
                    var msgShape = new NativeArray<float>(bakedGrainShapeLength, Allocator.Persistent);
                    msgShape.CopyFrom(tempShape);
                    BroadcastMessage(new GrainShapeEvent(msgShape));
                }
            }

            tempShape.Dispose();
        }

        #endregion
    }
}
