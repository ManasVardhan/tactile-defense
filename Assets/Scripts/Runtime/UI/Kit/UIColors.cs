using TactileWars.Core;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>UI palette. Neutral slate and cream like the reference cards; team colors are the only saturated
    /// accents besides gold, green (confirm) and red (danger).</summary>
    public static class UIColors
    {
        public static readonly Color Ink = Hex(0x2A2D3A);
        public static readonly Color Slate = Hex(0x3A4052);
        public static readonly Color SlateLight = Hex(0x535B70);
        public static readonly Color SlateDark = Hex(0x262A36);
        public static readonly Color Card = Hex(0xFBF8F1);
        public static readonly Color CardInner = Hex(0xEDE7DA);
        public static readonly Color CardRim = Hex(0xCFC6B3);
        public static readonly Color CardLine = Hex(0xDCD4C4);
        public static readonly Color Muted = Hex(0x7D8190);
        public static readonly Color Gold = Hex(0xF6B12B);
        public static readonly Color GoldDark = Hex(0xC27A14);
        public static readonly Color Green = Hex(0x76BD36);
        public static readonly Color GreenDark = Hex(0x4C8A1F);
        public static readonly Color Red = Hex(0xE04A2F);
        public static readonly Color RedDark = Hex(0xA02D1C);
        public static readonly Color Blue = Hex(0x3E8EDB);
        public static readonly Color BlueDark = Hex(0x255E9F);
        public static readonly Color Grey = Hex(0xB9BDC6);
        public static readonly Color GreyDark = Hex(0x868B97);
        public static readonly Color Xp = Hex(0x3FB4E5);
        public static readonly Color Prism = Hex(0xC35AD8);
        public static readonly Color White = Color.white;
        public static readonly Color Dim = new Color(0.07f, 0.08f, 0.11f, 0.62f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.32f);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Color Darker(Color c, float k = 0.72f) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        public static Color Lighter(Color c, float k = 0.25f) => Color.Lerp(c, Color.white, k);
        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static TeamPalette palette;
        public static TeamPalette Palette { get => palette; set => palette = value; }

        public static Color Team(TeamColorId id)
        {
            if (palette != null) return palette.Get(id).primary;
            return id switch
            {
                TeamColorId.Red => Hex(0xDA4029), TeamColorId.Teal => Hex(0x229A8A), TeamColorId.Blue => Hex(0x3B7FD8),
                TeamColorId.Green => Hex(0x8DB52B), TeamColorId.Orange => Hex(0xF0A020), TeamColorId.Pink => Hex(0xDB4793),
                _ => Hex(0x9B6FB0),
            };
        }

        public static Color TeamDark(TeamColorId id) => palette != null ? palette.Get(id).dark : Darker(Team(id));

        public static string TeamName(TeamColorId id) => id switch
        {
            TeamColorId.Red => "Red", TeamColorId.Blue => "Blue", TeamColorId.Green => "Green", TeamColorId.Orange => "Orange",
            TeamColorId.Pink => "Pink", TeamColorId.Purple => "Purple", _ => "Teal",
        };

        /// <summary>The six player factions (verified: Blue, Green, Orange, Red, Pink, Purple).</summary>
        public static readonly TeamColorId[] Factions = { TeamColorId.Red, TeamColorId.Blue, TeamColorId.Green, TeamColorId.Orange, TeamColorId.Pink, TeamColorId.Purple };
    }
}
