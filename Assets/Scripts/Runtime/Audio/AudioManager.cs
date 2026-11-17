using UnityEngine;

namespace TactileWars.Audio
{
    /// <summary>Pooled 2D AudioSources with per sound voice limiting and min interval, so 40 guns
    /// produce a texture instead of a wall of noise. Loops (music, stroke) use dedicated sources.</summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager instance;
        public SoundLibrary library;
        public int voices = 20;

        AudioSource[] sources;
        int[] sourceSound;          // SoundId index currently playing on each source, -1 when free
        float[] lastPlay;
        AudioSource music, loop;

        public static bool Muted { get; set; }
        /// <summary>Player sfx volume (0..1) from the settings screen.</summary>
        public static float SfxVolume { get; set; } = 1f;
        public static AudioManager Instance => instance;
        public SoundLibrary Library => library;
        [Tooltip("Which music this scene plays through MusicPlayer (crossfaded).")] public bool menuMusic;

        void Awake()
        {
            instance = this;
            sources = new AudioSource[voices];
            sourceSound = new int[voices];
            for (int i = 0; i < voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sourceSound[i] = -1;
            }
            int n = System.Enum.GetValues(typeof(SoundId)).Length;
            lastPlay = new float[n];
            for (int i = 0; i < n; i++) lastPlay[i] = -10f;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false;
            loop = gameObject.AddComponent<AudioSource>();
            loop.loop = true; loop.playOnAwake = false;
        }

        void Start()
        {
            // Music lives in the persistent MusicPlayer so menu and battle themes crossfade across scene loads.
            if (library != null)
            {
                var clip = menuMusic ? library.menuMusic : library.battleMusic;
                if (clip != null) MusicPlayer.Play(clip, library.musicVolume * library.masterVolume);
            }
        }

        void OnDestroy() { if (instance == this) instance = null; }

        public static void Play(SoundId id, Vector3 pos) { if (instance != null) instance.PlayInternal(id); }

        public static void SetLoop(SoundId id, bool on)
        {
            if (instance == null || instance.library == null) return;
            var l = instance.loop;
            if (!on) { if (l.isPlaying) l.Stop(); return; }
            var e = instance.library.Find(id);
            if (e == null || e.clips.Length == 0 || e.clips[0] == null) return;
            if (l.isPlaying && l.clip == e.clips[0]) return;
            l.clip = e.clips[0];
            l.volume = e.volume * instance.library.masterVolume * SfxVolume;
            l.Play();
        }

        void PlayInternal(SoundId id)
        {
            if (Muted || library == null || SfxVolume <= 0.001f) return;
            var e = library.Find(id);
            if (e == null || e.clips == null || e.clips.Length == 0) return;
            int idx = (int)id;
            float now = Time.unscaledTime;
            if (now - lastPlay[idx] < e.minInterval) return;

            // Recount voices for this sound (sources finish on their own).
            int playing = 0;
            int free = -1;
            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].isPlaying) { sourceSound[i] = -1; if (free < 0) free = i; continue; }
                if (sourceSound[i] == idx) playing++;
            }
            if (playing >= e.maxVoices || free < 0) return;

            var clip = e.clips[Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            var s = sources[free];
            s.clip = clip;
            s.volume = e.volume * library.masterVolume * SfxVolume;
            s.pitch = Random.Range(e.pitch.x, e.pitch.y);
            s.Play();
            sourceSound[free] = idx;
            lastPlay[idx] = now;
        }
    }
}
