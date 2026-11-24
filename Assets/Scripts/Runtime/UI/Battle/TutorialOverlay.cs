using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>
    /// Level 1 tutorial (spec 6.2). Eight guided steps, each a short card next to what it talks about and completed
    /// by doing it, plus five one-time hints. Every step and hint is saved in PlayerProfile.tutorialSeen when it is
    /// done (hints when shown) and never repeats. A "Skip tutorial" button marks everything seen. The HUD calls
    /// Begin() on Level 1 attack battles (see ShouldRun); tests can call it on any attack battle. Copy and triggers
    /// live in TutorialSteps. Only the skip button takes clicks; the cards let clicks through to the map.
    /// </summary>
    public partial class TutorialOverlay : MonoBehaviour
    {
        BattleHUD hud;
        Camera cam;
        RectTransform safe;
        Callout guide, tip;
        UIButton skip;
        RectTransform finger;

        PlayerArmy army;
        ArmyInput input;
        HexOverlay overlay;
        BattleManager bm;
        bool active, subscribed;

        string step;
        float stepTime;
        int deployedAtStep, tanksAtStep;
        bool tankOutAtStep;
        int stepFrame;
        bool stepAtLoad;
        Vector3 focusAtStep;
        float frontZAtStep;
        readonly List<HexCoord> suggest = new List<HexCoord>(4);

        readonly List<string> queue = new List<string>(4);
        string hint;
        float hintLeft;
        Vector3 hintWorld;
        PlayerUnit hintUnit;
        Transform hintFollow;
        float bubbleCheck;
        readonly List<EnemySquad> squads = new List<EnemySquad>(16);

        public bool Active => active;
        /// <summary>Id of the guided step on screen (TutorialSteps), null when none.</summary>
        public string CurrentStep => step;
        public string CurrentText => step != null ? TutorialSteps.Copy(step) : null;
        /// <summary>Id of the hint on screen, null when none.</summary>
        public string CurrentHint => hint;
        public string CurrentHintText => hint != null ? TutorialSteps.Copy(hint) : null;
        public IReadOnlyList<string> Seen => MetaGame.Profile.tutorialSeen;
        public UIButton SkipButton => skip;
        public Callout Guide => guide;
        public Callout Tip => tip;

        /// <summary>True for Level 1 while anything in the tutorial is still unseen.</summary>
        public static bool ShouldRun(string baseId, PlayerProfile p)
        {
            if (baseId != TutorialSteps.BaseId) return false;
            TutorialSteps.Upgrade(p); // saves from before the advance step
            return !TutorialSteps.AllSeen(p);
        }

        public static TutorialOverlay Create(RectTransform parent, BattleHUD hud, Camera cam)
        {
            var rt = UIFactory.Rect(parent, "Tutorial").Stretch();
            var o = rt.gameObject.AddComponent<TutorialOverlay>();
            o.hud = hud;
            o.cam = cam;
            o.safe = parent;
            o.finger = UIFactory.Rect(rt, "Finger").At(0.5f, 0.5f, 0f, 0f, 130f, 130f, 0.3f, 0.92f);
            var f = UIFactory.Icon(o.finger, UIIcon.Finger, 130f);
            ((RectTransform)f.transform).Stretch();
            o.finger.gameObject.SetActive(false);
            o.guide = Callout.Create(rt, "Guide", 620f, UIColors.Team(UIFactory.PlayerTeam));
            o.tip = Callout.Create(rt, "Tip", 560f, UIColors.Gold);
            o.skip = UIFactory.Button(rt, "Skip", "SKIP TUTORIAL", ButtonColor.Slate, 300f, 72f, UIIcon.FastForward, 28f);
            ((RectTransform)o.skip.transform).At(0f, 1f, 172f, -18f, 300f, 72f, 0f, 1f);
            o.skip.onClick = o.Skip;
            rt.gameObject.SetActive(false);
            return o;
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Starts (or resumes) the tutorial at the first unseen step. Ignored without a player army.</summary>
        public void Begin()
        {
            army = PlayerArmy.Instance;
            input = ArmyInput.Instance;
            bm = BattleManager.Instance;
            if (army == null || input == null || TutorialSteps.AllSeen(MetaGame.Profile)) return;
            overlay = army.GetComponentInChildren<HexOverlay>(true);
            active = true;
            gameObject.SetActive(true);
            squads.Clear();
            squads.AddRange(FindObjectsByType<EnemySquad>(FindObjectsSortMode.None));
            Subscribe(true);
            SetStep(null);
        }

        /// <summary>Marks every step and hint seen and ends the tutorial.</summary>
        public void Skip()
        {
            var p = MetaGame.Profile;
            foreach (var id in TutorialSteps.All) if (!p.tutorialSeen.Contains(id)) p.tutorialSeen.Add(id);
            SaveSystem.Save();
            Hide();
        }

        /// <summary>Ends the tutorial for this battle without marking anything (battle over).</summary>
        public void Hide()
        {
            active = false;
            Subscribe(false);
            step = null;
            hint = null;
            queue.Clear();
            if (overlay != null) overlay.SetSuggested(null);
            if (guide != null) guide.Hide();
            if (tip != null) tip.Hide();
            gameObject.SetActive(false);
        }

        void OnDestroy() { Subscribe(false); }

        void Subscribe(bool on)
        {
            if (on == subscribed) return;
            subscribed = on;
            if (on)
            {
                if (army != null) army.OrderRejected += OnRejected;
                BattleEvents.UnitKilled += OnUnitKilled;
                BattleEvents.PointCaptured += OnPointCaptured;
                BattleEvents.EnemySquadSpawned += OnSquadSpawned;
            }
            else
            {
                if (army != null) army.OrderRejected -= OnRejected;
                BattleEvents.UnitKilled -= OnUnitKilled;
                BattleEvents.PointCaptured -= OnPointCaptured;
                BattleEvents.EnemySquadSpawned -= OnSquadSpawned;
            }
        }

        static bool IsSeen(string id) => TutorialSteps.IsSeen(MetaGame.Profile, id);

        static void MarkSeen(string id)
        {
            var p = MetaGame.Profile;
            if (p.tutorialSeen.Contains(id)) return;
            p.tutorialSeen.Add(id);
            SaveSystem.Save();
        }

        // ------------------------------------------------------------------ guided steps

        int DeployedCount(UnitTypeId? type = null)
        {
            int c = 0;
            var sq = army.Squad;
            for (int i = 0; i < sq.Count; i++)
                if (sq[i].state != EntryState.Ready && (!type.HasValue || sq[i].type == type.Value)) c++;
            return c;
        }

        PlayerUnit FirstUnit(UnitTypeId? type = null)
        {
            var u = army.Units;
            for (int i = 0; i < u.Count; i++) if (!type.HasValue || u[i].Type.id == type.Value) return u[i];
            return null;
        }

        bool TankReady()
        {
            var sq = army.Squad;
            for (int i = 0; i < sq.Count; i++) if (sq[i].type == UnitTypeId.Tank && sq[i].state == EntryState.Ready) return true;
            return false;
        }

        int ReadyTankEntry()
        {
            var sq = army.Squad;
            for (int i = 0; i < sq.Count; i++) if (sq[i].type == UnitTypeId.Tank && sq[i].state == EntryState.Ready) return i;
            return -1;
        }

        float FrontZ()
        {
            var f = TutorialSteps.Foremost(army);
            return f != null ? MapPath.Progress(army.Grid.Center(f.Hex)) : float.MinValue;
        }

        bool PointOneDone()
        {
            var cp = TutorialSteps.FirstPoint(bm);
            return cp == null || cp.Captured || TutorialSteps.FirstPointClear(bm) || TutorialSteps.FirstPointCapturing(bm);
        }

        /// <summary>The step that should show now, or null. Marks steps 6 to 8 seen when they no longer apply.</summary>
        string NextStep()
        {
            if (!IsSeen(TutorialSteps.Deploy)) return input.ArmedEntry >= 0 ? TutorialSteps.Deploy : TutorialSteps.Pick;
            bool units = army.Units.Count > 0;
            if (!IsSeen(TutorialSteps.Unit)) return units ? TutorialSteps.Unit : null;
            if (!IsSeen(TutorialSteps.Move)) return units ? TutorialSteps.Move : null;
            // Steps 7 and 8 answer what happens on the field, so they go ahead of a pending step 5 or 6.
            string urgent = TankOrCapture();
            if (urgent != null) return urgent;
            if (!IsSeen(TutorialSteps.Pan)) return TutorialSteps.Pan;
            if (!IsSeen(TutorialSteps.Advance))
            {
                if (!units) return null;
                // Already up the lane (enemies near or point 1 open): nothing left to ask.
                if (TutorialSteps.EnemyNearArmy(army) || PointOneDone()) MarkSeen(TutorialSteps.Advance);
                else return TutorialSteps.Advance;
            }
            return null;
        }

        /// <summary>Step 7 when enemies are near, else step 8 when the first point is clear, else null. A clear point
        /// wins over the Tank card: the capture is what matters then.</summary>
        string TankOrCapture()
        {
            if (!IsSeen(TutorialSteps.Tank) && TutorialSteps.EnemyNearArmy(army))
            {
                if (PointOneDone()) MarkSeen(TutorialSteps.Tank);
                else if (TankReady() || FirstUnit(UnitTypeId.Tank) != null) return TutorialSteps.Tank;
                else MarkSeen(TutorialSteps.Tank); // no Tank left to place
            }
            if (!IsSeen(TutorialSteps.Capture))
            {
                var cp = TutorialSteps.FirstPoint(bm);
                if (cp == null || cp.Captured) MarkSeen(TutorialSteps.Capture);
                else if (TutorialSteps.FirstPointClear(bm) || TutorialSteps.FirstPointCapturing(bm)) return TutorialSteps.Capture;
            }
            return null;
        }

        bool StepDone()
        {
            switch (step)
            {
                case TutorialSteps.Pick: return input.ArmedEntry >= 0;
                case TutorialSteps.Deploy: return DeployedCount() > deployedAtStep;
                case TutorialSteps.Unit: return stepTime >= TutorialSteps.ReadSeconds || army.Selected != null;
                case TutorialSteps.Move:
                    for (int i = 0; i < army.Units.Count; i++) if (army.Units[i].IsMoving) return true;
                    return false;
                case TutorialSteps.Pan:
                    var bc = BattleCamera.Instance;
                    if (bc == null) return false;
                    // When step 5 is the first card of a battle (a resumed tutorial) the camera is still making its start
                    // snap, which is not the player's drag: re-baseline for a moment.
                    if (stepAtLoad && (stepTime < 0.25f || Time.frameCount < stepFrame + 10)) { focusAtStep = bc.Focus; return false; }
                    return (bc.Focus - focusAtStep).magnitude >= TutorialSteps.PanDistance;
                case TutorialSteps.Advance:
                    if (army.Units.Count == 0) return false;
                    var g = army.Grid;
                    // Four hex rows further along the map's path, or as far as the suggested hex when that is nearer (the
                    // suggestion keeps to the screen, so it can sit a little short).
                    float goal = frontZAtStep + TutorialSteps.AdvanceRows * 1.5f * g.OuterRadius - 0.3f;
                    if (suggest.Count > 0) goal = Mathf.Min(goal, MapPath.Progress(g.Center(suggest[0])) - 0.3f);
                    return TutorialSteps.EnemyNearArmy(army) || PointOneDone() || FrontZ() >= goal;
                case TutorialSteps.Tank:
                    // A clear point 1 ends the card at once, so the capture step is never held up.
                    if (PointOneDone()) return true;
                    var t = FirstUnit(UnitTypeId.Tank);
                    if (DeployedCount(UnitTypeId.Tank) > tanksAtStep || (t != null && t.IsMoving) || (!TankReady() && t == null)) return true;
                    // Information only when the Tank was already out: it ends after a few seconds.
                    return stepTime >= TutorialSteps.TankInfoSeconds && tankOutAtStep;
                case TutorialSteps.Capture:
                    // Done once units stand on the circle, but never before the card could be read.
                    var cp = TutorialSteps.FirstPoint(bm);
                    return cp == null || cp.Captured || (cp.PlayersInside > 0 && stepTime >= TutorialSteps.CaptureMinSeconds);
            }
            return true;
        }

        void SetStep(string id)
        {
            step = id;
            stepTime = 0f;
            stepFrame = Time.frameCount;
            stepAtLoad = Time.timeSinceLevelLoad < 2f;
            suggest.Clear();
            if (id != null)
            {
                deployedAtStep = DeployedCount();
                tanksAtStep = DeployedCount(UnitTypeId.Tank);
                tankOutAtStep = FirstUnit(UnitTypeId.Tank) != null;
                focusAtStep = BattleCamera.Instance != null ? BattleCamera.Instance.Focus : Vector3.zero;
                frontZAtStep = FrontZ();
                if (id == TutorialSteps.Deploy)
                {
                    MarkSeen(TutorialSteps.Pick); // arming is what step 1 asked for
                    TutorialSteps.DeploySuggestions(army, input.ArmedEntry, cam, 3, suggest);
                }
                else if (id == TutorialSteps.Move && TutorialSteps.MoveSuggestion(army, FirstUnit(), cam, out HexCoord h)) suggest.Add(h);
                else if (id == TutorialSteps.Advance
                         && TutorialSteps.LaneSuggestion(army, TutorialSteps.Foremost(army), TutorialSteps.AdvanceSuggestSteps, cam, out HexCoord ah)) suggest.Add(ah);
                else if (id == TutorialSteps.Tank && ReadyTankEntry() >= 0 && FirstUnit(UnitTypeId.Tank) == null)
                    TutorialSteps.TankSuggestions(army, ReadyTankEntry(), cam, TutorialSteps.TankSuggestCount, suggest);
                else if (id == TutorialSteps.Capture && TutorialSteps.CaptureSuggestion(army, bm, out HexCoord ch)) suggest.Add(ch);
                int n = TutorialSteps.Number(id);
                guide.Show("STEP " + n + " OF " + TutorialSteps.Guided.Length, TutorialSteps.Copy(id));
            }
            else guide.Hide();
            if (overlay != null) overlay.SetSuggested(suggest);
            finger.gameObject.SetActive(id == TutorialSteps.Pan);
        }

        void UpdateSteps(float dt)
        {
            if (step != null)
            {
                stepTime += dt;
                if (StepDone())
                {
                    MarkSeen(step);
                    SetStep(null);
                }
                else if (step == TutorialSteps.Deploy && input.ArmedEntry < 0) SetStep(TutorialSteps.Pick); // disarmed
                else if (step == TutorialSteps.Pan && TankOrCapture() is string urgent) { SetStep(urgent); return; } // pan comes back later
                else return;
            }
            string next = NextStep();
            if (next != null) SetStep(next);
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!active) return;
            if (bm != null && bm.Over) { Hide(); return; }
            float dt = Time.unscaledDeltaTime;
            UpdateSteps(dt);
            PollHints(dt);
            UpdateHint(dt);
            // One thing at a time: a hint, or the backup prompt, takes the stage from the guided card.
            bool quiet = hint == null && (hud.Backup == null || !hud.Backup.Visible);
            guide.SetVisible(quiet);
            finger.gameObject.SetActive(step == TutorialSteps.Pan && quiet);
            CollectAvoid();
            if (step != null) PointGuide();
            if (hint != null) PointTip();
            if (finger.gameObject.activeSelf) AnimateFinger();
            if (TutorialSteps.AllSeen(MetaGame.Profile) && step == null && hint == null) Hide();
        }
    }
}
