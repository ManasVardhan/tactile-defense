using System.Collections.Generic;
using TactileWars.Army;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Owns one UnitBadge per deployed unit: adds badges when units deploy, removes them when units are
    /// destroyed, and ticks them. Sits under the rest of the HUD.</summary>
    public class UnitBadgeLayer : MonoBehaviour
    {
        PlayerArmy army;
        Camera cam;
        RectTransform space;
        Camera canvasCam;
        readonly List<UnitBadge> badges = new List<UnitBadge>(8);

        public IReadOnlyList<UnitBadge> Badges => badges;

        public static UnitBadgeLayer Create(RectTransform parent, PlayerArmy army, Camera cam, Camera canvasCam)
        {
            var rt = UIFactory.Rect(parent, "UnitBadges").Stretch();
            var l = rt.gameObject.AddComponent<UnitBadgeLayer>();
            l.army = army;
            l.cam = cam;
            l.space = rt;
            l.canvasCam = canvasCam;
            if (army != null) army.SquadChanged += l.Sync;
            ArmyInput.BadgeHitTest = l.UnitAtScreen;
            return l;
        }

        void OnDestroy()
        {
            if (army != null) army.SquadChanged -= Sync;
            if (ArmyInput.BadgeHitTest == (System.Func<Vector2, PlayerUnit>)UnitAtScreen) ArmyInput.BadgeHitTest = null;
        }

        /// <summary>The unit whose visible badge covers the screen point, topmost first; null when none.</summary>
        public PlayerUnit UnitAtScreen(Vector2 screen)
        {
            for (int i = badges.Count - 1; i >= 0; i--)
            {
                var b = badges[i];
                if (b.Unit != null && !b.Unit.IsDestroyed && b.Contains(screen, canvasCam)) return b.Unit;
            }
            return null;
        }

        /// <summary>The badge of a deployed, living unit; null otherwise.</summary>
        public UnitBadge For(PlayerUnit unit)
        {
            for (int i = 0; i < badges.Count; i++) if (badges[i].Unit == unit) return badges[i];
            return null;
        }

        void Sync()
        {
            if (army == null) return;
            for (int i = badges.Count - 1; i >= 0; i--)
            {
                var u = badges[i].Unit;
                if (u != null && !u.IsDestroyed) continue;
                Destroy(badges[i].gameObject);
                badges.RemoveAt(i);
            }
            var units = army.Units;
            for (int i = 0; i < units.Count; i++)
                if (For(units[i]) == null) badges.Add(UnitBadge.Create(space, units[i]));
        }

        void LateUpdate()
        {
            for (int i = badges.Count - 1; i >= 0; i--)
            {
                var b = badges[i];
                if (b.Unit == null || b.Unit.IsDestroyed) { Sync(); return; }
                b.Tick(cam, space, canvasCam);
            }
        }
    }
}
