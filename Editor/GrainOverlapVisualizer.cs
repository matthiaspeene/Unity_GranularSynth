using UnityEngine;
using UnityEngine.UIElements;

namespace NotJustSound.GranularSynth.Editor
{
    /// <summary>
    /// A custom VisualElement that visualizes grain overlap by drawing multiple repetitions
    /// of the grain envelope curve based on grain rate and duration.
    /// </summary>
    [UxmlElement]
    public partial class GrainOverlapVisualizer : VisualElement
    {
        private AnimationCurve _grainShape;
        private float _grainShapePower = 1f;
        private float _grainRate = 10f; // grains per second
        private Vector2 _grainDurationRange = new Vector2(200, 400); // milliseconds

        private const int Resolution = 64; // Samples per grain curve
        private const int MaxGrainsToDisplay = 100; // Increased limit for full-width filling

        private VisualElement _labelContainer;
        private float _timeWindow = 1.0f; // Seconds to display across the width

        public AnimationCurve GrainShape
        {
            get => _grainShape;
            set
            {
                if (_grainShape != value)
                {
                    _grainShape = value;
                    UpdateLabels();
                    MarkDirtyRepaint();
                }
            }
        }

        public float GrainShapePower
        {
            get => _grainShapePower;
            set
            {
                if (Mathf.Abs(_grainShapePower - value) > Mathf.Epsilon)
                {
                    _grainShapePower = value;
                    MarkDirtyRepaint();
                }
            }
        }

        public float GrainRate
        {
            get => _grainRate;
            set
            {
                if (Mathf.Abs(_grainRate - value) > Mathf.Epsilon)
                {
                    _grainRate = value;
                    UpdateLabels();
                    MarkDirtyRepaint();
                }
            }
        }

        public Vector2 GrainDurationRange
        {
            get => _grainDurationRange;
            set
            {
                if (_grainDurationRange != value)
                {
                    _grainDurationRange = value;
                    UpdateLabels();
                    MarkDirtyRepaint();
                }
            }
        }

        public float TimeWindow
        {
            get => _timeWindow;
            set
            {
                if (Mathf.Abs(_timeWindow - value) > Mathf.Epsilon)
                {
                    _timeWindow = value;
                    UpdateLabels();
                    MarkDirtyRepaint();
                }
            }
        }

        public GrainOverlapVisualizer()
        {
            style.minHeight = 100;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden; // Keep everything inside

            _labelContainer = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    left = 0,
                    right = 0,
                    bottom = 0,
                    flexDirection = FlexDirection.Row
                }
            };
            Add(_labelContainer);

            // Initialize labels
            UpdateLabels();

            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_grainShape == null || _grainShape.length == 0 || _grainRate <= 0)
                return;

            var painter = ctx.painter2D;
            var rect = contentRect;

            if (rect.width <= 1 || rect.height <= 1)
                return;

            // Calculate grain timing
            float grainInterval = 1f / _grainRate; // seconds between grains
            float maxDuration = _grainDurationRange.y * 0.001f;
            float minDuration = _grainDurationRange.x * 0.001f;
            float avgDuration = (maxDuration + minDuration) * 0.5f;

            // Define the time window based on the TimeWindow property
            // This defines the "zoom" level
            float totalTimeWindow = _timeWindow;

            // Adjust time window if it's too small for even one grain
            if (totalTimeWindow < maxDuration * 0.1f)
                totalTimeWindow = maxDuration * 0.1f;

            // Calculate how many grains fit in this fixed window
            // We draw grains starting from the left edge until we pass the right edge + duration
            int grainsToDisplay = Mathf.Min(
                Mathf.CeilToInt((totalTimeWindow + maxDuration) / grainInterval) + 1,
                MaxGrainsToDisplay
            );

            // Drawing parameters
            float timeScale = rect.width / totalTimeWindow;
            float heightScale = rect.height * 0.7f; // Use 70% of height for amplitude
            float bottomY = rect.height;

            // Draw background grid (labels are updated separately)
            DrawTimeGrid(painter, rect, totalTimeWindow, timeScale);

            // Draw each grain
            for (int grainIndex = 0; grainIndex < grainsToDisplay; grainIndex++)
            {
                float grainStartTime = grainIndex * grainInterval;

                // Use a stable hash-based random duration for this grain
                // This keeps the visualization consistent even when repainting
                uint seed = (uint)grainIndex + 12345;
                seed = (seed ^ 61) ^ (seed >> 16);
                seed *= 9;
                seed = seed ^ (seed >> 4);
                seed *= 0x27d4eb2d;
                seed = seed ^ (seed >> 15);
                float randomValue = (seed & 0x7FFFFFFF) / (float)0x7FFFFFFF;

                float grainDuration = Mathf.Lerp(minDuration, maxDuration, randomValue);

                // Calculate color based on grain index (fade older grains)
                float alpha = 1f - (grainIndex / (float)grainsToDisplay) * 0.6f;
                Color grainColor = new Color(0.3f, 0.8f, 1f, alpha);

                DrawGrain(painter, grainStartTime, grainDuration, timeScale, bottomY, heightScale, grainColor);
            }
        }

        private void DrawGrain(
            Painter2D painter,
            float startTime,
            float duration,
            float timeScale,
            float baseY,
            float heightScale,
            Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.BeginPath();

            bool firstPoint = true;

            for (int i = 0; i < Resolution; i++)
            {
                float t = i / (float)(Resolution - 1); // 0 to 1
                float time = startTime + t * duration;
                float x = time * timeScale;

                // Evaluate the grain shape curve across its full length
                float curveLength = _grainShape.length > 0 ? _grainShape.keys[_grainShape.length - 1].time : 1f;
                float curveValue = Mathf.Pow(_grainShape.Evaluate(t * curveLength), _grainShapePower);
                float y = baseY - (curveValue * heightScale);

                if (firstPoint)
                {
                    painter.MoveTo(new Vector2(x, y));
                    firstPoint = false;
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }

            painter.Stroke();
        }

        private void UpdateLabels()
        {
            const int FixedGridDivisions = 4; // Always 4 grid lines

            // Manage children for labels
            int childCount = _labelContainer.childCount;
            int totalNeeded = FixedGridDivisions + 1; // +1 for line at 0

            // Add or hide labels as needed
            for (int i = 0; i < Mathf.Max(childCount, totalNeeded); i++)
            {
                Label label;
                if (i < childCount)
                {
                    label = (Label)_labelContainer[i];
                    if (i >= totalNeeded)
                    {
                        label.style.display = DisplayStyle.None;
                        continue;
                    }
                }
                else
                {
                    label = new Label();
                    label.pickingMode = PickingMode.Ignore;
                    label.style.position = Position.Absolute;
                    label.style.fontSize = 9;
                    label.style.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                    label.style.paddingLeft = 2;
                    label.style.top = 2;
                    _labelContainer.Add(label);
                }

                // Calculate time for this grid line
                float t = i / (float)FixedGridDivisions;
                float time = t * _timeWindow;

                // Update label text
                label.style.display = DisplayStyle.Flex;
                if (time < 1.0f)
                    label.text = $"{time * 1000:F0}ms";
                else
                    label.text = $"{time:F2}s";

                // Position will be updated during layout
                label.style.left = new StyleLength(new Length(t * 100f, LengthUnit.Percent));
            }
        }

        private void DrawTimeGrid(
            Painter2D painter,
            Rect rect,
            float totalTimeWindow,
            float timeScale)
        {
            const int FixedGridDivisions = 4; // Always 4 grid lines

            painter.strokeColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            painter.lineWidth = 1f;

            // Draw vertical lines at fixed divisions
            for (int i = 0; i <= FixedGridDivisions; i++)
            {
                float t = i / (float)FixedGridDivisions;
                float x = t * rect.width;

                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, rect.height));
                painter.Stroke();
            }

            // Draw center line (horizontal)
            painter.strokeColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, rect.height * 0.5f));
            painter.LineTo(new Vector2(rect.width, rect.height * 0.5f));
            painter.Stroke();

            // Draw baseline (horizontal)
            painter.strokeColor = new Color(0.8f, 0.8f, 0.8f, 0.2f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, rect.height));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.Stroke();
        }

    }
}
