using TactileWars.Army;
using TactileWars.Meta;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>
    /// The two one-time menu callouts (the battle tutorial's Callout card, arrow and ring). On the home screen after
    /// a level clear unlocked a unit: "You unlocked a new unit. Open Army." pointing at the Army button. Inside Army,
    /// on the Units tab, while the Healer is unlocked but not in the squad: "Add the Healer to your squad." pointing
    /// at the Healer's card. When each shows is decided by MetaGame (ShowArmyGuide, ShowAddUnitGuide); both are
    /// remembered in the profile's seen-list (Progression.GuideArmy, Progression.GuideAddUnit).
    /// </summary>
    public class MenuGuide : MonoBehaviour
    {
        public const string ArmyText = "You unlocked a new unit. Open Army.";
        public const string AddUnitText = "Add the Healer to your squad.";

        MenuRoot menu;
        RectTransform area;
        Callout callout;
        bool armyWanted, addWanted;

        public Callout Callout => callout;
        /// <summary>The seen-list id of the callout on screen (null when none).</summary>
        public string Showing { get; private set; }

        public static MenuGuide Create(MenuRoot menu, RectTransform area)
        {
            var g = area.gameObject.AddComponent<MenuGuide>();
            g.menu = menu;
            g.area = area;
            g.callout = Callout.Create(area, "Callout", 520f, UIColors.Gold);
            return g;
        }

        /// <summary>Re-reads the rules (called when the profile changes and when screens open or close).</summary>
        public void Refresh()
        {
            armyWanted = MetaGame.ShowArmyGuide;
            addWanted = MetaGame.ShowAddUnitGuide;
            Place();
        }

        void LateUpdate() { Place(); }

        void Place()
        {
            if (menu == null || callout == null) return;
            bool blocked = (menu.TeamSelect != null && menu.TeamSelect.IsOpen) || (menu.RaidCard != null && menu.RaidCard.IsOpen)
                           || (menu.Dialog != null && menu.Dialog.IsOpen) || ScreenTransition.Busy;
            string want = null;
            Component target = null;
            if (!blocked && menu.Current == null && armyWanted && menu.ArmyButton != null)
            {
                // At the Army button's dot, from above: clear of the button's label and of the squad strip.
                want = Progression.GuideArmy;
                target = menu.ArmyDot != null ? (Component)menu.ArmyDot : menu.ArmyButton;
            }
            else if (!blocked && menu.Current == menu.Army && menu.Army != null && addWanted && menu.Army.ActiveTab == ArmyScreen.Tab.Units)
            {
                var card = menu.Army.Builder != null ? menu.Army.Builder.Card(UnitTypeId.Healer) : null;
                if (card != null && card.Band != null) { want = Progression.GuideAddUnit; target = card.Band; }
            }
            Showing = want;
            if (want == null) { callout.Hide(); return; }
            if (want == Progression.GuideArmy) callout.Show("NEW UNIT", ArmyText);
            else callout.Show("TIP", AddUnitText);
            var rt = (RectTransform)target.transform;
            Vector2 local = area.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            if (want == Progression.GuideArmy) callout.PointAt(local, rt.rect.height * 0.5f + 4f, Callout.Side.Above, false);
            // From the right, over the locked cards, so the squad row and the Healer's ADD stay clear.
            else callout.PointAt(local, rt.rect.width * 0.5f + 4f, Callout.Side.Right, false);
        }
    }
}
