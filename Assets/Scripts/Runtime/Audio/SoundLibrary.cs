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
        TeslaCharge, TeslaZap, SniperAim, SniperShot, GeyserRumble, GeyserErupt
    }

    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
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
        [Range(0, 1)] public float musicVolume = 0.35f;
        [Range(0, 1)] public float masterVolume = 0.9f;

        public SoundEntry Find(SoundId id)
        {
            for (int i = 0; i < entries.Length; i++) if (entries[i].id == id) return entries[i];
            return null;
        }
    }
}
