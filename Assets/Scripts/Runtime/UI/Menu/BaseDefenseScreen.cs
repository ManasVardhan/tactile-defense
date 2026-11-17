using TactileWars.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TactileWars.UI
{
    /// <summary>Entry to the base editor. Loads BaseEditor.unity when that scene is in the build settings
    /// (next phase); until then shows an "under construction" card with the defenses unlocked so far.</summary>
    public class BaseDefenseScreen : MenuScreen
    {
        public const string EditorScene = "BaseEditor";
        public override string Title => "BASE DEFENSE";
        protected override Vector2 CardSize => new Vector2(1360f, 760f);

        RectTransform defenseRow;

        public static bool EditorAvailable => Application.CanStreamedLevelBeLoaded(EditorScene);

        protected override void Build()
        {
            var skin = UIFactory.Skin;
            var left = UIFactory.Panel(Body, "Art", UIColors.CardInner, 28f);
            var lrt = ((RectTransform)left.transform).At(0f, 0.5f, 0f, 0f, 520f, 630f, 0f, 0.5f);
            var glow = UIFactory.Img(lrt, "Glow", skin.glow, UIColors.WithAlpha(UIColors.Gold, 0.45f));
            ((RectTransform)glow.transform).At(0.5f, 0.5f, 0f, 30f, 520f, 520f);
            var hq = UIFactory.Img(lrt, "HQ", skin.Render("hq_" + UIFactory.PlayerTeam) ?? skin.Render("hq_Red"), Color.white);
            ((RectTransform)hq.transform).At(0.5f, 0.5f, 0f, 40f, 440f, 440f);
            var tag = UIFactory.Tag(lrt, "Tag", "UNDER CONSTRUCTION", UIColors.Gold, 54f, 30f);
            tag.At(0.5f, 0f, 0f, 46f, tag.sizeDelta.x, 54f, 0.5f, 0f);
            tag.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var stripes = tag.GetComponentInChildren<TextMeshProUGUI>();
            stripes.color = UIColors.Ink;
            stripes.fontSharedMaterial = skin.headingFont.material;

            var h = UIFactory.Text(Body, "Head", "Your base is being built", 46f, TextStyle.HeadingDark, TextAlignmentOptions.TopLeft);
            ((RectTransform)h.transform).At(0f, 1f, 560f, -10f, 720f, 60f, 0f, 1f);
            var p = UIFactory.Paragraph(Body, "Text",
                "Soon you will place mines, turrets, tanks, cannons, bunkers and tesla antennas on your own lane, " +
                "within a budget that grows with your level. Unlock defenses now in Research, Defense branch.",
                29f, UIColors.Muted);
            ((RectTransform)p.transform).At(0f, 1f, 560f, -86f, 720f, 170f, 0f, 1f);
            var sub = UIFactory.Text(Body, "DefHead", "UNLOCKED DEFENSES", 30f, TextStyle.HeadingDark, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)sub.transform).At(0f, 1f, 560f, -280f, 600f, 40f, 0f, 1f);
            defenseRow = UIFactory.Rect(Body, "Defenses").At(0f, 1f, 560f, -330f, 720f, 140f, 0f, 1f);
            var go = UIFactory.Button(Body, "OpenResearch", "OPEN RESEARCH", ButtonColor.Blue, 420f, 100f, UIIcon.Flask, 36f);
            ((RectTransform)go.transform).At(0f, 0f, 560f, 10f, 420f, 100f, 0f, 0f);
            go.onClick = () => { Menu.Open(Menu.Research); Menu.Research.SetBranch(ResearchBranch.Defense); };
        }

        public override void OnOpened()
        {
            if (EditorAvailable)
            {
                ScreenTransition.Load(EditorScene, MetaGame.Profile.team);
            }
        }

        public override void Refresh()
        {
            UIFactory.ClearChildren(defenseRow);
            var tree = MetaGame.Db != null ? MetaGame.Db.research : null;
            if (tree == null) return;
            var skin = UIFactory.Skin;
            int i = 0;
            foreach (var n in tree.nodes)
            {
                if (n.branch != ResearchBranch.Defense || string.IsNullOrEmpty(n.defenseId)) continue;
                int lvl = MetaGame.ResearchLevel(n.id);
                var cell = UIFactory.Rect(defenseRow, n.defenseId).At(0f, 0.5f, i * 118f, 0f, 108f, 128f, 0f, 0.5f);
                var bg = UIFactory.Panel(cell, "Bg", lvl > 0 ? UIColors.CardInner : UIColors.CardLine, 18f);
                ((RectTransform)bg.transform).Stretch();
                var img = UIFactory.Img(cell, "Render", skin.Defense(n.defenseId, MetaGame.Profile.team), lvl > 0 ? Color.white : new Color(0.35f, 0.35f, 0.4f, 0.55f));
                ((RectTransform)img.transform).At(0.5f, 1f, 0f, 2f, 104f, 104f, 0.5f, 1f);
                if (lvl > 0)
                {
                    var t = UIFactory.Text(cell, "Lv", "Lv " + lvl, 22f, TextStyle.HeadingDark);
                    ((RectTransform)t.transform).At(0.5f, 0f, 0f, 4f, 100f, 26f, 0.5f, 0f);
                }
                else
                {
                    var l = UIFactory.Icon(cell, UIIcon.Lock, 40f);
                    ((RectTransform)l.transform).At(0.5f, 0f, 0f, 6f, 40f, 40f, 0.5f, 0f);
                }
                i++;
            }
        }
    }
}
