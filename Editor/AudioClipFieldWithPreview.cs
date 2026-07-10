using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NotJustSound.GranularSynth.Editor
{
    [UxmlElement]
    public partial class AudioClipFieldWithPreview : VisualElement
    {

        private ObjectField _objectField;
        public ObjectField ObjectField => _objectField;
        private AudioClip _clip;
        private float[] _waveformCache;
        private const int Resolution = 256; // Number of bars/samples to cache

        public AudioClip Clip
        {
            get => _clip;
            set
            {
                if (_clip != value)
                {
                    _clip = value;
                    _objectField.SetValueWithoutNotify(value);
                    RebuildWaveform();
                    MarkDirtyRepaint();
                }
            }
        }

        public AudioClipFieldWithPreview()
        {
            style.flexDirection = FlexDirection.Column;
            style.minHeight = 80;

            _objectField = new ObjectField("Sample")
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };

            _objectField.RegisterValueChangedCallback(evt =>
            {
                Clip = evt.newValue as AudioClip;
            });

            Add(_objectField);

            generateVisualContent += OnGenerateVisualContent;
        }

        private void RebuildWaveform()
        {
            if (_clip == null)
            {
                _waveformCache = null;
                return;
            }

            // Guard against large files or unreadable data if necessary
            // For now, assume we can read.
            // If the clip is not readable (e.g. streaming), GetData might fail.
            // A try-catch could be useful, or checking loadState for streaming.
            if (_clip.loadState != AudioDataLoadState.Loaded)
            {
                // Try to load? Or just fail gracefully?
                // Often in editor, it loads automatically basically.
            }

            try
            {
                var samples = new float[_clip.samples * _clip.channels];
                _clip.GetData(samples, 0);

                _waveformCache = new float[Resolution];

                int totalSamples = samples.Length;
                int packSize = totalSamples / Resolution;
                if (packSize < 1) packSize = 1;

                for (int i = 0; i < Resolution; i++)
                {
                    float max = 0;
                    int startSample = i * packSize;

                    // Simple peak detection
                    for (int j = 0; j < packSize; j++)
                    {
                        int index = startSample + j;
                        if (index >= totalSamples) break;

                        float val = Mathf.Abs(samples[index]);
                        if (val > max) max = val;
                    }
                    _waveformCache[i] = max;
                }
            }
            catch
            {
                // In case of error (e.g. valid clip but not readable data), clear cache
                _waveformCache = null;
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_clip == null || _waveformCache == null)
                return;

            var painter = ctx.painter2D;
            var rect = contentRect;

            // Calculate where to draw. We want to draw below the ObjectField.
            // We can approximate or use flex. 
            // If the element has a fixed height (e.g. 80), and ObjectField is ~20, 
            // we have ~60px for waveform.

            float topOffset = _objectField.resolvedStyle.height;
            if (float.IsNaN(topOffset) || topOffset < 1) topOffset = 20;
            topOffset += 2; // small padding from the field

            float availableHeight = rect.height - topOffset;
            if (availableHeight <= 1) return;

            painter.lineWidth = 1.0f;
            painter.strokeColor = new Color(0.3f, 0.6f, 1f, 1f); // Nice waveform color

            float widthPerSample = rect.width / (float)Resolution;
            float midY = topOffset + (availableHeight * 0.5f);
            float heightScale = availableHeight * 0.5f;

            painter.BeginPath();
            for (int i = 0; i < Resolution; i++)
            {
                float x = i * widthPerSample;
                float height = _waveformCache[i] * heightScale;

                // Draw vertical line centered on midY
                painter.MoveTo(new Vector2(x, midY - height));
                painter.LineTo(new Vector2(x, midY + height));
            }
            painter.Stroke();
        }
    }
}
