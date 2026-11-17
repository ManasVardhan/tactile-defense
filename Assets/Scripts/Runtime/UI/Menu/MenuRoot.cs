using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>Main menu / home base. Builds the whole menu UI from the skin at runtime (top bar, navigation,
    /// Attack button, screens, first run team selection) over the live 3D backdrop.</summary>
    public class MenuRoot : MonoBehaviour
    {
        public UISkin skin;
        public Camera cam;
        public MenuBackdrop backdrop;
        public MetaDatabase database;

        public static MenuRoot Instance { get; private set; }
        public UIRoot Root { get; private set; }
        public TopBar Bar { get; private set; }
        public UIToast Toast { get; private set; }
        public TeamSelectScreen TeamSelect { get; private set; }
        public AttackScreen Attack { get; private set; }
        public ArmyScreen Army { get; private set; }
        public ResearchScreen Research { get; private set; }
        public BaseDefenseScreen BaseDefense { get; private set; }
        public ProfileScreen Profile { get; private set; }
        public SettingsScreen Settings { get; private set; }
        public ConfirmDialog Dialog { get; private set; }
        public RaidReportCard RaidCard { get; private set; }
        /// <summary>The simulated raid shown on this menu visit (null when none).</summary>
        public Save.RaidRecord LastRaid { get; private set; }
        public MenuScreen Current { get; private set; }
        public UIButton AttackButton { get; private set; }
        public readonly List<UIButton> NavButtons = new List<UIButton>();

        RectTransform home, screens, overlay;
        CanvasGroup homeGroup;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; MetaGame.Changed -= OnChanged; }

        void Start()
        {
            if (database != null) MetaGame.Db = database;
            var p = MetaGame.Profile;
            GameSettings.Apply(p.settings, cam);
            if (backdrop != null) backdrop.ApplyTeam(p.team);
            MetaGame.Changed += OnChanged;
            Build();
            if (!p.teamChosen) TeamSelect.Open(true, true);
            else
            {
                // Simulated offline raids on the player's base (labeled as simulated on the card).
                LastRaid = Online.LocalOfflineProvider.Instance.CheckForRaid(System.DateTime.UtcNow);
                if (LastRaid != null) RaidCard.Show(LastRaid);
            }
        }

        void OnApplicationPause(bool paused) { if (paused) MetaGame.MarkSeen(System.DateTime.UtcNow); }
        void OnApplicationQuit() { MetaGame.MarkSeen(System.DateTime.UtcNow); }

        void Build()
        {
            var p = MetaGame.Profile;
            UIFactory.PlayerTeam = p.team;
            Root = UIRoot.Create("MenuCanvas", cam, skin, 10);
            var safe = Root.Safe;

            home = UIFactory.Rect(safe, "Home").Stretch();
            homeGroup = home.gameObject.AddComponent<CanvasGroup>();
            BuildHome(home);
            Bar = TopBar.Build(this, UIFactory.Rect(safe, "Top").Stretch());
            Bar.Settings.onClick = () => Open(Settings);

            screens = UIFactory.Rect(safe, "Screens").Stretch();
            Attack = MakeScreen<AttackScreen>("AttackScreen");
            Army = MakeScreen<ArmyScreen>("ArmyScreen");
            Research = MakeScreen<ResearchScreen>("ResearchScreen");
            BaseDefense = MakeScreen<BaseDefenseScreen>("BaseDefenseScreen");
            Profile = MakeScreen<ProfileScreen>("ProfileScreen");
            Settings = MakeScreen<SettingsScreen>("SettingsScreen");
            // The top bar stays above screens so currencies remain visible while shopping.
            Bar.transform.parent.SetAsLastSibling();

            overlay = UIFactory.Rect(Root.CanvasRect, "Overlay").Stretch();
            TeamSelect = TeamSelectScreen.Create(this, overlay);
            Dialog = ConfirmDialog.Create(overlay);
            RaidCard = RaidReportCard.Create(this, overlay);
            Toast = UIToast.Create(overlay, 300f);
            Bar.Refresh(false);
        }

        T MakeScreen<T>(string name) where T : MenuScreen
        {
            var rt = UIFactory.Rect(screens, name);
            var s = rt.gameObject.AddComponent<T>();
            s.Init(this);
            return s;
        }

        void BuildHome(RectTransform rt)
        {
            var skin = UIFactory.Skin;
            var team = UIFactory.PlayerTeam;

            // Wordmark.
            var logo = UIFactory.Rect(rt, "Logo").At(0.5f, 1f, 0f, -26f, 640f, 150f, 0.5f, 1f);
            var l1 = UIFactory.Text(logo, "Line1", "PAINT SQUAD", 74f, TextStyle.Heading);
            ((RectTransform)l1.transform).At(0.5f, 1f, 0f, 0f, 640f, 84f, 0.5f, 1f);
            var wars = UIFactory.Ribbon(logo, "WARS", UIColors.Team(team), 250f, 70f, 46f);
            wars.At(0.5f, 1f, 0f, -78f, 250f, 70f, 0.5f, 1f);
            logo.localRotation = Quaternion.Euler(0f, 0f, 2f);

            // Navigation (bottom left): round icon buttons with labels, like the reference's bottom tab icons.
            var nav = UIFactory.Rect(rt, "Nav").At(0f, 0f, 36f, 28f, 760f, 196f, 0f, 0f);
            var navBg = UIFactory.Img(nav, "Bg", skin.pill, UIColors.WithAlpha(UIColors.Ink, 0.55f), UIFactory.PillMult(120f));
            ((RectTransform)navBg.transform).At(0f, 0f, 0f, 34f, 740f, 120f, 0f, 0f);
            var items = new (string label, UIIcon icon, System.Func<MenuScreen> screen)[]
            {
                ("ARMY", UIIcon.Users, () => Army),
                ("RESEARCH", UIIcon.Flask, () => Research),
                ("BASE", UIIcon.Shield, () => BaseDefense),
                ("PROFILE", UIIcon.Trophy, () => Profile),
            };
            NavButtons.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var b = UIFactory.IconButton(nav, "Nav_" + it.label, it.icon, 136f, ButtonColor.White, 0.6f);
                ((RectTransform)b.transform).At(0f, 0f, 30f + i * 178f, 46f, 136f, 136f, 0f, 0f);
                var lbl = UIFactory.Text(nav, "Label_" + it.label, it.label, 30f, TextStyle.Heading);
                ((RectTransform)lbl.transform).At(0f, 0f, 30f + i * 178f + 68f, 0f, 200f, 44f, 0.5f, 0f);
                var screenGetter = it.screen;
                b.sound = SoundId.UiOpen;
                b.onClick = () => Open(screenGetter());
                NavButtons.Add(b);
            }

            // Attack (bottom right): big team colored pill with the sword, a bust peeking over it.
            var atk = UIFactory.Rect(rt, "AttackGroup").At(1f, 0f, -40f, 34f, 520f, 230f, 1f, 0f);
            AttackButton = UIFactory.Button(atk, "AttackButton", "ATTACK!", ButtonColor.Team, 470f, 158f, UIIcon.Sword, 70f);
            ((RectTransform)AttackButton.transform).At(1f, 0f, 0f, 0f, 470f, 158f, 1f, 0f);
            AttackButton.sound = SoundId.UiOpen;
            AttackButton.onClick = OpenAttack;
            var peek = UIFactory.Img(atk, "Bust", skin.Bust(team), Color.white);
            ((RectTransform)peek.transform).At(0f, 1f, -6f, 20f, 132f, 132f, 0f, 1f);
            peek.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            var hint = UIFactory.Tag(rt, "Offline", "OFFLINE MODE: AI OPPONENTS", UIColors.WithAlpha(UIColors.Ink, 0.7f), 34f, 20f);
            hint.At(1f, 1f, -30f, -118f, hint.sizeDelta.x, 34f, 1f, 1f);
        }

        // ------------------------------------------------------------------ navigation

        public void OpenAttack() => Open(Attack);

        public void Open(MenuScreen s)
        {
            if (s == null) return;
            if (Current != null && Current != s) Current.Anim.Hide();
            Current = s;
            s.Anim.Show();   // activate first: TMP measures text correctly only on active objects
            s.Refresh();
            s.OnOpened();
            SetHomeVisible(false);
        }

        public void CloseScreen()
        {
            if (Current != null) Current.Anim.Hide();
            Current = null;
            SetHomeVisible(true);
        }

        void SetHomeVisible(bool on)
        {
            homeGroup.interactable = on;
            homeGroup.blocksRaycasts = on;
            UIAnim.Fade(homeGroup, on ? 1f : 0f, 0.2f);
            if (Bar != null && Bar.LeagueGroup != null) UIAnim.Fade(Bar.LeagueGroup, on ? 1f : 0f, 0.2f);
        }

        void OnChanged()
        {
            if (Bar != null) Bar.Refresh(true);
            if (Current != null) Current.Refresh();
        }

        /// <summary>Team changes recolor a lot of the UI: rebuild it.</summary>
        public void RebuildForTeam()
        {
            var open = Current != null ? Current.GetType() : null;
            if (backdrop != null) backdrop.ApplyTeam(MetaGame.Profile.team);
            if (Root != null) Destroy(Root.gameObject);
            Current = null;
            Build();
            if (open == typeof(ProfileScreen)) Open(Profile);
        }

        public void ShowToast(string text, UIIcon? icon = null, Color? color = null) { if (Toast != null) Toast.Show(text, icon, color); }

        /// <summary>Starts a battle against a roster base behind a paint wipe.</summary>
        public void StartBattle(BaseDefinition b)
        {
            if (b == null || ScreenTransition.Busy) return;
            var lo = BattleLaunch.Prepare(b);
            if (BattleLaunch.LastMercenaryUnpaid) ShowToast("Not enough gold to hire your mercenary", UIIcon.Mercenary, UIColors.RedDark);
            if (backdrop != null) backdrop.CheerAll();
            ScreenTransition.Load(BattleLaunch.BattleScene, lo.playerTeam);
        }
    }
}
