using System;
using UnityEngine;

namespace TactileWars.Core
{
    /// <summary>Per team colors. Values sampled from reference frames (VISUAL_ANALYSIS.md) where available, others are estimates.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Team Palette")]
    public class TeamPalette : ScriptableObject
    {
        public TeamColors[] teams = Array.Empty<TeamColors>();

        public TeamColors Get(TeamColorId id)
        {
            for (int i = 0; i < teams.Length; i++)
                if (teams[i].id == id) return teams[i];
            return new TeamColors { id = id, primary = Color.white, dark = Color.gray, paint = Color.white };
        }
    }
}
