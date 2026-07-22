using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NotJustSound.GranularSynth;

namespace NotJustSound.GranularSynth.Editor
{
    [CustomEditor(typeof(GranularGenerator))]
    public class GranularGeneratorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/studio.notjustsound.granular/Editor/GranularGeneratorView.uxml");
            if (visualTree == null)
            {
                root.Add(new Label("Could not load UXML: Packages/studio.notjustsound.granular/Editor/GranularGeneratorView.uxml"));
                return root;
            }

            visualTree.CloneTree(root);

            // Bind explicitly where names do not match
            // 'sample' and 'grainShape' match names in UXML and class, so they might auto-bind if we call Bind(serializedObject)
            // But for clarity and robustness against renaming, we can bind everything or just the mismatches.

            // Mismatches:
            // UXML: startPosition -> Class: StartPositionRange
            // UXML: grainVolume -> Class: GrainVolumeRange
            // UXML: grainPitch -> Class: GrainPitchRange
            // UXML: grainDuration -> Class: GrainDurationRange

            var liveUpdateField = root.Q<Toggle>("liveUpdate");
            if (liveUpdateField != null) liveUpdateField.BindProperty(serializedObject.FindProperty("liveUpdate"));

            var startPositionField = root.Q<MinMaxSlider>("startPosition");
            if (startPositionField != null) startPositionField.BindProperty(serializedObject.FindProperty("StartPositionRange"));

            var antiPhasingField = root.Q<Toggle>("antiPhasing");
            if (antiPhasingField != null) antiPhasingField.BindProperty(serializedObject.FindProperty("antiPhasing"));

            var rateField = root.Q<Slider>("grainRate");
            if (rateField != null)
            {
                rateField.BindProperty(serializedObject.FindProperty("grainRate"));
                // Update overlap when rate changes manually
                rateField.RegisterValueChangedCallback(evt =>
                {
                    float durationBase = serializedObject.FindProperty("grainDurationBase").floatValue;
                    float durationRandom = serializedObject.FindProperty("grainDurationRandom").floatValue;

                    float offset = durationBase * (durationRandom * 0.01f);
                    float min = Mathf.Max(1f, durationBase - offset);
                    float max = durationBase + offset;
                    float avgDuration = (min + max) * 0.5f * 0.001f;

                    if (avgDuration > 0)
                    {
                        var overlapProp = serializedObject.FindProperty("grainOverlap");
                        overlapProp.floatValue = evt.newValue * avgDuration;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }

            var overlapField = root.Q<Slider>("grainOverlap");
            if (overlapField != null)
            {
                overlapField.BindProperty(serializedObject.FindProperty("grainOverlap"));
                // Update rate when overlap changes manually
                overlapField.RegisterValueChangedCallback(evt =>
                {
                    float durationBase = serializedObject.FindProperty("grainDurationBase").floatValue;
                    float durationRandom = serializedObject.FindProperty("grainDurationRandom").floatValue;

                    float offset = durationBase * (durationRandom * 0.01f);
                    float min = Mathf.Max(1f, durationBase - offset);
                    float max = durationBase + offset;
                    float avgDuration = (min + max) * 0.5f * 0.001f;

                    if (avgDuration > 0)
                    {
                        var rateProp = serializedObject.FindProperty("grainRate");
                        rateProp.floatValue = evt.newValue / avgDuration;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }

            var grainVolumeField = root.Q<MinMaxSlider>("grainVolume");
            if (grainVolumeField != null) grainVolumeField.BindProperty(serializedObject.FindProperty("GrainVolumeRange"));

            var grainVolumePowerField = root.Q<Slider>("grainVolumePower");
            if (grainVolumePowerField != null) grainVolumePowerField.BindProperty(serializedObject.FindProperty("grainVolumePower"));

            var grainPitchField = root.Q<MinMaxSlider>("grainPitch");
            if (grainPitchField != null) grainPitchField.BindProperty(serializedObject.FindProperty("GrainPitchRange"));

            var grainPanField = root.Q<MinMaxSlider>("grainPan");
            if (grainPanField != null) grainPanField.BindProperty(serializedObject.FindProperty("GrainPanRange"));

            var grainDurationField = root.Q<Slider>("grainDuration");
            if (grainDurationField != null)
            {
                grainDurationField.BindProperty(serializedObject.FindProperty("grainDurationBase"));
                // Update rate when duration changes to maintain overlap
                grainDurationField.RegisterValueChangedCallback(evt =>
                {
                    var overlapProp = serializedObject.FindProperty("grainOverlap");
                    float overlap = overlapProp.floatValue;

                    float durationBase = evt.newValue;
                    float durationRandom = serializedObject.FindProperty("grainDurationRandom").floatValue;

                    // Calculate expected range to get accurate avg duration
                    float offset = durationBase * (durationRandom * 0.01f);
                    float min = Mathf.Max(1f, durationBase - offset);
                    float max = durationBase + offset;
                    float avgDuration = (min + max) * 0.5f * 0.001f;

                    if (avgDuration > 0)
                    {
                        var rateProp = serializedObject.FindProperty("grainRate");
                        rateProp.floatValue = overlap / avgDuration;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }

            var grainDurationRandomField = root.Q<Slider>("grainDurationRandom");
            if (grainDurationRandomField != null)
            {
                grainDurationRandomField.BindProperty(serializedObject.FindProperty("grainDurationRandom"));
                // Update rate when randomization changes (as it shifts avg duration if min hits 1ms)
                grainDurationRandomField.RegisterValueChangedCallback(evt =>
                {
                    var overlapProp = serializedObject.FindProperty("grainOverlap");
                    float overlap = overlapProp.floatValue;

                    float durationBase = serializedObject.FindProperty("grainDurationBase").floatValue;
                    float durationRandom = evt.newValue;

                    // Calculate expected range to get accurate avg duration
                    float offset = durationBase * (durationRandom * 0.01f);
                    float min = Mathf.Max(1f, durationBase - offset);
                    float max = durationBase + offset;
                    float avgDuration = (min + max) * 0.5f * 0.001f;

                    if (avgDuration > 0)
                    {
                        var rateProp = serializedObject.FindProperty("grainRate");
                        rateProp.floatValue = overlap / avgDuration;
                        serializedObject.ApplyModifiedProperties();
                    }
                });
            }

            // 'sample' and 'grainShape' should bind automatically if we do nothing else and rely on names, 
            // but since we are manually binding some, let's just make sure everything is handled.
            // Actually, BindProperty is the UI Toolkit way for manual binding.

            var sampleField = root.Q<AudioClipFieldWithPreview>("sample");
            // If AudioClipFieldWithPreview inherits from BindableElement and implements INotifyValueChanged, 
            // BindProperty should work. If it's just a VisualElement wrapping things, we might need to check.
            // We expose the internal ObjectField to allow binding.
            if (sampleField != null) sampleField.ObjectField.BindProperty(serializedObject.FindProperty("sample"));

            var grainShapeField = root.Q<CurveField>("grainShape");
            if (grainShapeField != null) grainShapeField.BindProperty(serializedObject.FindProperty("grainShape"));

            var grainShapePowerField = root.Q<Slider>("grainShapePower");
            if (grainShapePowerField != null) grainShapePowerField.BindProperty(serializedObject.FindProperty("grainShapePower"));

            // Bind and update the GrainOverlapVisualizer
            var overlapVisualizer = root.Q<GrainOverlapVisualizer>("grainOverlapVisualizer");
            if (overlapVisualizer != null)
            {
                // Initial update
                UpdateOverlapVisualizer(overlapVisualizer, serializedObject);

                // Track changes to update the visualizer
                root.TrackSerializedObjectValue(serializedObject, so => UpdateOverlapVisualizer(overlapVisualizer, so));

                // Add a zoom slider for the visualizer
                var zoomSlider = new Slider("Visualizer Zoom (seconds)", 0.1f, 5.0f)
                {
                    value = 1.0f,
                    showInputField = true
                };
                zoomSlider.RegisterValueChangedCallback(evt =>
                {
                    overlapVisualizer.TimeWindow = evt.newValue;
                });

                // Insert the zoom slider right before the visualizer
                var visualizerParent = overlapVisualizer.parent;
                if (visualizerParent != null)
                {
                    int visualizerIndex = visualizerParent.IndexOf(overlapVisualizer);
                    visualizerParent.Insert(visualizerIndex, zoomSlider);
                }
            }

            return root;
        }

        private void UpdateOverlapVisualizer(GrainOverlapVisualizer visualizer, SerializedObject so)
        {
            visualizer.GrainShape = so.FindProperty("grainShape").animationCurveValue;
            visualizer.GrainShapePower = so.FindProperty("grainShapePower").floatValue;
            visualizer.GrainRate = so.FindProperty("grainRate").floatValue;
            visualizer.GrainDurationRange = so.FindProperty("GrainDurationRange").vector2Value;
        }
    }
}
