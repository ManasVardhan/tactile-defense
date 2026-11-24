using TactileWars.Meta;
using TactileWars.Save;
using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    public class SettingsScreen : MenuScreen
    {
        public override string Title => "SETTINGS";
        protected override Vector2 CardSize => new Vector2(1060f, 760f);
        SettingsPanel panel;
        public UIButton ResetButton { get; private set; }

        protected override void Build()
        {
            var area = UIFactory.Rect(Body, "Panel").At(0.5f, 1f, 0f, -10f, 900f, 330f, 0.5f, 1f);
            panel = SettingsPanel.Build(area, 900f);
            var line = UIFactory.Img(Body, "Line", UIFactory.Skin.white, UIColors.CardLine);
            ((RectTransform)line.transform).At(0.5f, 1f, 0f, -350f, 900f, 4f, 0.5f, 1f);
            ResetButton = UIFactory.Button(Body, "Reset", "RESET SAVE", ButtonColor.Red, 380f, 96f, UIIcon.Replay, 34f);
            ((RectTransform)ResetButton.transform).At(0.5f, 1f, -230f, -384f, 380f, 96f, 0.5f, 1f);
            ResetButton.onClick = () => Menu.Dialog.Show("RESET SAVE?", "This deletes your cleared levels, unlocked units, gold and heroes. It cannot be undone.", "RESET", ButtonColor.Red, DoReset);
            var hint = UIFactory.Paragraph(Body, "ResetHint", "Start over from Level 1 with a fresh profile.", 24f, UIColors.Muted, TextAlignmentOptions.MidlineLeft);
            ((RectTransform)hint.transform).At(0.5f, 1f, 210f, -384f, 420f, 96f, 0.5f, 1f);
            var credits = UIFactory.Paragraph(Body, "Credits",
                "Fonts: Lilita One and Fredoka (SIL Open Font License 1.1). All art, audio and code are original to this recreation. " +
                "Offline build: no accounts, no servers, rankings are simulated.", 21f, UIColors.Muted, TextAlignmentOptions.Bottom);
            ((RectTransform)credits.transform).At(0.5f, 0f, 0f, 4f, 900f, 90f, 0.5f, 0f);
        }

        public override void Refresh() { panel.Refresh(); }

        void DoReset()
        {
            SaveSystem.ResetProfile();
            Menu.CloseScreen();
            Menu.RebuildForTeam();
            Menu.TeamSelect.Open(true);
        }
    }
}
