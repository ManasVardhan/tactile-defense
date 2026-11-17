using UnityEngine;

namespace TactileWars.Audio
{
    /// <summary>Persistent music player with two sources that crossfade when the theme changes (menu to battle and
    /// back). Created on first use and kept across scene loads. Silent in batchmode (automated runs).</summary>
    public class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer instance;
        static float volume = 0.7f;

        AudioSource a, b;
        AudioSource current;
        float targetBase = 0.35f;
        float fade = 1f;
        const float FadeTime = 1.2f;

        /// <summary>Player music volume (0..1) from the settings screen.</summary>
        public static float Volume
        {
            get => volume;
            set { volume = Mathf.Clamp01(value); if (instance != null) instance.ApplyVolumes(); }
        }

        public static AudioClip CurrentClip => instance != null && instance.current != null ? instance.current.clip : null;

        static MusicPlayer Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
            instance.a = go.AddComponent<AudioSource>();
            instance.b = go.AddComponent<AudioSource>();
            foreach (var s in new[] { instance.a, instance.b }) { s.loop = true; s.playOnAwake = false; s.volume = 0f; s.ignoreListenerPause = true; }
            return instance;
        }

        /// <summary>Crossfades to 'clip' (no-op when it is already playing).</summary>
        public static void Play(AudioClip clip, float baseVolume)
        {
            if (clip == null || Application.isBatchMode) return;
            var p = Ensure();
            p.targetBase = baseVolume;
            if (p.current != null && p.current.clip == clip && p.current.isPlaying) { p.ApplyVolumes(); return; }
            var next = p.current == p.a ? p.b : p.a;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            p.current = next;
            p.fade = 0f;
        }

        void Update()
        {
            if (fade >= 1f) return;
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / FadeTime);
            ApplyVolumes();
            if (fade >= 1f)
            {
                var other = current == a ? b : a;
                other.Stop();
            }
        }

        void ApplyVolumes()
        {
            float v = targetBase * volume;
            var other = current == a ? b : a;
            if (current != null) current.volume = v * fade;
            if (other != null && other.isPlaying) other.volume = v * (1f - fade);
        }
    }
}
