using UnityEngine;
using UnityEngine.Audio;

namespace NotJustSound.GranularSynth
{
    /// <summary>
    /// Provides convenient extension methods to update Granular Generator parameters at runtime.
    /// </summary>
    public static class GranularSynthExtensions
    {
        #region AudioSource Extensions

        public static void SetGrainRate(this AudioSource audioSource, float rate)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainRateEvent(rate));
        }

        public static void SetGrainPosition(this AudioSource audioSource, float min, float max)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainPositionEvent(min, max));
        }

        public static void SetGrainPitch(this AudioSource audioSource, float min, float max)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainPitchEvent(min, max));
        }

        public static void SetGrainVolume(this AudioSource audioSource, float min, float max)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainVolumeEvent(min, max));
        }

        public static void SetGrainVolumePower(this AudioSource audioSource, float power)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainVolumePowerEvent(power));
        }

        public static void SetGrainPan(this AudioSource audioSource, float min, float max)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainPanEvent(min, max));
        }

        public static void SetGrainDuration(this AudioSource audioSource, float min, float max)
        {
            if (audioSource.generator is not GranularGenerator) return;
            var instance = audioSource.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance)) return;

            SendMessage(instance, new GrainDurationEvent(min, max));
        }

        #endregion

        #region GeneratorInstance Extensions

        public static void SetGrainRate(this GeneratorInstance instance, float rate)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainRateEvent(rate));
        }

        public static void SetGrainPosition(this GeneratorInstance instance, float min, float max)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainPositionEvent(min, max));
        }

        public static void SetGrainPitch(this GeneratorInstance instance, float min, float max)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainPitchEvent(min, max));
        }

        public static void SetGrainVolume(this GeneratorInstance instance, float min, float max)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainVolumeEvent(min, max));
        }

        public static void SetGrainVolumePower(this GeneratorInstance instance, float power)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainVolumePowerEvent(power));
        }

        public static void SetGrainPan(this GeneratorInstance instance, float min, float max)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainPanEvent(min, max));
        }

        public static void SetGrainDuration(this GeneratorInstance instance, float min, float max)
        {
            if (!ControlContext.builtIn.Exists(instance)) return;
            SendMessage(instance, new GrainDurationEvent(min, max));
        }

        #endregion

        #region Internal

        private static void SendMessage<T>(ProcessorInstance instance, T message) where T : unmanaged
        {
            ControlContext.builtIn.SendMessage(instance, ref message);
        }

        #endregion
    }
}
