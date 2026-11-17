using TactileWars.Audio;
using TactileWars.Save;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TactileWars.Meta
{
    /// <summary>Applies the saved settings: music and sfx volumes and the quality toggle (low disables post
    /// processing and uses the Low quality level).</summary>
    public static class GameSettings
    {
        public static void Apply(SettingsData s, Camera cam = null)
        {
            if (s == null) return;
            AudioManager.SfxVolume = Mathf.Clamp01(s.sfxVolume);
            MusicPlayer.Volume = Mathf.Clamp01(s.musicVolume);
            // In the editor the active quality level is a project setting: only switch it in player builds.
            int lvl = s.quality <= 0 ? 1 : 3;
            if (!Application.isEditor && lvl < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != lvl) QualitySettings.SetQualityLevel(lvl, true);
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = s.quality > 0;
            }
        }
    }
}
