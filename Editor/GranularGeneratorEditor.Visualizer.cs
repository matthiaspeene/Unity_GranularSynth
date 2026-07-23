using UnityEditor;

namespace NotJustSound.GranularSynth.Editor
{
    public partial class GranularGeneratorEditor
    {
        private void UpdateOverlapVisualizer(GrainOverlapVisualizer visualizer, SerializedObject so)
        {
            visualizer.GrainShape = so.FindProperty("grainShape").animationCurveValue;
            visualizer.GrainShapePower = so.FindProperty("grainShapePower").floatValue;
            visualizer.GrainRate = so.FindProperty("grainRate").floatValue;
            visualizer.GrainDurationRange = so.FindProperty("GrainDurationRange").vector2Value;
        }
    }
}