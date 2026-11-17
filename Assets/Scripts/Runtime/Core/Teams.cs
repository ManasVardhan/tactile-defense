using System;
using UnityEngine;

namespace TactileWars.Core
{
    public enum TeamColorId { Blue = 0, Green = 1, Orange = 2, Red = 3, Pink = 4, Purple = 5, Teal = 6 }

    [Serializable]
    public struct TeamColors
    {
        public TeamColorId id;
        public Color primary;
        public Color dark;
        public Color paint;
    }

    public enum Team { Player = 0, Enemy = 1 }
}
