using System;
using TactileWars.Core;
using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Every font, material and sprite the UI uses. Built by the editor UIArtBuilder into Assets/Data/UISkin.asset.</summary>
    [CreateAssetMenu(menuName = "TactileWars/UI Skin")]
    public class UISkin : ScriptableObject
    {
        [Header("Fonts (SIL OFL, see Assets/Art/Fonts/LICENSES.md)")]
        public TMP_FontAsset headingFont;
        public TMP_FontAsset bodyFont;
        [Tooltip("Heading font with a dark outline and drop shadow (white titles and button labels).")] public Material headingOutline;
        [Tooltip("Heading font with a thinner outline (small labels on colored plates).")] public Material headingOutlineThin;
        [Tooltip("Body font with a dark outline (white text over the 3D scene).")] public Material bodyOutline;

        [Header("Shapes (9 sliced where it makes sense)")]
        public Sprite rounded16;
        public Sprite rounded32;
        public Sprite pill;
        public Sprite pillShine;
        public Sprite circle;
        public Sprite circleShine;
        public Sprite ring;
        public Sprite ringThin;
        public Sprite hex;
        public Sprite hexRing;
        public Sprite tab;
        public Sprite softShadow;
        public Sprite glow;
        public Sprite splat;
        public Sprite ribbon;
        public Sprite gradientV;
        public Sprite dash;
        public Sprite medal;
        public Sprite white;

        [Header("Icons (index = UIIcon)")]
        public Sprite[] icons = Array.Empty<Sprite>();

        [Header("Renders of the real models (index = TeamColorId)")]
        public Sprite[] busts = Array.Empty<Sprite>();
        public Sprite[] soldiers = Array.Empty<Sprite>();
        public Sprite[] heroes = Array.Empty<Sprite>();
        public NamedSprite[] renders = Array.Empty<NamedSprite>();

        public TeamPalette palette;

        public Sprite Icon(UIIcon i) { int k = (int)i; return k < icons.Length ? icons[k] : null; }
        public Sprite Bust(TeamColorId t) => Pick(busts, t);
        public Sprite Soldier(TeamColorId t) => Pick(soldiers, t);
        public Sprite Hero(TeamColorId t) => Pick(heroes, t);

        static Sprite Pick(Sprite[] a, TeamColorId t)
        {
            int k = (int)t;
            if (k < a.Length && a[k] != null) return a[k];
            return a.Length > 0 ? a[0] : null;
        }

        public Sprite Render(string key)
        {
            for (int i = 0; i < renders.Length; i++) if (renders[i].key == key) return renders[i].sprite;
            return null;
        }

        /// <summary>Structure render for a defense id in a team color (key "def_turret_Red").</summary>
        public Sprite Defense(string defenseId, TeamColorId t) => Render("def_" + defenseId + "_" + t) ?? Render("def_" + defenseId + "_Red");
    }

    [Serializable]
    public struct NamedSprite
    {
        public string key;
        public Sprite sprite;
    }
}
