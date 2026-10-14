using CatMatch3.Core;
using UnityEngine;

namespace CatMatch3.Runtime
{
    public enum AudioCue { Select, Invalid, Match, Striped, Combo, Win, Fail }

    // Presentation-only latch/cooldowns, independent of AudioSource for standalone validation.
    internal sealed class AudioCueGate
    {
        private readonly float[] next = new float[7];
        private bool terminalPlayed;

        public bool TryEnter(AudioCue cue, float now)
        {
            int index = (int)cue;
            if (index < 0 || index >= next.Length || terminalPlayed) return false;
            if (cue == AudioCue.Win || cue == AudioCue.Fail) { terminalPlayed = true; return true; }
            if (now < next[index]) return false;
            float gap = cue == AudioCue.Select ? 0.06f : cue == AudioCue.Match ? 0.08f : 0.16f;
            next[index] = now + gap;
            return true;
        }

        public void Reset()
        {
            terminalPlayed = false;
            for (int i = 0; i < next.Length; i++) next[i] = float.NegativeInfinity;
        }
    }

    /// <summary>Two reusable 2D sources and seven saved clips; no gameplay decisions or timed callbacks.</summary>
    [DisallowMultipleComponent]
    public sealed class BoardAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip selectClip;
        [SerializeField] private AudioClip invalidClip;
        [SerializeField] private AudioClip matchClip;
        [SerializeField] private AudioClip stripedClip;
        [SerializeField] private AudioClip comboClip;
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip failClip;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.6f;
        private const string Preference = "CatMatch3.AudioEnabled";
        private readonly AudioCueGate gate = new AudioCueGate();
        private AudioSource effects, results;
        public bool AudioEnabled { get; private set; }

        private void Awake() { Initialize(); }

        public void Initialize()
        {
            if (effects != null) return;
            effects = CreateSource();
            results = CreateSource();
            AudioEnabled = PlayerPrefs.GetInt(Preference, 1) != 0;
            gate.Reset();
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = masterVolume;
            return source;
        }

        public void ToggleEnabled()
        {
            AudioEnabled = !AudioEnabled;
            if (!AudioEnabled) StopPlayback();
            PlayerPrefs.SetInt(Preference, AudioEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Play(AudioCue cue)
        {
            if (!isActiveAndEnabled || effects == null || !gate.TryEnter(cue, Time.unscaledTime)) return;
            // Consume terminal events while muted too; unmuting cannot replay an old result.
            if (!AudioEnabled) return;
            var clip = GetClip(cue);
            if (clip == null) return;
            bool terminal = cue == AudioCue.Win || cue == AudioCue.Fail;
            if (terminal) StopPlayback();
            (terminal ? results : effects).PlayOneShot(clip, Gain(cue));
        }

        private AudioClip GetClip(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.Select: return selectClip;
                case AudioCue.Invalid: return invalidClip;
                case AudioCue.Match: return matchClip;
                case AudioCue.Striped: return stripedClip;
                case AudioCue.Combo: return comboClip;
                case AudioCue.Win: return winClip;
                case AudioCue.Fail: return failClip;
                default: return null;
            }
        }

        private static float Gain(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.Select: return 0.22f;
                case AudioCue.Invalid: return 0.35f;
                case AudioCue.Match: return 0.42f;
                case AudioCue.Striped: return 0.32f;
                case AudioCue.Combo: return 0.32f;
                case AudioCue.Win: return 0.48f;
                case AudioCue.Fail: return 0.42f;
                default: return 0f;
            }
        }

        public void Cancel()
        {
            StopPlayback();
            gate.Reset();
        }

        private void StopPlayback()
        {
            if (effects != null) effects.Stop();
            if (results != null) results.Stop();
        }

        private void OnDisable() { Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) StopPlayback(); }

        public static AudioCue? ResultCue(GameplayState state)
        {
            if (state == GameplayState.Win) return AudioCue.Win;
            if (state == GameplayState.Fail) return AudioCue.Fail;
            return null;
        }
    }
}
