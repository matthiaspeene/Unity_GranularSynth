using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NotJustSound.GranularSynth;

namespace NotJustSound.GranularSynth.Editor
{
    [CustomEditor(typeof(GranularGenerator))]
    public partial class GranularGeneratorEditor : UnityEditor.Editor
    {
        private const string VisualTreePath = "Packages/studio.notjustsound.granular/Editor/GranularGeneratorView.uxml";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
            if (visualTree == null)
            {
                root.Add(new Label($"Could not load UXML: {VisualTreePath}"));
                return root;
            }

            visualTree.CloneTree(root);

            BindInspector(root);

            return root;
        }

    }
}
