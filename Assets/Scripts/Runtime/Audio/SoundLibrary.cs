using System;
using UnityEngine;

namespace TactileWars.Audio
{
    public enum SoundId
    {
        Shoot, ShootHeavy, TurretShot, CannonShot, TankShot, Hit, Splat, UnitPop, Explosion, MineExplosion,
        BuildingHit, BuildingDestroyed, StrokeDraw, FormationSet, CaptureTick, CaptureComplete,
        ReinforcementReady, ReinforcementCall, BubblePop, Victory, Defeat, Footsteps, UiClick,
        // Appended for the UI phase (keep order: values are serialized as ints).
        UiBack, UiOpen, UiPurchase, UiError, CoinPickup, LevelUp,
        // Appended for phase 5 defenses.
        TeslaCharge, TeslaZap, SniperAim, SniperShot, GeyserRumble, GeyserErupt,
        // Appended for unit controls: a refused deploy or move order.
        Reject
    }

    /// <summary>Mix groups. Every SFX file is normalized to the same peak, so loudness is set here (phase 8).</summary>
    public enum SoundCategory { Shots = 0, Impacts = 1, Explosions = 2, Jingles = 3, UI = 4, Feedback = 5, Ambience = 6 }

    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public SoundCategory category = SoundCategory.Feedback;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float volume = 0.6f;
        public Vector2 pitch = new Vector2(0.95f, 1.05f);
        [Tooltip("Voice limit: max simultaneous plays of this sound.")] public int maxVoices = 3;
        [Tooltip("Minimum seconds between two plays of this sound.")] public float minInterval = 0.03f;
        [Range(0, 1)] public float spatialBlend = 0f;
    }

    /// <summary>Sound table populated by AssetBuilder from Assets/Audio. Missing clips are tolerated.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        public SoundEntry[] entries = Array.Empty<SoundEntry>();
        public AudioClip battleMusic;
        public AudioClip menuMusic;
        [Range(0, 1)] public float musicVolume = 0.28f;
        [Range(0, 1)] public float masterVolume = 0.9f;

        [Header("Category mix (ESTIMATE: chosen by reasoning, not by listening; tune on device)")]
        [Range(0, 1)] public float shotsVolume = 0.7f;
        [Range(0, 1)] public float impactsVolume = 0.75f;
        [Range(0, 1)] public float explosionsVolume = 1f;
        [Range(0, 1)] public float jinglesVolume = 1f;
        [Range(0, 1)] public float uiVolume = 0.8f;
        [Range(0, 1)] public float feedbackVolume = 0.9f;
        [Range(0, 1)] public float ambienceVolume = 0.7f;

        public float CategoryVolume(SoundCategory c)
        {
            switch (c)
            {
                case SoundCategory.Shots: return shotsVolume;
                case SoundCategory.Impacts: return impactsVolume;
                case SoundCategory.Explosions: return explosionsVolume;
                case SoundCategory.Jingles: return jinglesVolume;
                case SoundCategory.UI: return uiVolume;
                case SoundCategory.Ambience: return ambienceVolume;
                default: return feedbackVolume;
            }
        }

        public SoundEntry Find(SoundId id)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].id == id) return entries[i];
            return null;
        }
    }
}
