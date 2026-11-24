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
    /// <summary>Main menu / home base. Builds the whole menu UI from the skin at runtime (top bar, the Army and
    /// Attack buttons, screens, first run team selection, one-time callouts) over the live 3D backdrop. The Research,
    /// Base and Profile screens are still built (their code and tests stay alive) but nothing on the menu opens them
    /// while MetaGame.BaseFeaturesEnabled is off.</summary>
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
        public UIButton ArmyButton { get; private set; }
        /// <summary>The notification dot on the Army button: lit while an unlocked unit carries the NEW tag.</summary>
        public bool ArmyDotShown => armyDot != null && armyDot.activeSelf;
        public RectTransform ArmyDot => armyDot != null ? (RectTransform)armyDot.transform : null;
        public MenuGuide Guide { get; private set; }

        RectTransform squadStrip;
        GameObject armyDot;

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
            Guide = MenuGuide.Create(this, UIFactory.Rect(safe, "Guide").Stretch());
            Bar.Refresh(false);
            RefreshHome();
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

            // Army (bottom left): the squad's glyphs above a big pill, mirroring Attack on the right. A red dot on
            // the button while a newly unlocked unit has not been seen in Army.
            var army = UIFactory.Rect(rt, "ArmyGroup").At(0f, 0f, 40f, 34f, 520f, 260f, 0f, 0f);
            ArmyButton = UIFactory.Button(army, "ArmyButton", "ARMY", ButtonColor.White, 400f, 158f, UIIcon.Users, 70f);
            ((RectTransform)ArmyButton.transform).At(0f, 0f, 0f, 0f, 400f, 158f, 0f, 0f);
            ArmyButton.sound = SoundId.UiOpen;
            ArmyButton.onClick = () => Open(Army);
            var dot = UIFactory.Img(ArmyButton.transform, "Dot", skin.circle, Color.white);
            ((RectTransform)dot.transform).At(1f, 1f, 6f, 12f, 50f, 50f, 1f, 1f);
            var dotFill = UIFactory.Img(dot.transform, "Fill", skin.circle, UIColors.Red);
            ((RectTransform)dotFill.transform).Stretch(6f, 6f, 6f, 6f);
            var bang = UIFactory.Text(dot.transform, "Mark", "!", 32f, TextStyle.HeadingThin);
            ((RectTransform)bang.transform).Stretch(0f, 0f, 0f, 3f);
            armyDot = dot.gameObject;
            armyDot.SetActive(false);
            squadStrip = UIFactory.Rect(army, "Squad").At(0f, 0f, 0f, 172f, 520f, 76f, 0f, 0f);

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
            if (Current != null && Current != s) { Current.Anim.Hide(); Current.OnClosed(); }
            Current = s;
            s.Anim.Show();   // activate first: TMP measures text correctly only on active objects
            s.Refresh();
            s.OnOpened();
            SetHomeVisible(false);
            if (Guide != null) Guide.Refresh();
        }

        public void CloseScreen()
        {
            var was = Current;
            if (Current != null) Current.Anim.Hide();
            Current = null;
            if (was != null) was.OnClosed();
            SetHomeVisible(true);
            RefreshHome();
        }

        void SetHomeVisible(bool on)
        {
            homeGroup.interactable = on;
            homeGroup.blocksRaycasts = on;
            UIAnim.Fade(homeGroup, on ? 1f : 0f, 0.2f);
        }

        void OnChanged()
        {
            if (Bar != null) Bar.Refresh(true);
            if (Current != null) Current.Refresh();
            RefreshHome();
        }

        /// <summary>The Army dot, the squad strip over the Army button and the callouts.</summary>
        void RefreshHome()
        {
            if (armyDot != null)
            {
                bool on = MetaGame.HasNewUnits;
                if (on && !armyDot.activeSelf) UIAnim.Pop((RectTransform)armyDot.transform, 0.4f, 0.3f);
                armyDot.SetActive(on);
            }
            if (squadStrip != null) BuildSquadStrip();
            if (Guide != null) Guide.Refresh();
        }

        void BuildSquadStrip()
        {
            UIFactory.ClearChildren(squadStrip);
            var squad = MetaGame.Squad;
            var units = MetaGame.Db != null ? MetaGame.Db.units : null;
            const float d = 56f, gap = 8f, pad = 12f;
            int n = 0;
            foreach (var s in squad) if (s.HasValue) n++;
            float w = pad * 2f + n * d + (n - 1) * gap;
            var bg = UIFactory.Img(squadStrip, "Bg", UIFactory.Skin.pill, UIColors.WithAlpha(UIColors.Ink, 0.62f), UIFactory.PillMult(d + 16f));
            ((RectTransform)bg.transform).At(0f, 0f, 0f, 0f, w, d + 16f, 0f, 0f);
            int i = 0;
            foreach (var s in squad)
            {
                if (!s.HasValue) continue;
                var disc = SquadBuilderPanel.GlyphDisc(bg.transform, "Unit_" + i, units != null ? units.Get(s.Value) : null, d);
                disc.At(0f, 0.5f, pad + i * (d + gap), 0f, d, d, 0f, 0.5f);
                i++;
            }
        }

        /// <summary>Team changes recolor a lot of the UI: rebuild it.</summary>
        public void RebuildForTeam()
        {
            var open = Current != null ? Current.GetType() : null;
            if (backdrop != null) backdrop.ApplyTeam(MetaGame.Profile.team);
            if (Root != null) Destroy(Root.gameObject);
            Current = null;
            Build();
            // Only screens the menu can reach are reopened (never Research, Base or Profile).
            if (open == typeof(ArmyScreen)) Open(Army);
            else if (open == typeof(AttackScreen)) Open(Attack);
            else if (open == typeof(SettingsScreen)) Open(Settings);
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
