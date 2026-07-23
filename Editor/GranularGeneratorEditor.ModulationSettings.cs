using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NotJustSound.GranularSynth.Editor
{
    public partial class GranularGeneratorEditor
    {
        private void AddModulationSetting(SerializedObject serializedObject)
        {
            var modulationSettingsProperty = serializedObject.FindProperty("modulationSettings");
            if (modulationSettingsProperty == null || !modulationSettingsProperty.isArray)
            {
                return;
            }

            serializedObject.Update();

            int newIndex = modulationSettingsProperty.arraySize;
            modulationSettingsProperty.arraySize++;

            var elementProperty = modulationSettingsProperty.GetArrayElementAtIndex(newIndex);
            var targetProperty = elementProperty.FindPropertyRelative("target");
            if (targetProperty != null)
            {
                targetProperty.enumValueIndex = 0;
            }

            var amountProperty = elementProperty.FindPropertyRelative("amount");
            if (amountProperty != null)
            {
                amountProperty.floatValue = 0f;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void RebuildModulationSettingsList(VisualElement container, SerializedObject serializedObject)
        {
            container.Clear();

            var modulationSettingsProperty = serializedObject.FindProperty("modulationSettings");
            if (modulationSettingsProperty == null || !modulationSettingsProperty.isArray)
            {
                container.Add(new Label("Modulation settings are unavailable."));
                return;
            }

            for (int i = 0; i < modulationSettingsProperty.arraySize; i++)
            {
                var elementProperty = modulationSettingsProperty.GetArrayElementAtIndex(i);
                container.Add(CreateModulationSettingRow(elementProperty, i, serializedObject));
            }
        }

        private VisualElement CreateModulationSettingRow(SerializedProperty elementProperty, int index, SerializedObject serializedObject)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            AddModulationPopupField(row, elementProperty, index, serializedObject, "source", 90, 6);
            AddModulationPopupField(row, elementProperty, index, serializedObject, "target", 120, 6);
            AddModulationAmountField(row, elementProperty, index, serializedObject);
            AddRemoveButton(row, index, serializedObject);

            return row;
        }

        private static void AddModulationPopupField(
            VisualElement row,
            SerializedProperty elementProperty,
            int index,
            SerializedObject serializedObject,
            string propertyName,
            int minWidth,
            int marginRight)
        {
            var property = elementProperty.FindPropertyRelative(propertyName);
            if (property == null)
            {
                return;
            }

            var choices = new List<string>(property.enumDisplayNames);
            if (choices.Count == 0)
            {
                return;
            }

            var currentIndex = Mathf.Clamp(property.enumValueIndex, 0, choices.Count - 1);
            var field = new PopupField<string>(choices, currentIndex)
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = minWidth,
                    marginRight = marginRight
                }
            };

            field.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var modulationSettings = serializedObject.FindProperty("modulationSettings");
                if (modulationSettings == null || index >= modulationSettings.arraySize)
                {
                    return;
                }

                var refreshedElement = modulationSettings.GetArrayElementAtIndex(index);
                var refreshedProperty = refreshedElement.FindPropertyRelative(propertyName);
                if (refreshedProperty != null)
                {
                    refreshedProperty.enumValueIndex = choices.IndexOf(evt.newValue);
                    serializedObject.ApplyModifiedProperties();
                }
            });

            row.Add(field);
        }

        private static void AddModulationAmountField(VisualElement row, SerializedProperty elementProperty, int index, SerializedObject serializedObject)
        {
            var amountProperty = elementProperty.FindPropertyRelative("amount");
            if (amountProperty == null)
            {
                return;
            }

            var targetProperty = elementProperty.FindPropertyRelative("target");
            var amountRange = targetProperty != null
                ? ModulationSettingRanges.GetAmountRange((ModulationTarget)targetProperty.enumValueIndex)
                : new Vector2(-1f, 1f);

            var amountField = new Slider(amountRange.x, amountRange.y)
            {
                value = amountProperty.floatValue,
                showInputField = true,
                style =
                {
                    minWidth = 180
                }
            };

            amountField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var modulationSettings = serializedObject.FindProperty("modulationSettings");
                if (modulationSettings == null || index >= modulationSettings.arraySize)
                {
                    return;
                }

                var refreshedElement = modulationSettings.GetArrayElementAtIndex(index);
                var refreshedAmount = refreshedElement.FindPropertyRelative("amount");
                if (refreshedAmount != null)
                {
                    refreshedAmount.floatValue = Mathf.Clamp(evt.newValue, amountRange.x, amountRange.y);
                    serializedObject.ApplyModifiedProperties();
                }
            });

            row.Add(amountField);
        }

        private static void AddRemoveButton(VisualElement row, int index, SerializedObject serializedObject)
        {
            var removeButton = new Button(() =>
            {
                serializedObject.Update();
                var modulationSettings = serializedObject.FindProperty("modulationSettings");
                if (modulationSettings == null || !modulationSettings.isArray || index >= modulationSettings.arraySize)
                {
                    return;
                }

                modulationSettings.DeleteArrayElementAtIndex(index);
                if (index < modulationSettings.arraySize)
                {
                    modulationSettings.DeleteArrayElementAtIndex(index);
                }

                serializedObject.ApplyModifiedProperties();
            })
            {
                text = "×",
                tooltip = "Remove modulation target"
            };

            removeButton.style.width = 20;
            removeButton.style.height = 20;
            removeButton.style.marginLeft = 6;
            removeButton.style.paddingLeft = 0;
            removeButton.style.paddingRight = 0;
            removeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(removeButton);
        }
    }
}