using TactileWars.Core;
using UnityEngine;

namespace TactileWars.Units
{
    /// <summary>Swaps the shared TeamColor, TeamColorDark and Paint material slots to the team's shared
    /// materials (keeps the SRP batcher working; no per renderer material instances). A soldier's Helmet slot shows the
    /// team color too, unless SetAccent put a shared per unit type material there (helmet bowl, rim lip and eye
    /// glints), so unit types read apart by color while the body keeps the team color.</summary>
    public class TeamTint : MonoBehaviour
    {
        public Material baseTeamColor;
        public Material baseTeamColorDark;
        public Material basePaint;
        [Tooltip("The Accent slot material (soldiers: helmet rim and eye glints); SetAccent swaps it.")] public Material baseAccent;
        [Tooltip("The Helmet slot material (soldiers: the helmet bowl): team colored, or the SetAccent material.")] public Material baseHelmet;
        [Tooltip("Use the outlined unit variants of the team materials.")] public bool unitVariant;
        public Renderer[] renderers;
        [Tooltip("Tint automatically on Start (props and capture points that belong to a side).")] public bool applyOnStart;
        public Team startTeam = Team.Enemy;
        Material[][] original;
        Material accent;
        TeamMaterials last;

        /// <summary>The material now in the Accent slot (baseAccent unless SetAccent replaced it).</summary>
        public Material CurrentAccent => accent != null ? accent : baseAccent;

        /// <summary>Puts a shared material in the Accent slot (null restores the original) and re-applies the team.</summary>
        public void SetAccent(Material m)
        {
            accent = m;
            if (last != null) Apply(last);
            else Apply(null, true);
        }

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

        public void Apply(TeamMaterials team) => Apply(team, false);

        void Apply(TeamMaterials team, bool accentOnly)
        {
            if (team == null && !accentOnly) return;
            if (team != null) last = team;
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
                    if (m == null) { }
                    else if ((m == baseAccent || m == baseHelmet) && accent != null) m = accent;
                    else if (team == null) { } // accent only, no team known yet
                    else if (m == baseTeamColor || (m == baseHelmet && baseHelmet != null)) m = unitVariant ? team.unitTeamColor : team.teamColor;
                    else if (m == baseTeamColorDark) m = unitVariant ? team.unitTeamColorDark : team.teamColorDark;
                    else if (m == basePaint) m = unitVariant ? team.unitPaint : team.paint;
                    mats[k] = m;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
