using UnityEngine;

namespace NotJustSound.GranularSynth
{
    public partial class GranularGenerator
    {
        #region Properties

        public bool LiveUpdate
        {
            get => liveUpdate;
            set => liveUpdate = value;
        }

        public float GrainRate
        {
            get => grainRate;
            set
            {
                if (Mathf.Abs(grainRate - value) > Mathf.Epsilon)
                {
                    grainRate = value;
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainOverlap = grainRate * avgDuration;
                    }

                    BroadcastMessage(new GrainRateEvent(grainRate));
                }
            }
        }

        public float GrainOverlap
        {
            get => grainOverlap;
            set
            {
                if (Mathf.Abs(grainOverlap - value) > Mathf.Epsilon)
                {
                    grainOverlap = Mathf.Max(0.1f, value);
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainRate = grainOverlap / avgDuration;
                        BroadcastMessage(new GrainRateEvent(grainRate));
                    }
                }
            }
        }

        public Vector2 StartPosition
        {
            get => StartPositionRange;
            set
            {
                StartPositionRange = value;
                BroadcastMessage(new GrainPositionEvent(StartPositionRange.x, StartPositionRange.y));
            }
        }

        public Vector2 GrainPitch
        {
            get => GrainPitchRange;
            set
            {
                GrainPitchRange = value;
                BroadcastMessage(new GrainPitchEvent(GrainPitchRange.x, GrainPitchRange.y));
            }
        }

        public Vector2 GrainVolume
        {
            get => GrainVolumeRange;
            set
            {
                GrainVolumeRange = value;
                BroadcastMessage(new GrainVolumeEvent(GrainVolumeRange.x, GrainVolumeRange.y));
            }
        }

        public float GrainVolumePower
        {
            get => grainVolumePower;
            set
            {
                if (Mathf.Abs(grainVolumePower - value) > Mathf.Epsilon)
                {
                    grainVolumePower = value;
                    BroadcastMessage(new GrainVolumePowerEvent(grainVolumePower));
                }
            }
        }

        public Vector2 GrainPan
        {
            get => GrainPanRange;
            set
            {
                GrainPanRange = value;
                BroadcastMessage(new GrainPanEvent(GrainPanRange.x, GrainPanRange.y));
            }
        }

        public float GrainDurationBase
        {
            get => grainDurationBase;
            set
            {
                if (Mathf.Abs(grainDurationBase - value) > Mathf.Epsilon)
                {
                    grainDurationBase = value;
                    UpdateDurationRange();
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainRate = grainOverlap / avgDuration;
                    }

                    BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
                }
            }
        }

        public float GrainDurationRandom
        {
            get => grainDurationRandom;
            set
            {
                if (Mathf.Abs(grainDurationRandom - value) > Mathf.Epsilon)
                {
                    grainDurationRandom = value;
                    UpdateDurationRange();
                    float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                    if (avgDuration > 0)
                    {
                        grainRate = grainOverlap / avgDuration;
                    }

                    BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
                }
            }
        }

        public Vector2 GrainDuration
        {
            get => GrainDurationRange;
            set
            {
                GrainDurationRange = value;
                float avgDuration = (GrainDurationRange.x + GrainDurationRange.y) * 0.5f * 0.001f;
                if (avgDuration > 0)
                {
                    grainRate = grainOverlap / avgDuration;
                }

                BroadcastMessage(new GrainDurationEvent(GrainDurationRange.x, GrainDurationRange.y));
            }
        }

        public float GrainShapePower
        {
            get => grainShapePower;
            set
            {
                if (Mathf.Abs(grainShapePower - value) > Mathf.Epsilon)
                {
                    grainShapePower = value;
                    ValidateGrainShape();
                }
            }
        }

        #endregion
    }
}
