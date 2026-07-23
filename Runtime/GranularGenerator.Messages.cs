using Unity.Collections;

namespace NotJustSound.GranularSynth
{
    // Message structs for modulation updates
    public readonly struct Modulation1Event
    {
        public readonly float value;
        public Modulation1Event(float value) => this.value = value;
    }

    public readonly struct Modulation2Event
    {
        public readonly float value;
        public Modulation2Event(float value) => this.value = value;
    }

    public readonly struct Modulation3Event
    {
        public readonly float value;
        public Modulation3Event(float value) => this.value = value;
    }

    // Message structs for direct parameter updates
    public readonly struct GrainRateEvent
    {
        public readonly float value;
        public GrainRateEvent(float value) => this.value = value;
    }

    public readonly struct GrainPositionEvent
    {
        public readonly float min, max;
        public GrainPositionEvent(float min, float max) { this.min = min; this.max = max; }
    }

    public readonly struct GrainPitchEvent
    {
        public readonly float min, max;
        public GrainPitchEvent(float min, float max) { this.min = min; this.max = max; }
    }

    public readonly struct GrainVolumeEvent
    {
        public readonly float min, max;
        public GrainVolumeEvent(float min, float max) { this.min = min; this.max = max; }
    }

    public readonly struct GrainVolumePowerEvent
    {
        public readonly float value;
        public GrainVolumePowerEvent(float value) => this.value = value;
    }

    public readonly struct GrainPanEvent
    {
        public readonly float min, max;
        public GrainPanEvent(float min, float max) { this.min = min; this.max = max; }
    }

    public readonly struct GrainDurationEvent
    {
        public readonly float min, max;
        public GrainDurationEvent(float min, float max) { this.min = min; this.max = max; }
    }

    public readonly struct GrainShapeEvent
    {
        public readonly NativeArray<float> shape;
        public GrainShapeEvent(NativeArray<float> shape) => this.shape = shape;
    }
}
