using TactileWars.Meta;
using TactileWars.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Music and sfx sliders (live, wired to AudioManager and MusicPlayer), quality toggle. Shared by the
    /// menu settings screen and the pause menu. Changes save when a slider is released or a toggle is pressed.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        Slider music, sfx;
        UIButton low, high;
        public Slider Music => music;
        public Slider Sfx => sfx;

        public static SettingsPanel Build(RectTransform parent, float width)
        {
            var sp = parent.gameObject.AddComponent<SettingsPanel>();
            sp.music = Row(parent, "Music", UIIcon.Music, "MUSIC", 0f, width);
            sp.sfx = Row(parent, "Sfx", UIIcon.Sound, "SOUND FX", -110f, width);
            var q = UIFactory.Rect(parent, "Quality").At(0.5f, 1f, 0f, -220f, width, 90f, 0.5f, 1f);
            var qi = UIFactory.Icon(q, UIIcon.Star, 62f);
            ((RectTransform)qi.transform).At(0f, 0.5f, 0f, 0f, 62f, 62f, 0f, 0.5f);
            var ql = UIFactory.Text(q, "Label", "QUALITY", 34f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)ql.transform).At(0f, 0.5f, 80f, 0f, 240f, 50f, 0f, 0.5f);
            sp.low = UIFactory.Button(q, "Low", "LOW", ButtonColor.White, 170f, 76f, null, 32f);
            ((RectTransform)sp.low.transform).At(1f, 0.5f, -186f, 0f, 170f, 76f, 1f, 0.5f);
            sp.high = UIFactory.Button(q, "High", "HIGH", ButtonColor.White, 170f, 76f, null, 32f);
            ((RectTransform)sp.high.transform).At(1f, 0.5f, 0f, 0f, 170f, 76f, 1f, 0.5f);
            sp.low.onClick = () => sp.SetQuality(0);
            sp.high.onClick = () => sp.SetQuality(1);
            sp.music.onValueChanged.AddListener(v => { MetaGame.Profile.settings.musicVolume = v; GameSettings.Apply(MetaGame.Profile.settings); });
            sp.sfx.onValueChanged.AddListener(v => { MetaGame.Profile.settings.sfxVolume = v; GameSettings.Apply(MetaGame.Profile.settings); });
            AddSaveOnRelease(sp.music);
            AddSaveOnRelease(sp.sfx);
            return sp;
        }

        static void AddSaveOnRelease(Slider s)
        {
            var et = s.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            e.callback.AddListener(_ => SaveSystem.Save());
            et.triggers.Add(e);
        }

        static Slider Row(RectTransform parent, string name, UIIcon icon, string label, float y, float width)
        {
            var r = UIFactory.Rect(parent, name).At(0.5f, 1f, 0f, y, width, 90f, 0.5f, 1f);
            var ic = UIFactory.Icon(r, icon, 62f);
            ((RectTransform)ic.transform).At(0f, 0.5f, 0f, 0f, 62f, 62f, 0f, 0.5f);
            var t = UIFactory.Text(r, "Label", label, 34f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)t.transform).At(0f, 0.5f, 80f, 0f, 240f, 50f, 0f, 0.5f);
            var s = UIFactory.Slider(r, "Slider", width - 360f, UIColors.Team(UIFactory.PlayerTeam));
            ((RectTransform)s.transform).At(1f, 0.5f, -8f, 0f, width - 360f, 64f, 1f, 0.5f);
            return s;
        }

        public void Refresh()
        {
            var st = MetaGame.Profile.settings;
            music.SetValueWithoutNotify(st.musicVolume);
            sfx.SetValueWithoutNotify(st.sfxVolume);
            ShowQuality(st.quality);
        }

        void SetQuality(int q)
        {
            MetaGame.Profile.settings.quality = q;
            GameSettings.Apply(MetaGame.Profile.settings);
            SaveSystem.Save();
            ShowQuality(q);
        }

        void ShowQuality(int q)
        {
            Style(low, q == 0);
            Style(high, q != 0);
        }

        static void Style(UIButton b, bool on)
        {
            b.SetColor(on ? ButtonColor.Team : ButtonColor.White);
            var l = b.Label();
            if (l == null) return;
            l.fontSharedMaterial = on ? UIFactory.Skin.headingOutline : UIFactory.Skin.headingFont.material;
            l.color = on ? Color.white : UIColors.Ink;
        }
    }
}
