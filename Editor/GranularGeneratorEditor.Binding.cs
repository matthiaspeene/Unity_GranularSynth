using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NotJustSound.GranularSynth.Editor
{
    public partial class GranularGeneratorEditor
    {
        private void BindInspector(VisualElement root)
        {
            BindFixedProperties(root);
            BindCoupledGrainTimingProperties(root);
            BindCurveAndAudioFields(root);
            BindVisualizer(root);
            BindModulationSettings(root);
        }

        private void BindFixedProperties(VisualElement root)
        {
            BindProperty(root, "liveUpdate", "liveUpdate");
            BindProperty(root, "startPosition", "StartPositionRange");
            BindProperty(root, "antiPhasing", "antiPhasing");
            BindProperty(root, "grainVolume", "GrainVolumeRange");
            BindProperty(root, "grainVolumePower", "grainVolumePower");
            BindProperty(root, "grainPitch", "GrainPitchRange");
            BindProperty(root, "grainPan", "GrainPanRange");
            BindProperty(root, "grainShapePower", "grainShapePower");
        }

        private void BindCoupledGrainTimingProperties(VisualElement root)
        {
            BindRateField(root);
            BindOverlapField(root);
            BindDurationField(root);
            BindDurationRandomField(root);
        }

        private void BindCurveAndAudioFields(VisualElement root)
        {
            var sampleField = root.Q<AudioClipFieldWithPreview>("sample");
            if (sampleField != null)
            {
                var sampleProperty = serializedObject.FindProperty("sample");
                if (sampleProperty != null)
                {
                    sampleField.ObjectField.BindProperty(sampleProperty);
                }
            }

            BindProperty(root, "grainShape", "grainShape");
        }

        private void BindRateField(VisualElement root)
        {
            var rateField = root.Q<Slider>("grainRate");
            if (rateField == null)
            {
                return;
            }

            var rateProperty = serializedObject.FindProperty("grainRate");
            if (rateProperty != null)
            {
                rateField.BindProperty(rateProperty);
            }

            rateField.RegisterValueChangedCallback(evt =>
            {
                UpdateOverlapFromRate(evt.newValue);
            });
        }

        private void BindOverlapField(VisualElement root)
        {
            var overlapField = root.Q<Slider>("grainOverlap");
            if (overlapField == null)
            {
                return;
            }

            var overlapProperty = serializedObject.FindProperty("grainOverlap");
            if (overlapProperty != null)
            {
                overlapField.BindProperty(overlapProperty);
            }

            overlapField.RegisterValueChangedCallback(evt =>
            {
                UpdateRateFromOverlap(evt.newValue);
            });
        }

        private void BindDurationField(VisualElement root)
        {
            var grainDurationField = root.Q<Slider>("grainDuration");
            if (grainDurationField == null)
            {
                return;
            }

            var durationProperty = serializedObject.FindProperty("grainDurationBase");
            if (durationProperty != null)
            {
                grainDurationField.BindProperty(durationProperty);
            }

            grainDurationField.RegisterValueChangedCallback(evt =>
            {
                UpdateRateFromDuration(evt.newValue);
            });
        }

        private void BindDurationRandomField(VisualElement root)
        {
            var grainDurationRandomField = root.Q<Slider>("grainDurationRandom");
            if (grainDurationRandomField == null)
            {
                return;
            }

            var durationRandomProperty = serializedObject.FindProperty("grainDurationRandom");
            if (durationRandomProperty != null)
            {
                grainDurationRandomField.BindProperty(durationRandomProperty);
            }

            grainDurationRandomField.RegisterValueChangedCallback(evt =>
            {
                UpdateRateFromDurationRandom(evt.newValue);
            });
        }

        private void BindProperty(VisualElement root, string queryName, string propertyName)
        {
            var field = root.Q<BindableElement>(queryName);
            if (field == null)
            {
                return;
            }

            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                field.BindProperty(property);
            }
        }

        private void BindVisualizer(VisualElement root)
        {
            var overlapVisualizer = root.Q<GrainOverlapVisualizer>("grainOverlapVisualizer");
            if (overlapVisualizer == null)
            {
                return;
            }

            UpdateOverlapVisualizer(overlapVisualizer, serializedObject);

            var zoomSlider = new Slider("Visualizer Zoom (seconds)", 0.1f, 5.0f)
            {
                value = 1.0f,
                showInputField = true
            };

            zoomSlider.RegisterValueChangedCallback(evt =>
            {
                overlapVisualizer.TimeWindow = evt.newValue;
            });

            var visualizerParent = overlapVisualizer.parent;
            if (visualizerParent != null)
            {
                int visualizerIndex = visualizerParent.IndexOf(overlapVisualizer);
                visualizerParent.Insert(visualizerIndex, zoomSlider);
            }
        }

        private void BindModulationSettings(VisualElement root)
        {
            var modulationSettingsContainer = root.Q<VisualElement>("modulationSettingsList");
            var addModulationSettingButton = root.Q<Button>("addModulationSetting");
            var overlapVisualizer = root.Q<GrainOverlapVisualizer>("grainOverlapVisualizer");

            void RefreshModulationSettings()
            {
                if (modulationSettingsContainer != null)
                {
                    RebuildModulationSettingsList(modulationSettingsContainer, serializedObject);
                }
            }

            RefreshModulationSettings();

            root.TrackSerializedObjectValue(serializedObject, so =>
            {
                if (overlapVisualizer != null)
                {
                    UpdateOverlapVisualizer(overlapVisualizer, so);
                }

                RefreshModulationSettings();
            });

            if (addModulationSettingButton != null)
            {
                addModulationSettingButton.clicked += () =>
                {
                    AddModulationSetting(serializedObject);
                    RefreshModulationSettings();
                };
            }
        }

        private float GetAverageDurationSeconds(float durationBase, float durationRandom)
        {
            float offset = durationBase * (durationRandom * 0.01f);
            float min = Mathf.Max(1f, durationBase - offset);
            float max = durationBase + offset;
            return (min + max) * 0.5f * 0.001f;
        }

        private void UpdateOverlapFromRate(float rate)
        {
            float durationBase = serializedObject.FindProperty("grainDurationBase").floatValue;
            float durationRandom = serializedObject.FindProperty("grainDurationRandom").floatValue;
            float avgDuration = GetAverageDurationSeconds(durationBase, durationRandom);

            if (avgDuration <= 0)
            {
                return;
            }

            var overlapProp = serializedObject.FindProperty("grainOverlap");
            if (overlapProp != null)
            {
                overlapProp.floatValue = rate * avgDuration;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void UpdateRateFromOverlap(float overlap)
        {
            float durationBase = serializedObject.FindProperty("grainDurationBase").floatValue;
            float durationRandom = serializedObject.FindProperty("grainDurationRandom").floatValue;
            float avgDuration = GetAverageDurationSeconds(durationBase, durationRandom);

            if (avgDuration <= 0)
            {
                return;
            }

            var rateProp = serializedObject.FindProperty("grainRate");
            if (rateProp != null)
            {
                rateProp.floatValue = overlap / avgDuration;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void UpdateRateFromDuration(float durationBase)
        {
            var overlapProp = serializedObject.FindProperty("grainOverlap");
            if (overlapProp == null)
            {
                return;
            }

            float overlap = overlapProp.floatValue;
            float durationRandom = serializedObject.FindProperty("grainDurationRandom").floatValue;
            float avgDuration = GetAverageDurationSeconds(durationBase, durationRandom);

            if (avgDuration <= 0)
            {
                return;
            }

            var rateProp = serializedObject.FindProperty("grainRate");
            if (rateProp != null)
            {
                rateProp.floatValue = overlap / avgDuration;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void UpdateRateFromDurationRandom(float durationRandom)
        {
            var overlapProp = serializedObject.FindProperty("grainOverlap");
            if (overlapProp == null)
            {
                return;
            }

            float overlap = overlapProp.floatValue;
            float durationBase = serializedObject.FindProperty("grainDurationBase").floatValue;
            float avgDuration = GetAverageDurationSeconds(durationBase, durationRandom);

            if (avgDuration <= 0)
            {
                return;
            }

            var rateProp = serializedObject.FindProperty("grainRate");
            if (rateProp != null)
            {
                rateProp.floatValue = overlap / avgDuration;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}