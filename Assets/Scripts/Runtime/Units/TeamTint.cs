using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Swaps the shared TeamColor, TeamColorDark and Paint material slots to the team's shared
    /// materials (keeps the SRP batcher working; no per renderer material instances).</summary>
    public class TeamTint : MonoBehaviour
    {
        public Material baseTeamColor;
        public Material baseTeamColorDark;
        public Material basePaint;
        [Tooltip("Use the outlined unit variants of the team materials.")] public bool unitVariant;
        public Renderer[] renderers;
        [Tooltip("Tint automatically on Start (props and capture points that belong to a side).")] public bool applyOnStart;
        public Team startTeam = Team.Enemy;
        Material[][] original;

        void Start()
        {
            if (!applyOnStart) return;
            var gc = TactileWars.Battle.BattleContext.Config;
            var vis = TactileWars.Battle.BattleContext.Visuals;
            if (gc == null || vis == null) return;
            Apply(vis.Get(startTeam == Team.Player ? gc.playerTeam : gc.enemyTeam));
        }

        void CacheOriginal()
        {
            if (original != null) return;
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            original = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) original[i] = renderers[i] != null ? renderers[i].sharedMaterials : new Material[0];
        }

        public void Apply(TeamMaterials team)
        {
            if (team == null) return;
            CacheOriginal();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var src = original[i];
                var mats = new Material[src.Length];
                for (int k = 0; k < src.Length; k++)
                {
                    var m = src[k];
                    if (m != null && m == baseTeamColor) m = unitVariant ? team.unitTeamColor : team.teamColor;
                    else if (m != null && m == baseTeamColorDark) m = unitVariant ? team.unitTeamColorDark : team.teamColorDark;
                    else if (m != null && m == basePaint) m = unitVariant ? team.unitPaint : team.paint;
                    mats[k] = m;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
