using TactileWars.Battle;
using TactileWars.Meta;
using TMPro;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>Pause: freezes the battle (timeScale 0, gameplay input off, sfx paused, music keeps playing) and
    /// offers resume, restart, settings and quit to base.</summary>
    public class PauseMenu : MonoBehaviour
    {
        BattleHUD hud;
        UIPanelAnim anim;
        RectTransform buttons, settingsArea;
        SettingsPanel settings;
        public bool IsOpen { get; private set; }
        public UIButton ResumeButton { get; private set; }
        public UIButton RestartButton { get; private set; }
        public UIButton QuitButton { get; private set; }
        float savedScale = 1f;

        public static PauseMenu Create(BattleHUD hud, Transform parent)
        {
            var rt = UIFactory.Rect(parent, "PauseMenu").Stretch();
            var m = rt.gameObject.AddComponent<PauseMenu>();
            m.hud = hud;
            m.BuildUI(rt);
            rt.gameObject.SetActive(false);
            return m;
        }

        void BuildUI(RectTransform rt)
        {
            UIFactory.Dim(rt, 0.55f);
            var cardRoot = UIFactory.Rect(rt, "CardRoot").At(0.5f, 0.5f, 0f, -24f, 680f, 640f);
            var face = UIFactory.Card(cardRoot, "Card", 680f, 640f);
            ((RectTransform)face.parent).Stretch();
            var ribbon = UIFactory.Ribbon(cardRoot, "PAUSED", UIColors.Slate, 520f, 116f, 62f);
            ribbon.At(0.5f, 1f, 0f, 46f, 520f, 116f, 0.5f, 1f);

            buttons = UIFactory.Rect(face, "Buttons").Stretch(0f, 90f, 0f, 30f);
            ResumeButton = UIFactory.Button(buttons, "Resume", "RESUME", ButtonColor.Green, 480f, 116f, UIIcon.Play, 52f);
            ((RectTransform)ResumeButton.transform).At(0.5f, 1f, 0f, 0f, 480f, 116f, 0.5f, 1f);
            ResumeButton.onClick = Resume;
            RestartButton = UIFactory.Button(buttons, "Restart", "RESTART", ButtonColor.Gold, 480f, 96f, UIIcon.Replay, 40f);
            ((RectTransform)RestartButton.transform).At(0.5f, 1f, 0f, -138f, 480f, 96f, 0.5f, 1f);
            RestartButton.onClick = Restart;
            var set = UIFactory.Button(buttons, "Settings", "SETTINGS", ButtonColor.Slate, 480f, 96f, UIIcon.Gear, 40f);
            ((RectTransform)set.transform).At(0.5f, 1f, 0f, -254f, 480f, 96f, 0.5f, 1f);
            set.onClick = ShowSettings;
            QuitButton = UIFactory.Button(buttons, "Quit", "QUIT TO BASE", ButtonColor.Red, 480f, 96f, UIIcon.Home, 40f);
            ((RectTransform)QuitButton.transform).At(0.5f, 1f, 0f, -370f, 480f, 96f, 0.5f, 1f);
            QuitButton.onClick = Quit;

            settingsArea = UIFactory.Rect(face, "Settings").Stretch(40f, 110f, 40f, 30f);
            var panelRoot = UIFactory.Rect(settingsArea, "Panel").At(0.5f, 1f, 0f, 0f, 580f, 330f, 0.5f, 1f);
            settings = SettingsPanel.Build(panelRoot, 580f);
            var back = UIFactory.Button(settingsArea, "Back", "BACK", ButtonColor.Slate, 300f, 96f, UIIcon.ArrowLeft, 40f);
            ((RectTransform)back.transform).At(0.5f, 0f, 0f, 0f, 300f, 96f, 0.5f, 0f);
            back.sound = Audio.SoundId.UiBack;
            back.onClick = ShowButtons;
            settingsArea.gameObject.SetActive(false);

            rt.gameObject.AddComponent<CanvasGroup>();
            anim = rt.gameObject.AddComponent<UIPanelAnim>();
            anim.scaleTarget = cardRoot;
        }

        public void Open()
        {
            var bm = BattleManager.Instance;
            if (IsOpen || (bm != null && bm.Over)) return;
            IsOpen = true;
            savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (bm != null && bm.recorder != null) bm.recorder.InputEnabled = false;
            ShowButtons();
            transform.SetAsLastSibling();
            anim.Show();
        }

        public void Resume()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Time.timeScale = savedScale;
            AudioListener.pause = false;
            var bm = BattleManager.Instance;
            if (bm != null && bm.recorder != null && !bm.Over) bm.recorder.InputEnabled = true;
            anim.Hide();
        }

        public void ForceClose()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            anim.Hide(true);
        }

        public void Restart()
        {
            // Same loadout and opponent (the mercenary fee is not charged again).
            ScreenTransition.Load(BattleLaunch.BattleScene, UIFactory.PlayerTeam);
        }

        public void Quit()
        {
            BattleLaunch.Clear();
            ScreenTransition.Load(BattleLaunch.MenuScene, UIFactory.PlayerTeam);
        }

        void ShowSettings()
        {
            buttons.gameObject.SetActive(false);
            settingsArea.gameObject.SetActive(true);
            settings.Refresh();
        }

        void ShowButtons()
        {
            buttons.gameObject.SetActive(true);
            settingsArea.gameObject.SetActive(false);
        }
    }
}
