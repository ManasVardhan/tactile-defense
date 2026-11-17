using System;
using UnityEngine;

namespace TactileWars.Core
{
    [Serializable]
    public class TeamMaterials
    {
        public TeamColorId id;
        public Material teamColor;      // replaces FBX slot TeamColor
        public Material teamColorDark;  // replaces FBX slot TeamColorDark
        public Material paint;          // replaces FBX slot Paint
        public Material unitTeamColor;  // outlined variants used on soldiers
        public Material unitTeamColorDark;
        public Material unitPaint;
        public Material blot;           // spiral blot at the ring centre
        public Material ring;           // squad ring decal
        public Material splat;          // ground paint splats
        public Material projectile;     // paint ball dot
        public Material stroke;         // dashed formation stroke
        public Material strokeGlow;     // soft glow under the stroke
        public Material arc;            // ring arc indicators (capture, health, bubble)
    }

    /// <summary>Shared per team materials so renderers stay SRP batcher friendly. Built by AssetBuilder.</summary>
    [CreateAssetMenu(menuName = "TactileWars/Team Visuals")]
    public class TeamVisuals : ScriptableObject
    {
        public TeamMaterials[] teams = Array.Empty<TeamMaterials>();
        public Material flash;          // white hit flash material
        public Material blobShadow;
        public Material neutralArc;

        public TeamMaterials Get(TeamColorId id)
        {
            for (int i = 0; i < teams.Length; i++)
                if (teams[i].id == id) return teams[i];
            return teams.Length > 0 ? teams[0] : null;
        }
    }
}
