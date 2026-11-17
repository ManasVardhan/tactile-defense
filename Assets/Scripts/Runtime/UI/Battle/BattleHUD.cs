using System.Collections.Generic;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Core;
using TactileWars.Formation;
using TactileWars.Meta;
using TactileWars.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TactileWars.UI
{
    /// <summary>
    /// Battle HUD, matching the reference layout: dark rounded pause tab top left; round squad badge top right
    /// (white ring, three soldier busts, team colored arc = soldiers alive / starting count, with the number);
    /// hexagonal POW badge beside it that fills with the reinforcement meter and calls backup when tapped.
    /// Plus capture point pips, edge arrows to the objective, edge helmets for off screen enemy squads, shape
    /// hints, the first battle tutorial, pause and result screens. Listens to BattleEvents only (gameplay never
    /// references UI). Updates are event driven; the per frame edge indicators do not allocate.
    /// </summary>
    public partial class BattleHUD : MonoBehaviour
    {
        public UISkin skin;
        public Camera cam;

        public static BattleHUD Instance { get; private set; }
        public UIRoot Root { get; private set; }
        public PauseMenu Pause { get; private set; }
        public ResultScreen Results { get; private set; }
        public UIButton PauseButton { get; private set; }
        public UIButton PowButton { get; private set; }
        public TutorialOverlay Tutorial { get; private set; }
        public int ShownSoldiers { get; private set; }
        public float ShownMeter { get; private set; }

        TeamColorId team, enemy;
        BattleManager bm;
        RectTransform hud;
        CanvasGroup hudGroup;
        UIToast toast;

        // Squad badge.
        RectTransform badge;
        Image arc;
        TextMeshProUGUI countText;
        int lastAlive = -1;

        // POW badge.
        RectTransform pow;
        Image powFill, powGlow, powHex;
        TextMeshProUGUI powLabel, powTap;
        bool powReady;

        // Objectives.
        readonly Image[] pipFill = new Image[2];
        readonly Image[] pipFace = new Image[2];
        readonly Image[] pipIcon = new Image[2];
        readonly TextMeshProUGUI[] pipNum = new TextMeshProUGUI[2];
        TextMeshProUGUI objectiveText;
        CanvasGroup objectiveTextGroup;
        bool[] contested = new bool[2];

        // Edge indicators.
        RectTransform objArrow, objArrowPointer;
        const int MaxEnemyMarkers = 6;
        readonly RectTransform[] enemyMarkers = new RectTransform[MaxEnemyMarkers];
        readonly List<EnemySquad> squads = new List<EnemySquad>(16);

        // Shape hint.
        TextMeshProUGUI shapeHint;
        float shapeHintTimer;

        void Awake() { Instance = this; }

        void Start()
        {
            bm = BattleManager.Instance;
            if (cam == null) cam = BattleCamera.Instance != null ? BattleCamera.Instance.Cam : Camera.main;
            var gc = BattleContext.Config;
            team = gc != null ? gc.playerTeam : TeamColorId.Red;
            enemy = gc != null ? gc.enemyTeam : TeamColorId.Teal;
            defense = BattleSetup.Instance != null && BattleSetup.Instance.DefenseMode;
            // Defense battles: the AI attacker owns 'team' (badge, capture pips); menus use the player's color.
            UIFactory.PlayerTeam = defense ? enemy : team;
            var profile = MetaGame.Profile;
            GameSettings.Apply(profile.settings, cam);

            Root = UIRoot.Create("HUDCanvas", cam, skin, 10);
            hud = UIFactory.Rect(Root.Safe, "HUD").Stretch();
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            BuildPauseTab();
            BuildSquadBadge();
            if (defense) BuildDefenseControls(); else BuildPow();
            BuildObjectives();
            BuildIndicators();
            shapeHint = UIFactory.Text(hud, "ShapeHint", "", 76f, TextStyle.Heading);
            ((RectTransform)shapeHint.transform).At(0.5f, 1f, 0f, -200f, 800f, 100f, 0.5f, 1f);
            shapeHint.gameObject.SetActive(false);
            toast = UIToast.Create(Root.Safe, -250f);
            Tutorial = TutorialOverlay.Create(Root.Safe, cam);
            if (!profile.tutorial.drawStroke && !defense) Tutorial.Begin();
            Pause = PauseMenu.Create(this, Root.Safe);
            Results = ResultScreen.Create(this, Root.Safe);
            if (defense) DefenseResults = DefenseResultCard.Create(this, Root.Safe);

            var found = FindObjectsByType<EnemySquad>(FindObjectsSortMode.None);
            if (!defense) squads.AddRange(found);

            BattleEvents.SoldierCountChanged += OnSoldierCount;
            BattleEvents.ReinforcementChanged += OnMeter;
            BattleEvents.Reinforced += OnReinforced;
            BattleEvents.CaptureProgress += OnCapture;
            BattleEvents.PointCaptured += OnPointCaptured;
            BattleEvents.ShapeRecognized += OnShape;
            BattleEvents.Victory += OnBattleOver;
            BattleEvents.Defeat += OnBattleOver;
            BattleEvents.SquadWiped += OnSquadWiped;
            BattleSession.Rewarded += OnRewarded;
            BattleSession.DefenseRewarded += OnDefenseRewarded;
            BattleEvents.EnemySquadSpawned += OnEnemySquadSpawned;
            BattleEvents.Counterattack += OnCounterattack;

            OnMeter(bm != null ? bm.Meter.Fill : 0f, bm != null && bm.Meter.Ready);
            ShowStartBanner();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            BattleEvents.SoldierCountChanged -= OnSoldierCount;
            BattleEvents.ReinforcementChanged -= OnMeter;
            BattleEvents.Reinforced -= OnReinforced;
            BattleEvents.CaptureProgress -= OnCapture;
            BattleEvents.PointCaptured -= OnPointCaptured;
            BattleEvents.ShapeRecognized -= OnShape;
            BattleEvents.Victory -= OnBattleOver;
            BattleEvents.Defeat -= OnBattleOver;
            BattleEvents.SquadWiped -= OnSquadWiped;
            BattleSession.Rewarded -= OnRewarded;
            BattleSession.DefenseRewarded -= OnDefenseRewarded;
            BattleEvents.EnemySquadSpawned -= OnEnemySquadSpawned;
            BattleEvents.Counterattack -= OnCounterattack;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            GameSpeed.Reset();
            AudioListener.pause = false;
        }

        // ------------------------------------------------------------------ build

        void BuildPauseTab()
        {
            var root = UIFactory.Rect(hud, "PauseTab").At(0f, 1f, 0f, 0f, 150f, 104f, 0f, 1f);
            var bg = UIFactory.Img(root, "Bg", skin.tab, UIColors.WithAlpha(UIColors.SlateDark, 0.9f), 1f, true);
            ((RectTransform)bg.transform).Stretch(-60f, -60f, 0f, 0f);
            var ic = UIFactory.Icon(root, UIIcon.Pause, 60f);
            ((RectTransform)ic.transform).At(0.5f, 0.5f, -6f, 6f, 60f, 60f);
            PauseButton = root.gameObject.AddComponent<UIButton>();
            PauseButton.onClick = () => Pause.Open();
        }

        void BuildSquadBadge()
        {
            badge = UIFactory.Rect(hud, "SquadBadge").At(1f, 1f, -22f, -16f, 176f, 176f, 1f, 1f);
            var sh = UIFactory.Img(badge, "Shadow", skin.glow, new Color(0f, 0f, 0f, 0.35f));
            ((RectTransform)sh.transform).At(0.5f, 0.5f, 0f, -6f, 220f, 220f);
            var disc = UIFactory.Img(badge, "Disc", skin.circle, UIColors.Hex(0x8B909B));
            ((RectTransform)disc.transform).Stretch(8f, 8f, 8f, 8f);
            var inner = UIFactory.Img(badge, "Inner", skin.circle, UIColors.Hex(0xA4A9B3));
            ((RectTransform)inner.transform).Stretch(26f, 26f, 26f, 26f);
            arc = UIFactory.Img(badge, "Arc", skin.ring, UIColors.Team(team));
            ((RectTransform)arc.transform).Stretch(4f, 4f, 4f, 4f);
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = true;
            arc.fillAmount = 1f;
            var ringW = UIFactory.Img(badge, "Ring", skin.ringThin, Color.white);
            ((RectTransform)ringW.transform).Stretch(0f, 0f, 0f, 0f);
            var busts = UIFactory.Rect(badge, "Busts").Stretch(18f, 22f, 18f, 30f);
            var b = skin.Bust(team);
            for (int i = 0; i < 3; i++)
            {
                int k = i == 1 ? 2 : (i == 0 ? 0 : 1); // draw the middle bust last so it sits in front
                float x = k == 0 ? -36f : (k == 1 ? 36f : 0f);
                var bi = UIFactory.Img(busts, "Bust" + k, b, Color.white);
                ((RectTransform)bi.transform).At(0.5f, 0.5f, x, k == 2 ? -2f : 6f, k == 2 ? 90f : 78f, k == 2 ? 90f : 78f);
            }
            var pill = UIFactory.Img(badge, "CountBg", skin.pill, UIColors.WithAlpha(UIColors.SlateDark, 0.92f), UIFactory.PillMult(46f));
            ((RectTransform)pill.transform).At(0.5f, 0f, 0f, -6f, 96f, 46f, 0.5f, 0.5f);
            var ci = UIFactory.Icon(pill.transform, UIIcon.Soldier, 34f);
            ((RectTransform)ci.transform).At(0f, 0.5f, 6f, 1f, 34f, 34f, 0f, 0.5f);
            countText = UIFactory.Text(pill.transform, "Count", "0", 32f, TextStyle.HeadingThin, TextAlignmentOptions.MidlineRight);
            ((RectTransform)countText.transform).Stretch(36f, 0f, 12f, 2f);
        }

        void BuildPow()
        {
            pow = UIFactory.Rect(hud, "PowBadge").At(1f, 1f, -214f, -30f, 140f, 140f, 1f, 1f);
            powGlow = UIFactory.Img(pow, "Glow", skin.glow, UIColors.WithAlpha(UIColors.Team(team), 0f));
            ((RectTransform)powGlow.transform).At(0.5f, 0.5f, 0f, 0f, 250f, 250f);
            var sh = UIFactory.Img(pow, "Shadow", skin.glow, new Color(0f, 0f, 0f, 0.3f));
            ((RectTransform)sh.transform).At(0.5f, 0.5f, 0f, -6f, 180f, 180f);
            var plate = UIFactory.Img(pow, "Plate", skin.circle, UIColors.Hex(0xD3D6DC), 1f, true);
            ((RectTransform)plate.transform).Stretch();
            var ring = UIFactory.Img(pow, "Ring", skin.ringThin, Color.white);
            ((RectTransform)ring.transform).Stretch();
            powHex = UIFactory.Img(pow, "Hex", skin.hex, UIColors.Ink);
            ((RectTransform)powHex.transform).Stretch(16f, 14f, 16f, 18f);
            powFill = UIFactory.Img(pow, "Fill", skin.hex, UIColors.Team(team));
            ((RectTransform)powFill.transform).Stretch(24f, 22f, 24f, 26f);
            powFill.type = Image.Type.Filled;
            powFill.fillMethod = Image.FillMethod.Vertical;
            powFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            powFill.fillAmount = 0f;
            var hexRing = UIFactory.Img(pow, "HexRing", skin.hexRing, UIColors.WithAlpha(Color.white, 0.9f));
            ((RectTransform)hexRing.transform).Stretch(22f, 20f, 22f, 24f);
            powLabel = UIFactory.Text(pow, "Label", "POW", 34f, TextStyle.HeadingThin);
            ((RectTransform)powLabel.transform).Stretch(0f, 0f, 0f, 4f);
            powTap = UIFactory.Text(pow, "Tap", "TAP!", 30f, TextStyle.Heading);
            ((RectTransform)powTap.transform).At(0.5f, 0f, 0f, -30f, 160f, 40f, 0.5f, 0.5f);
            powTap.gameObject.SetActive(false);
            PowButton = pow.gameObject.AddComponent<UIButton>();
            PowButton.sound = Audio.SoundId.UiClick;
            PowButton.onClick = CallReinforcements;
        }

        void BuildObjectives()
        {
            var root = UIFactory.Rect(hud, "Objectives").At(0.5f, 1f, 0f, -16f, 300f, 84f, 0.5f, 1f);
            var bg = UIFactory.Img(root, "Bg", skin.pill, UIColors.WithAlpha(UIColors.SlateDark, 0.72f), UIFactory.PillMult(84f));
            ((RectTransform)bg.transform).Stretch();
            var dash = UIFactory.Img(root, "Link", skin.dash, UIColors.WithAlpha(Color.white, 0.6f));
            dash.type = Image.Type.Tiled;
            ((RectTransform)dash.transform).At(0.5f, 0.5f, 0f, 0f, 90f, 10f);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -78f : 78f;
                var pip = UIFactory.Rect(root, "Pip" + i).At(0.5f, 0.5f, x, 0f, 66f, 66f);
                var back = UIFactory.Img(pip, "Back", skin.circle, UIColors.WithAlpha(Color.white, 0.18f));
                ((RectTransform)back.transform).Stretch();
                pipFill[i] = UIFactory.Img(pip, "Fill", skin.ring, UIColors.Team(team));
                ((RectTransform)pipFill[i].transform).Stretch();
                pipFill[i].type = Image.Type.Filled;
                pipFill[i].fillMethod = Image.FillMethod.Radial360;
                pipFill[i].fillOrigin = (int)Image.Origin360.Top;
                pipFill[i].fillAmount = 0f;
                pipFace[i] = UIFactory.Img(pip, "Face", skin.circle, UIColors.Slate);
                ((RectTransform)pipFace[i].transform).Stretch(9f, 9f, 9f, 9f);
                pipIcon[i] = UIFactory.Icon(pip, UIIcon.Flag, 36f);
                ((RectTransform)pipIcon[i].transform).At(0.5f, 0.5f, 0f, 1f, 36f, 36f);
                pipNum[i] = UIFactory.Text(pip, "Num", (i + 1).ToString(), 22f, TextStyle.HeadingThin);
                ((RectTransform)pipNum[i].transform).At(1f, 0f, 2f, 2f, 30f, 30f, 0.5f, 0.5f);
            }
            objectiveText = UIFactory.Text(hud, "ObjectiveText", "CAPTURE BOTH POINTS", 34f, TextStyle.Heading);
            ((RectTransform)objectiveText.transform).At(0.5f, 1f, 0f, -100f, 700f, 40f, 0.5f, 1f);
            objectiveText.fontSize = 30f;
            objectiveTextGroup = objectiveText.gameObject.AddComponent<CanvasGroup>();
            RefreshPips();
        }

        void BuildIndicators()
        {
            objArrow = UIFactory.Rect(hud, "ObjectiveArrow").At(0.5f, 0.5f, 0f, 0f, 96f, 96f);
            objArrowPointer = UIFactory.Rect(objArrow, "Pointer").At(0.5f, 0.5f, 0f, 0f, 150f, 150f);
            var tri = UIFactory.Icon(objArrowPointer, UIIcon.ArrowRight, 60f);
            ((RectTransform)tri.transform).At(1f, 0.5f, 6f, 0f, 60f, 60f, 1f, 0.5f);
            var plate = UIFactory.Img(objArrow, "Plate", skin.circle, Color.white);
            ((RectTransform)plate.transform).Stretch();
            var inner = UIFactory.Img(objArrow, "Inner", skin.circle, UIColors.Team(team));
            ((RectTransform)inner.transform).Stretch(7f, 7f, 7f, 7f);
            var flag = UIFactory.Icon(objArrow, UIIcon.Flag, 54f);
            ((RectTransform)flag.transform).At(0.5f, 0.5f, 0f, 2f, 54f, 54f);
            objArrow.gameObject.SetActive(false);

            var eb = skin.Bust(enemy);
            for (int i = 0; i < MaxEnemyMarkers; i++)
            {
                var m = UIFactory.Rect(hud, "EnemyMarker" + i).At(0.5f, 0.5f, 0f, 0f, 84f, 84f);
                var p = UIFactory.Img(m, "Plate", skin.circle, UIColors.WithAlpha(UIColors.SlateDark, 0.92f));
                ((RectTransform)p.transform).Stretch();
                var r = UIFactory.Img(m, "Ring", skin.ringThin, UIColors.WithAlpha(UIColors.Team(enemy), 0.95f));
                ((RectTransform)r.transform).Stretch();
                var bi = UIFactory.Img(m, "Bust", eb, Color.white);
                ((RectTransform)bi.transform).Stretch(8f, 6f, 8f, 10f);
                m.gameObject.SetActive(false);
                enemyMarkers[i] = m;
            }
        }

        void ShowStartBanner()
        {
            if (defense) { ShowDefenseBanner(); return; }
            var lo = BattleLoadout.Pending;
            string name = lo != null && !string.IsNullOrEmpty(lo.baseName) ? lo.baseName : "Training Grounds";
            // Between the objective arrow and the squad, clear of the tutorial finger below.
            toast.Show("ATTACK: " + name.ToUpperInvariant(), UIIcon.Sword, UIColors.WithAlpha(UIColors.Team(team), 0.95f), 1.8f, 165f);
        }

        // ------------------------------------------------------------------ events

        void OnSoldierCount(int alive, int max)
        {
            ShownSoldiers = alive;
            arc.fillAmount = max > 0 ? Mathf.Clamp01(alive / (float)max) : 0f;
            countText.SetText("{0}", alive);
            if (lastAlive >= 0 && alive < lastAlive) UIAnim.Punch(badge, 0.05f, 0.2f);
            else if (lastAlive >= 0 && alive > lastAlive) UIAnim.Punch(badge, 0.15f, 0.35f);
            lastAlive = alive;
        }

        void OnMeter(float fill, bool ready)
        {
            if (powFill == null) return;
            ShownMeter = fill;
            powFill.fillAmount = fill;
            if (ready != powReady)
            {
                powReady = ready;
                powTap.gameObject.SetActive(ready);
                powHex.color = ready ? UIColors.TeamDark(team) : UIColors.Ink;
                if (ready) UIAnim.Pop(pow, 0.7f, 0.4f);
                else pow.localScale = Vector3.one;
                if (!ready) powGlow.color = UIColors.WithAlpha(UIColors.Team(team), 0f);
            }
            PowButton.interactable = ready;
        }

        public void CallReinforcements()
        {
            if (bm == null || !bm.CallReinforcements())
            {
                UIAnim.Shake(pow, 8f, 0.25f);
                return;
            }
        }

        void OnReinforced(int n)
        {
            toast.Show("+" + n + " BACKUP SOLDIERS!", UIIcon.Users, UIColors.WithAlpha(UIColors.TeamDark(team), 0.95f));
            var p = MetaGame.Profile;
            if (!p.tutorial.reinforcements) { p.tutorial.reinforcements = true; Save.SaveSystem.Save(); }
        }

        void OnCapture(int i, float p, bool isContested)
        {
            if (i < 0 || i > 1) return;
            pipFill[i].fillAmount = p;
            contested[i] = isContested;
        }

        void OnPointCaptured(int i)
        {
            RefreshPips();
            if (i >= 0 && i < 2) UIAnim.Pop(pipFace[i].transform.parent, 0.6f, 0.45f);
            if (defense) toast.Show("THE ATTACKERS TOOK POINT " + (i + 1) + "!", UIIcon.Flag, UIColors.WithAlpha(UIColors.RedDark, 0.95f));
            else if (bm != null && i + 1 < bm.capturePoints.Length)
                toast.Show("POINT " + (i + 1) + " CAPTURED! NEXT POINT AHEAD", UIIcon.Flag, UIColors.WithAlpha(UIColors.TeamDark(team), 0.95f));
        }

        void RefreshPips()
        {
            for (int i = 0; i < 2; i++)
            {
                var cp = bm != null && i < bm.capturePoints.Length ? bm.capturePoints[i] : null;
                bool captured = cp != null && cp.Captured;
                bool active = cp != null && cp.Active && !captured;
                pipFace[i].color = captured ? UIColors.Team(team) : (active ? UIColors.Slate : UIColors.WithAlpha(UIColors.Slate, 0.6f));
                pipIcon[i].sprite = skin.Icon(captured ? UIIcon.Check : (active ? UIIcon.Flag : UIIcon.Lock));
                pipIcon[i].color = active || captured ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                if (captured) pipFill[i].fillAmount = 1f;
            }
        }

        void OnShape(ShapeType s)
        {
            if (defense) return; // the AI's formations are not the player's strokes
            string t = s switch
            {
                ShapeType.Line => "LINE!",
                ShapeType.Circle => "CIRCLE!",
                ShapeType.Square => "SQUARE!",
                ShapeType.Free => "FREE FORM!",
                _ => null,
            };
            if (t != null)
            {
                shapeHint.text = t;
                shapeHint.gameObject.SetActive(true);
                shapeHintTimer = 1.1f;
                UIAnim.Pop(shapeHint.transform, 1.6f, 0.3f);
            }
            if (Tutorial != null && Tutorial.Active) Tutorial.OnStroke();
        }

        void OnSquadWiped(EnemySquad s)
        {
            if (s != null && s.Bubble != null) toast.Show("SQUAD POPPED!", UIIcon.Skull, UIColors.WithAlpha(UIColors.Ink, 0.9f), 1f);
        }

        void OnBattleOver(BattleResult r)
        {
            hudGroup.interactable = false;
            UIAnim.Fade(hudGroup, 0.35f, 0.4f);
            if (Pause != null) Pause.ForceClose();
            if (Tutorial != null) Tutorial.Hide();
            GameSpeed.Reset();
            if (BattleSession.Last == null && !defense) Results.ShowFallback(r);
        }

        void OnRewarded(RewardSummary s) => Results.Show(s);

        // ------------------------------------------------------------------ per frame (no allocations)

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (shapeHintTimer > 0f)
            {
                shapeHintTimer -= dt;
                if (shapeHintTimer <= 0f) shapeHint.gameObject.SetActive(false);
                else if (shapeHintTimer < 0.3f) shapeHint.alpha = shapeHintTimer / 0.3f;
                else shapeHint.alpha = 1f;
            }
            if (objectiveTextGroup != null && objectiveTextGroup.alpha > 0f && Time.timeSinceLevelLoad > 4f)
                objectiveTextGroup.alpha = Mathf.MoveTowards(objectiveTextGroup.alpha, 0f, dt * 1.5f);
            if (powReady)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                pow.localScale = Vector3.one * (1f + 0.07f * k);
                powGlow.color = UIColors.WithAlpha(UIColors.Team(team), 0.35f + 0.4f * k);
            }
            if (defense) UpdateDefense();
            for (int i = 0; i < 2; i++)
                if (contested[i]) pipFace[i].color = Color.Lerp(UIColors.Slate, UIColors.Red, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f));
            UpdateIndicators();
        }

        void UpdateIndicators()
        {
            if (cam == null || Root == null) return;
            Vector2 size = Root.Safe.rect.size;
            bool over = bm != null && bm.Over;

            // Objective arrow.
            var cp = bm != null ? bm.CurrentPoint : null;
            if (!over && cp != null && !cp.Captured && EdgePosition(cp.transform.position, size, 0.5f, out Vector2 pos, out float angle))
            {
                if (!objArrow.gameObject.activeSelf) objArrow.gameObject.SetActive(true);
                objArrow.anchoredPosition = pos;
                objArrowPointer.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            else if (objArrow.gameObject.activeSelf) objArrow.gameObject.SetActive(false);

            // Enemy squads off screen (nearby ones only, like the reference edge helmets).
            int used = 0;
            Vector3 focus = BattleCamera.Instance != null ? BattleCamera.Instance.Focus : cam.transform.position;
            if (!over)
            {
                for (int i = 0; i < squads.Count && used < MaxEnemyMarkers; i++)
                {
                    var s = squads[i];
                    if (s == null || s.Wiped || s.AliveCount == 0) continue;
                    Vector3 c = s.Center;
                    float dx = c.x - focus.x, dz = c.z - focus.z;
                    if (dx * dx + dz * dz > 34f * 34f) continue;
                    if (!EdgePosition(c, size, 0.6f, out Vector2 p, out _)) continue;
                    // Keep helmets from stacking on the objective arrow or on each other.
                    if (objArrow.gameObject.activeSelf && (p - objArrow.anchoredPosition).sqrMagnitude < 110f * 110f)
                        p.x += p.x >= objArrow.anchoredPosition.x ? 110f : -110f;
                    for (int k = 0; k < used; k++)
                        if ((p - enemyMarkers[k].anchoredPosition).sqrMagnitude < 90f * 90f) p += (Mathf.Abs(p.x) > Mathf.Abs(p.y) * size.x / size.y ? new Vector2(0f, -92f) : new Vector2(92f, 0f));
                    var m = enemyMarkers[used++];
                    if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
                    m.anchoredPosition = p;
                }
            }
            for (int i = used; i < MaxEnemyMarkers; i++)
                if (enemyMarkers[i].gameObject.activeSelf) enemyMarkers[i].gameObject.SetActive(false);
        }

        /// <summary>Canvas position on the screen border toward an off screen world point. False when on screen.</summary>
        bool EdgePosition(Vector3 world, Vector2 size, float height, out Vector2 pos, out float angleDeg)
        {
            Vector3 vp = cam.WorldToViewportPoint(world + Vector3.up * height);
            pos = default; angleDeg = 0f;
            const float inset = 0.015f;
            if (vp.x > inset && vp.x < 1f - inset && vp.y > inset && vp.y < 1f - inset) return false;
            Vector2 d = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
            if (d.sqrMagnitude < 1f) return false;
            float hx = size.x * 0.5f - 64f;
            float hyTop = size.y * 0.5f - 215f;   // stay below the top HUD row
            float hyBottom = size.y * 0.5f - 64f;
            float hy = d.y > 0f ? hyTop : hyBottom;
            float t = Mathf.Min(hx / Mathf.Max(1e-3f, Mathf.Abs(d.x)), hy / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
            pos = d * t;
            angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return true;
        }

        public void SetHudVisible(bool on)
        {
            hudGroup.alpha = on ? 1f : 0f;
            hudGroup.blocksRaycasts = on;
        }
    }
}
