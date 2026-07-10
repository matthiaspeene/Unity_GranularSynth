using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace NotJustSound.GranularSynth {
    public class GranularRangeControl : MonoBehaviour {

        [SerializeField] private Vector2 inputRange = new Vector2(0f, 1f);

        [SerializeField] private Vector2 playRateRange = new Vector2(20f, 100f);

        [SerializeField] private float playbackRangeWidth = 0.8f;

        [SerializeField] private Vector2 playbackRangeScale = new Vector2(0f, 0.8f);

        [SerializeField] private float pitchRangeWidth = 1.5f;

        [SerializeField] private Vector2 pitchScale = new Vector2(0f, -2f);

        [SerializeField] private float gainRangeWidth = 0.1f;

        [SerializeField] private Vector2 gainScale = new Vector2(0.5f, 1f);

        private AudioSource granularSource;

        private void Start() {
            granularSource = GetComponent<AudioSource>();
            setInput(0f);
        }

        /// <summary>
        /// Sets the input value that gets remapped to a range of granular synthesis parameters.
        /// </summary>
        public void setInput(float input) {
            float normalizedInput = Mathf.InverseLerp(inputRange.x, inputRange.y, input);

            float playbackRangeStart = Mathf.Lerp(playbackRangeScale.x, (playbackRangeScale.y - playbackRangeWidth), normalizedInput);
            float playRate = Mathf.Lerp(playRateRange.x, playRateRange.y, normalizedInput);
            float pitch = Mathf.Lerp(pitchScale.x, (pitchScale.y - pitchRangeWidth), normalizedInput);
            float gain = Mathf.Lerp(gainScale.x, (gainScale.y - gainRangeWidth), normalizedInput);

            granularSource.SetGrainPosition(playbackRangeStart, playbackRangeStart + playbackRangeWidth);
            granularSource.SetGrainRate(playRate);
            granularSource.SetGrainPitch(pitch, pitch + pitchRangeWidth);
            granularSource.SetGrainVolume(gain, gain + gainRangeWidth);
        }
    }
}
