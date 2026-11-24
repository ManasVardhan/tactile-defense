using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Save;
using TactileWars.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TactileWars.Tests
{
    /// <summary>The attack battle HUD for units: the unit bar (arm, disarm, counts, greyed buttons, clicks kept off
    /// the map), the badges with health pips over units, the backup prompt when every unit is lost, and the Level 1
    /// tutorial (guided steps, one-time hints, skip, persistence, when it runs).</summary>
    public class ArmyHudTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleLaunch.Clear();
            GameSpeed.Reset();
            BattleTestUtil.Cleanup();
            yield return null;
        }

        static PlayerArmy Army => BattleTestUtil.Army;
        static ArmyInput Input => ArmyInput.Instance;
        static BattleHUD Hud => BattleHUD.Instance;
        static UnitBar Bar => Hud.UnitBar;
        static TutorialOverlay Tut => Hud.Tutorial;

        IEnumerator Setup()
        {
            yield return BattleTestUtil.LoadBattle();
            BattleTestUtil.DisableEnemies();
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsNotNull(Hud, "battle HUD");
            Assert.IsNotNull(Input, "unit input");
        }

        /// <summary>Loads Level 'baseIndex + 1' through the launch path, with a fresh tutorial record when asked.</summary>
        static IEnumerator LoadLevel(int baseIndex, bool freshTutorial, params string[] seen)
        {
            BattleTestUtil.UseTestSave();
            MetaGame.Db = MetaDatabase.Load();
            var p = MetaGame.Profile;
            if (freshTutorial) { p.tutorialSeen.Clear(); p.tutorialSeen.AddRange(seen); }
            BattleLaunch.Prepare(MetaGame.Db.roster.bases[baseIndex]);
            yield return BattleTestUtil.LoadBattle();
        }

        static Vector2 ScreenOf(RectTransform rt) =>
            RectTransformUtility.WorldToScreenPoint(Hud.Root.Camera, rt.TransformPoint(rt.rect.center));

        /// <summary>A free hex near 'p' whose screen point is on screen and not under any HUD element.</summary>
        static HexCoord OpenHexNear(Vector3 p, params HexCoord[] avoid)
        {
            var g = Army.Grid;
            HexCoord best = default;
            float bestD = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c)) continue;
                bool near = false;
                foreach (var a in avoid) if (HexGrid.Distance(a, c) < 2) { near = true; break; }
                if (near) continue;
                Vector2 s = BattleTestUtil.ToScreen(g.Center(c));
                if (s.x < 80f || s.x > BattleTestUtil.W - 80f || s.y < 80f || s.y > BattleTestUtil.H - 80f) continue;
                if (PointerInput.IsOverUI(s)) continue;
                float d = (g.Center(c) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "an open hex on screen");
            return best;
        }

        static int EntryOf(UnitTypeId type, EntryState state)
        {
            for (int i = 0; i < Army.Squad.Count; i++)
                if (Army.Squad[i].type == type && Army.Squad[i].state == state) return i;
            return -1;
        }

        static PlayerUnit DeployType(UnitTypeId type, HexCoord hex)
        {
            int i = EntryOf(type, EntryState.Ready);
            Assert.GreaterOrEqual(i, 0, "a ready " + type);
            var u = Army.Deploy(i, hex);
            Assert.IsNotNull(u, "deploy " + type + " on " + hex);
            return u;
        }

        static List<string> SavedSeen()
        {
            var p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(SaveSystem.FilePath));
            return p.tutorialSeen ?? new List<string>();
        }

        // ------------------------------------------------------------------ unit bar

        [UnityTest]
        public IEnumerator BarShowsOneButtonPerTypeWithCounts()
        {
            yield return Setup();
            Assert.IsNotNull(Bar, "attack battles have a unit bar");
            Assert.AreEqual(2, Bar.Buttons.Count, "default squad (Attacker, Attacker, Tank): one button per type");
            Assert.AreEqual("x2", Bar.CountText(UnitTypeId.Attacker));
            Assert.AreEqual("x1", Bar.CountText(UnitTypeId.Tank));
            Assert.AreEqual("Attacker", Bar.NameText(UnitTypeId.Attacker));
            Assert.AreEqual("A", Bar.GlyphText(UnitTypeId.Attacker));
            Assert.IsTrue(Bar.ButtonFor(UnitTypeId.Attacker).interactable);
            Assert.IsTrue(Bar.ButtonFor(UnitTypeId.Tank).interactable);
            Assert.IsNull(Bar.ButtonFor(UnitTypeId.Sniper), "no button for a type not in the squad");
        }

        [UnityTest]
        public IEnumerator ClickingButtonArmsAndClickingAgainDisarms()
        {
            yield return Setup();
            Bar.ButtonFor(UnitTypeId.Attacker).Click();
            yield return null;
            Assert.AreEqual(EntryOf(UnitTypeId.Attacker, EntryState.Ready), Input.ArmedEntry, "the first ready Attacker is armed");
            Assert.AreEqual(UnitTypeId.Attacker, Bar.HighlightedType, "the armed button is highlighted");
            Bar.ButtonFor(UnitTypeId.Tank).Click();
            yield return null;
            Assert.AreEqual(UnitTypeId.Tank, Army.Squad[Input.ArmedEntry].type, "another type's button switches the armed entry");
            Assert.AreEqual(UnitTypeId.Tank, Bar.HighlightedType);
            Bar.ButtonFor(UnitTypeId.Tank).Click();
            yield return null;
            Assert.AreEqual(-1, Input.ArmedEntry, "clicking the armed button again disarms");
            Assert.IsNull(Bar.HighlightedType, "nothing highlighted once disarmed");
        }

        [UnityTest]
        public IEnumerator CountDropsAfterDeployAndButtonGreysAtZero()
        {
            yield return Setup();
            var hex = OpenHexNear(Army.spawnPoint + Vector3.forward * 4f);
            Bar.ButtonFor(UnitTypeId.Attacker).Click();
            yield return BattleTestUtil.Tap(Army.Grid.Center(hex));
            Assert.IsNotNull(Army.UnitAt(hex), "the armed Attacker deployed on the clicked hex");
            Assert.AreEqual("x1", Bar.CountText(UnitTypeId.Attacker));
            Assert.IsTrue(Bar.ButtonFor(UnitTypeId.Attacker).interactable);

            var hex2 = OpenHexNear(Army.spawnPoint + Vector3.forward * 4f, hex);
            Bar.ButtonFor(UnitTypeId.Tank).Click();
            yield return BattleTestUtil.Tap(Army.Grid.Center(hex2));
            Assert.IsNotNull(Army.UnitAt(hex2), "the Tank deployed");
            Assert.AreEqual("x0", Bar.CountText(UnitTypeId.Tank));
            Assert.IsFalse(Bar.ButtonFor(UnitTypeId.Tank).interactable, "a type with nothing left to deploy is greyed");
            Bar.ButtonFor(UnitTypeId.Tank).Click();
            yield return null;
            Assert.AreEqual(-1, Input.ArmedEntry, "a greyed button arms nothing");
        }

        [UnityTest]
        public IEnumerator ClickOnBarDoesNotReachTheMap()
        {
            yield return Setup();
            // Look up the lane until the Tank button covers a free hex: a click there would be a valid move order.
            var rt = (RectTransform)Bar.ButtonFor(UnitTypeId.Tank).transform;
            Vector2 s = default;
            Vector3 ground = default;
            HexCoord under = default;
            bool open = false;
            for (float dz = 6f; dz <= 60f && !open; dz += 2f)
            {
                BattleCamera.Instance.SnapTo(Army.spawnPoint + Vector3.forward * dz);
                yield return null;
                s = ScreenOf(rt);
                open = PointerInput.ScreenToGround(BattleTestUtil.Cam, s, out ground) && Army.Grid.TryWorldToHex(ground, out under) && Army.Grid.IsFree(under);
            }
            Assert.IsTrue(open, "a free hex under the Tank button at some camera position");
            Assert.IsTrue(PointerInput.IsOverUI(s), "the bar button is a HUD hit area");
            var unit = DeployType(UnitTypeId.Attacker, OpenHexNear(BattleCamera.Instance.Focus, under));
            Army.Select(unit);
            yield return null;
            var hex0 = unit.Hex;
            int rejected = 0;
            Army.OrderRejected += _ => rejected++;

            BattleTestUtil.Pointer.Position = s;
            yield return null;
            yield return null;
            Assert.IsFalse(Input.HasHover, "no hex hover under a HUD button");
            yield return BattleTestUtil.Tap(ground);
            yield return null;
            Assert.AreEqual(hex0, unit.Hex, "no move order from a click on the bar");
            Assert.IsFalse(unit.IsMoving);
            Assert.AreEqual(0, rejected, "no refused order either");
            Assert.AreSame(unit, Army.Selected, "the selection is kept");
        }

        [UnityTest]
        public IEnumerator RestoredUnitRelightsItsButton()
        {
            yield return Setup();
            var tank = DeployType(UnitTypeId.Tank, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            yield return null;
            Assert.IsFalse(Bar.ButtonFor(UnitTypeId.Tank).interactable);
            BattleTestUtil.KillAll(tank);
            yield return null;
            Assert.AreEqual(EntryState.Destroyed, Army.Squad[EntryOf(UnitTypeId.Tank, EntryState.Destroyed)].state);
            Assert.IsFalse(Bar.ButtonFor(UnitTypeId.Tank).interactable, "a lost unit is not back yet");
            var bm = BattleManager.Instance;
            bm.Meter.Add(1000f);
            BattleEvents.RaiseReinforcementChanged(bm.Meter.Fill, bm.Meter.Ready);
            yield return null;
            Hud.PowButton.Click();
            yield return null;
            Assert.AreEqual("x1", Bar.CountText(UnitTypeId.Tank), "backup restored the Tank");
            Assert.IsTrue(Bar.ButtonFor(UnitTypeId.Tank).interactable, "its button lights up again");
        }

        [UnityTest]
        public IEnumerator NoUnitBarInDefenseBattle()
        {
            UITestUtil.UseTempSave();
            BattleTestUtil.MarkTutorialSeen(MetaGame.Profile);
            yield return DefenseBattleTests.LoadDefense(true, 7);
            yield return UITestUtil.WaitUntil(() => BattleHUD.Instance != null && BattleHUD.Instance.Root != null, 5f);
            yield return null;
            Assert.IsTrue(Hud.DefenseMode);
            Assert.IsNull(Hud.UnitBar, "no unit bar in defense");
            Assert.IsNull(Hud.Badges, "no unit badges in defense");
            Assert.IsNull(Hud.Backup, "no backup prompt in defense");
            Assert.AreEqual(0, Object.FindObjectsByType<UnitBar>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(Tut.Active, "no tutorial in defense");
            UITestUtil.Cleanup();
        }

        // ------------------------------------------------------------------ badges

        [UnityTest]
        public IEnumerator BadgeAppearsWhenDamagedOrSelected()
        {
            yield return Setup();
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            var b = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f, a.Hex));
            yield return null;
            var badgeA = Hud.Badges.For(a);
            var badgeB = Hud.Badges.For(b);
            Assert.IsNotNull(badgeA, "one badge per deployed unit");
            Assert.IsNotNull(badgeB);
            Assert.AreEqual("A", badgeA.Glyph);
            Assert.IsFalse(badgeA.Shown, "hidden at full health and not selected");

            Army.Select(a);
            yield return null;
            Assert.IsTrue(badgeA.Shown, "shown while selected");
            Army.Select(null);
            yield return null;
            Assert.IsFalse(badgeA.Shown, "hidden again once deselected");

            var s0 = b.Soldiers[0];
            s0.Health.TakeDamage(s0.Health.max * 0.2f, s0.transform.position + Vector3.forward, TeamColorId.Teal);
            yield return null;
            Assert.IsTrue(badgeB.Shown, "shown once damaged");
            Assert.AreEqual(3, badgeB.FilledPips);
            Assert.AreEqual(0, badgeB.HalfTonePips, "above half health is a full pip");

            b.Soldiers[1].Health.Kill(TeamColorId.Teal);
            yield return null;
            Assert.AreEqual(2, badgeB.FilledPips, "two pips after one soldier dies");
            s0.Health.TakeDamage(s0.Health.max * 0.45f, s0.transform.position + Vector3.forward, TeamColorId.Teal);
            yield return null;
            Assert.AreEqual(2, badgeB.FilledPips);
            Assert.AreEqual(1, badgeB.HalfTonePips, "a soldier below half health shows a half tone pip");

            BattleTestUtil.KillAll(b);
            yield return null;
            Assert.IsNull(Hud.Badges.For(b), "a destroyed unit's badge is removed");
            Assert.IsNotNull(Hud.Badges.For(a));
        }

        [UnityTest]
        public IEnumerator ClickingUnitBadgeSelectsUnit()
        {
            yield return Setup();
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + new Vector3(-6f, 0f, 4f)));
            var b = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + new Vector3(4f, 0f, 4f), a.Hex));
            yield return null;
            yield return null;
            var badgeB = Hud.Badges.For(b);
            Assert.IsTrue(badgeB.GlyphShown, "B's badge is on screen");
            Vector2 at = badgeB.DiscScreenPoint(Hud.Root.Camera);
            Assert.IsFalse(PointerInput.IsOverUI(at), "badges do not block the map (not raycast targets)");
            // The ground under the badge is not B's hex: a plain map click there would not select B.
            Assert.IsTrue(PointerInput.ScreenToGround(BattleTestUtil.Cam, at, out Vector3 ground));
            Army.Grid.TryWorldToHex(ground, out HexCoord under);
            Assert.AreNotEqual(b.Hex, under, "the badge floats above the unit, off its hex");
            Assert.IsNull(Army.UnitAt(under), "and no unit holds the hex under the badge");

            BattleTestUtil.Pointer.Click(at);
            yield return null;
            yield return null;
            Assert.AreSame(b, Army.Selected, "clicking B's badge selects B");
            Assert.IsFalse(b.IsMoving);

            // With A selected, a click on B's badge switches to B and orders nothing.
            Army.Select(a);
            yield return null;
            HexCoord aHex = a.Hex;
            at = badgeB.DiscScreenPoint(Hud.Root.Camera);
            BattleTestUtil.Pointer.Click(at);
            yield return null;
            yield return null;
            Assert.AreSame(b, Army.Selected, "the badge click selects B over the selected A");
            Assert.IsFalse(a.IsMoving, "A was not ordered to the ground under the badge");
            Assert.AreEqual(aHex, a.Hex);
        }

        [UnityTest]
        public IEnumerator ContestedPadShowsFeedback()
        {
            var seen = new List<string>(TutorialSteps.All);
            seen.Remove(TutorialSteps.HintContested);
            yield return LoadLevel(0, true, seen.ToArray());
            BattleTestUtil.DisableEnemies();
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(Tut.Active, "the tutorial runs for its last unseen hint");
            var bm = BattleManager.Instance;
            var cp = bm.capturePoints[0];
            var u = Army.Deploy(0, BattleTestUtil.PadHex(cp));
            Assert.IsNotNull(u);
            foreach (var s in u.Soldiers) s.HoldFire = true; // keep the enemies alive while we look
            var playerArc = cp.progressRing.sharedMaterial;
            var enemyArc = BattleContext.Visuals.Get(BattleContext.Config.enemyTeam).arc;
            Assert.AreNotSame(playerArc, enemyArc);
            BattleCamera.Instance.SnapTo(cp.transform.position - Vector3.forward * 3f);
            var squad = BattleTestUtil.SpawnEnemySquad(cp.transform.position + new Vector3(0f, 0f, 1.2f), 2, false, true);
            yield return BattleTestUtil.Wait(0.4f);
            Assert.Greater(UnitRegistry.CountWithin(Team.Enemy, cp.transform.position, cp.Radius), 0, "enemies stand on the circle");

            bool sawEnemy = false, sawPlayer = false;
            float p0 = cp.Progress;
            float end = Time.time + 1.5f;
            while (Time.time < end)
            {
                Assert.IsTrue(cp.Contested && cp.ShowsContested, "a unit and enemies on the circle: contested");
                if (cp.RingShowsEnemy) { sawEnemy = true; Assert.AreSame(enemyArc, cp.progressRing.sharedMaterial, "the ring flashes the enemy color"); }
                else { sawPlayer = true; Assert.AreSame(playerArc, cp.progressRing.sharedMaterial); }
                yield return null;
            }
            Assert.IsTrue(sawEnemy && sawPlayer, "the ring flashes (enemy color, then the player's)");
            Assert.AreEqual(p0, cp.Progress, 1e-4f, "no progress while contested");
            Assert.AreEqual(TutorialSteps.HintContested, Tut.CurrentHint, "the player is told why");
            Assert.AreEqual("Enemies are on the circle. Clear them to capture.", Tut.CurrentHintText);
            CollectionAssert.Contains(SavedSeen(), TutorialSteps.HintContested, "persisted like the other hints");
            // Capture a frame with the ring in the enemy color.
            for (int i = 0; i < 40 && !cp.RingShowsEnemy; i++) yield return null;
            BattleTestUtil.Capture("pad_contested");

            // Clearing the circle ends the contested look and the capture goes on.
            foreach (var s in squad.GetComponentsInChildren<TactileWars.Units.Soldier>()) if (s.IsAlive) s.Health.Kill(TeamColorId.Red);
            yield return BattleTestUtil.Wait(0.5f);
            Assert.IsFalse(cp.ShowsContested);
            Assert.IsFalse(cp.RingShowsEnemy);
            Assert.AreSame(playerArc, cp.progressRing.sharedMaterial, "the ring is back in the player's color");
            Assert.Greater(cp.Progress, p0, "the capture moves once the circle is clear");
        }

        // ------------------------------------------------------------------ backup prompt

        [UnityTest]
        public IEnumerator BackupPromptShowsWhenAllUnitsLostAndMeterReady()
        {
            yield return Setup();
            var bm = BattleManager.Instance;
            var prompt = Hud.Backup;
            Assert.IsNotNull(prompt);
            Assert.IsFalse(prompt.Visible, "nothing lost yet");
            bm.Meter.Add(1000f);
            BattleEvents.RaiseReinforcementChanged(bm.Meter.Fill, bm.Meter.Ready);
            var units = BattleTestUtil.DeployAll();
            yield return null;
            Assert.IsFalse(prompt.Visible, "units alive, meter ready: no prompt");
            BattleTestUtil.KillAll(units[0]);
            yield return null;
            Assert.IsFalse(prompt.Visible, "one unit lost but others alive: no prompt");
            BattleTestUtil.KillAll(units[1]);
            BattleTestUtil.KillAll(units[2]);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsFalse(bm.Over, "the battle waits for backup");
            Assert.IsTrue(prompt.Visible, "every unit lost, nothing to deploy, meter ready: prompt shows");
            Assert.AreEqual("All units lost. Click Backup to bring one back.", prompt.Text);
            Assert.Greater(prompt.Pulse, 0f, "the backup button pulses");
            Hud.PowButton.Click();
            yield return null;
            Assert.AreEqual(1, Army.ReadyCount, "backup brought a unit back");
            Assert.IsFalse(prompt.Visible, "the prompt goes once backup is used");
        }

        // ------------------------------------------------------------------ tutorial

        [UnityTest]
        public IEnumerator TutorialWalksThroughGuidedSteps()
        {
            // The real Level 1 layout: point 1 is guarded and its passive squads stay put, so steps 6 and 7 (which
            // would go ahead of step 5 once their triggers hold) wait for the player.
            yield return LoadLevel(0, true);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual("b01", BattleSetup.Instance.Applied.baseId);
            Assert.IsTrue(Tut.Active, "Level 1 runs the tutorial on a fresh profile");
            Assert.AreEqual("pick", Tut.CurrentStep);
            Assert.AreEqual("This is your squad. Click the Attacker.", Tut.CurrentText);
            Assert.IsTrue(Tut.SkipButton.gameObject.activeInHierarchy, "skip is visible");

            Bar.ButtonFor(UnitTypeId.Attacker).Click();
            yield return null;
            yield return null;
            Assert.AreEqual("deploy", Tut.CurrentStep);
            CollectionAssert.Contains(Tut.Seen, "pick");
            var overlay = Army.GetComponentInChildren<HexOverlay>();
            Assert.AreEqual(3, overlay.Suggested.Count, "three glowing hexes");
            foreach (var h in overlay.Suggested) Assert.IsTrue(Army.CanDeploy(Input.ArmedEntry, h), h + " is deployable");

            yield return BattleTestUtil.Tap(Army.Grid.Center(overlay.Suggested[0]));
            yield return null;
            Assert.AreEqual(1, Army.Units.Count, "deployed on a glowing hex");
            Assert.AreEqual("unit", Tut.CurrentStep);
            CollectionAssert.Contains(Tut.Seen, "deploy");
            Assert.AreEqual(0, overlay.Suggested.Count, "the glow goes once deployed");

            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == "move", 8f);
            Assert.AreEqual("move", Tut.CurrentStep);
            CollectionAssert.Contains(Tut.Seen, "unit");
            Assert.AreEqual(1, overlay.Suggested.Count, "one suggested spot up the lane");
            var unit = Army.Units[0];
            var spot = overlay.Suggested[0];
            Assert.AreEqual(4, HexGrid.Distance(unit.Hex, spot), "four hexes away");
            Assert.Greater(MapPath.Progress(Army.Grid.Center(spot)), MapPath.Progress(Army.Grid.Center(unit.Hex)), "further along the map's path");

            yield return BattleTestUtil.Tap(unit.Centroid);
            Assert.AreSame(unit, Army.Selected, "clicked the unit");
            Assert.AreEqual("move", Tut.CurrentStep);
            yield return null;
            Assert.IsFalse(Tut.Guide.Covers(LocalOf(unit.Centroid), 30f), "the card does not cover the selected unit");
            Assert.IsFalse(Tut.Guide.Covers(LocalOf(Army.Grid.Center(spot)), 30f), "nor the suggested hex");
            yield return BattleTestUtil.Tap(Army.Grid.Center(spot));
            yield return null;
            Assert.AreEqual(spot, unit.Hex, "moved to the suggested spot");
            Assert.AreEqual("pan", Tut.CurrentStep);
            CollectionAssert.Contains(Tut.Seen, "move");

            Vector3 f0 = BattleCamera.Instance.Focus;
            yield return BattleTestUtil.DragScreen(new Vector2(640f, 300f), new Vector2(640f, 470f));
            yield return null;
            Assert.Greater((BattleCamera.Instance.Focus - f0).magnitude, 1f, "the drag panned");
            CollectionAssert.Contains(Tut.Seen, "pan");
            Assert.AreNotEqual("pan", Tut.CurrentStep);

            var saved = SavedSeen();
            foreach (var id in new[] { "pick", "deploy", "unit", "move", "pan" })
                CollectionAssert.Contains(saved, id, id + " is in the saved profile");
            CollectionAssert.DoesNotContain(saved, "tank", "step 7 waits for enemies");

            // Step 6: move up the lane, with a suggested hex.
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == "advance", 3f);
            Assert.AreEqual("advance", Tut.CurrentStep);
            Assert.AreEqual("STEP 6 OF 8", Tut.Guide.Header);
            Assert.AreEqual("Now move your units along the track toward the enemy.", Tut.CurrentText);
            Assert.AreEqual(1, overlay.Suggested.Count, "one suggested hex up the lane");
            var ahead = overlay.Suggested[0];
            Assert.AreEqual(5, HexGrid.Distance(unit.Hex, ahead), "five hexes up from the foremost unit");
            Assert.Greater(MapPath.Progress(Army.Grid.Center(ahead)), MapPath.Progress(Army.Grid.Center(unit.Hex)));
            yield return BattleTestUtil.Tap(unit.Centroid);
            Assert.AreSame(unit, Army.Selected, "re-clicking the selected unit keeps it selected");
            yield return BattleTestUtil.Tap(Army.Grid.Center(ahead));
            yield return null;
            Assert.AreEqual(ahead, unit.Hex, "sent up the lane");
            CollectionAssert.Contains(Tut.Seen, "advance");
            Assert.AreNotEqual("advance", Tut.CurrentStep);
        }

        Vector2 LocalOf(Vector3 world)
        {
            var safe = (RectTransform)Tut.Guide.transform.parent.parent;
            Vector3 sp = BattleTestUtil.Cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, sp, Hud.Root.Camera, out Vector2 local);
            return local;
        }

        [UnityTest]
        public IEnumerator TutorialHintsFireOnceEach()
        {
            yield return LoadLevel(0, true);
            BattleTestUtil.DisableEnemies();
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(Tut.Active);
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            yield return null;
            Assert.IsNull(Tut.CurrentHint);

            a.Soldiers[0].Health.Kill(TeamColorId.Teal);
            yield return null;
            Assert.AreEqual("hint_pips", Tut.CurrentHint, "a soldier dying explains the pips");
            Assert.AreEqual("The dots over a unit show how many soldiers it has left.", Tut.CurrentHintText);
            CollectionAssert.Contains(Tut.Seen, "hint_pips");
            yield return UITestUtil.WaitUntil(() => Tut.CurrentHint == null, 10f);
            Assert.IsNull(Tut.CurrentHint, "the hint goes away on its own");

            a.Soldiers[1].Health.Kill(TeamColorId.Teal);
            yield return null;
            yield return null;
            Assert.IsNull(Tut.CurrentHint, "the same hint never shows twice");

            Bar.ButtonFor(UnitTypeId.Attacker).Click();
            yield return null;
            yield return BattleTestUtil.Tap(Army.Grid.Center(a.Hex));
            yield return null;
            Assert.AreEqual("hint_reject", Tut.CurrentHint, "a refused click explains why");
            Assert.AreEqual("You can't go there. Pick an open spot.", Tut.CurrentHintText);
            CollectionAssert.Contains(SavedSeen(), "hint_reject");
            int count = 0;
            foreach (var id in Tut.Seen) if (id == "hint_pips") count++;
            Assert.AreEqual(1, count, "recorded once");
        }

        [UnityTest]
        public IEnumerator SkipTutorialMarksEverythingSeen()
        {
            yield return LoadLevel(0, true);
            BattleTestUtil.DisableEnemies();
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(Tut.Active);
            Assert.AreEqual("pick", Tut.CurrentStep);
            Tut.SkipButton.Click();
            yield return null;
            Assert.IsFalse(Tut.Active, "skipping ends the tutorial");
            Assert.IsNull(Tut.CurrentStep);
            Assert.IsFalse(Tut.SkipButton.gameObject.activeInHierarchy, "skip is gone with it");
            var saved = SavedSeen();
            foreach (var id in TutorialSteps.All)
            {
                CollectionAssert.Contains(Tut.Seen, id);
                CollectionAssert.Contains(saved, id, id + " saved");
            }
            Bar.ButtonFor(UnitTypeId.Attacker).Click();
            yield return null;
            Assert.AreEqual(0, Army.GetComponentInChildren<HexOverlay>().Suggested.Count, "no glowing hexes after a skip");
        }

        [UnityTest]
        public IEnumerator TutorialDoesNotRunOnOtherLevelsOrWhenSeen()
        {
            yield return LoadLevel(1, true);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual("b02", BattleSetup.Instance.Applied.baseId);
            Assert.IsFalse(Tut.Active, "Level 2 has no tutorial");
            Assert.IsFalse(Tut.SkipButton.gameObject.activeInHierarchy);

            BattleTestUtil.MarkTutorialSeen(MetaGame.Profile);
            BattleLaunch.Prepare(MetaGame.Db.roster.bases[0]);
            yield return BattleTestUtil.LoadBattle();
            yield return BattleTestUtil.Wait(0.3f);
            Assert.AreEqual("b01", BattleSetup.Instance.Applied.baseId);
            Assert.IsFalse(Tut.Active, "Level 1 does not repeat a finished tutorial");

            Assert.IsTrue(TutorialOverlay.ShouldRun("b01", new PlayerProfile()), "a new profile on Level 1");
            Assert.IsFalse(TutorialOverlay.ShouldRun("", new PlayerProfile()), "no base id (scene defaults)");
            Assert.IsFalse(TutorialOverlay.ShouldRun("b03", new PlayerProfile()));
        }

        // ------------------------------------------------------------------ tutorial fixes

        /// <summary>A passive squad of four grunts 'ahead' units in front of the unit, inside the step 6 distance.</summary>
        static TactileWars.Units.EnemySquad EnemyAhead(PlayerUnit u, float ahead)
        {
            return BattleTestUtil.SpawnEnemySquad(u.Centroid + Vector3.forward * ahead, 4, false, true);
        }

        [UnityTest]
        public IEnumerator TankStepDoesNotStallWhenTankAlreadyDeployed()
        {
            yield return LoadLevel(0, true, TutorialSteps.Pick, TutorialSteps.Deploy, TutorialSteps.Unit, TutorialSteps.Move, TutorialSteps.Pan, TutorialSteps.Advance);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(Tut.Active);
            var units = BattleTestUtil.DeployAll(Army.spawnPoint + Vector3.forward * 4f);
            Assert.AreEqual(0, Army.ReadyCount, "the whole squad is out, the Tank too");
            yield return null;
            Assert.IsNull(Tut.CurrentStep, "no enemy near yet and point 1 is guarded");
            PlayerUnit tank = null;
            foreach (var u in units) if (u.Type.id == UnitTypeId.Tank) tank = u;

            EnemyAhead(tank, 10f);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Tank, 5f);
            Assert.AreEqual(TutorialSteps.Tank, Tut.CurrentStep, "enemies near show step 6");
            float shown = Time.realtimeSinceStartup;
            bool tankMoved = false;
            while (!MetaGame.Profile.tutorialSeen.Contains(TutorialSteps.Tank) && Time.realtimeSinceStartup - shown < 8f)
            {
                tankMoved |= tank.IsMoving;
                yield return null;
            }
            float took = Time.realtimeSinceStartup - shown;
            Debug.Log($"[Test] tank step seen after {took:0.0}s without a Tank order");
            Assert.IsFalse(tankMoved, "the Tank was never moved");
            CollectionAssert.Contains(Tut.Seen, TutorialSteps.Tank, "step 6 is marked seen on its own within 8 s");
            Assert.AreNotEqual(TutorialSteps.Tank, Tut.CurrentStep);

            // Step 7 is not held up: clear point 1 and it shows.
            var cp = BattleManager.Instance.capturePoints[0];
            float clear = BattleManager.Instance.battleConfig.captureRadius + TutorialSteps.CaptureClearMargin;
            foreach (var h in new List<Health>(UnitRegistry.Of(Team.Enemy)))
                if (h != null && h.IsAlive && h.kind != TargetKind.Structure && (h.transform.position - cp.transform.position).magnitude < clear + 1f) h.Kill(TeamColorId.Red);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Capture, 5f);
            Assert.AreEqual(TutorialSteps.Capture, Tut.CurrentStep, "step 7 follows");
        }

        [UnityTest]
        public IEnumerator TankAndCaptureStepsPreemptPendingPan()
        {
            yield return LoadLevel(0, true, TutorialSteps.Pick, TutorialSteps.Deploy, TutorialSteps.Unit, TutorialSteps.Move);
            yield return BattleTestUtil.Wait(0.3f);
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            yield return null;
            yield return null;
            Assert.AreEqual(TutorialSteps.Pan, Tut.CurrentStep, "step 5 waits for a drag; seen: " + string.Join(", ", Tut.Seen));

            // Never drag. Enemies come near: step 6 takes over from step 5.
            EnemyAhead(a, 10f);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep != TutorialSteps.Pan, 5f);
            Assert.AreEqual(TutorialSteps.Tank, Tut.CurrentStep, "step 6, not step 5");
            CollectionAssert.DoesNotContain(Tut.Seen, TutorialSteps.Pan, "step 5 is still owed");

            // Deploying the Tank completes step 6; step 5 comes back while point 1 is still guarded.
            DeployType(UnitTypeId.Tank, OpenHexNear(Army.spawnPoint + Vector3.forward * 2f, a.Hex));
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Pan, 5f);
            CollectionAssert.Contains(Tut.Seen, TutorialSteps.Tank);
            Assert.AreEqual(TutorialSteps.Pan, Tut.CurrentStep, "step 5 returns");

            // Point 1 clear: step 7 takes over from step 5 too.
            var cp = BattleManager.Instance.capturePoints[0];
            foreach (var h in new List<Health>(UnitRegistry.Of(Team.Enemy)))
                if (h != null && h.IsAlive && h.kind != TargetKind.Structure && (h.transform.position - cp.transform.position).magnitude < 7f) h.Kill(TeamColorId.Red);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Capture, 5f);
            Assert.AreEqual(TutorialSteps.Capture, Tut.CurrentStep, "step 7, not step 5");
            CollectionAssert.DoesNotContain(Tut.Seen, TutorialSteps.Pan);
        }

        static readonly string[] UpToPan = { TutorialSteps.Pick, TutorialSteps.Deploy, TutorialSteps.Unit, TutorialSteps.Move, TutorialSteps.Pan };
        static readonly string[] UpToAdvance = { TutorialSteps.Pick, TutorialSteps.Deploy, TutorialSteps.Unit, TutorialSteps.Move, TutorialSteps.Pan, TutorialSteps.Advance };

        [UnityTest]
        public IEnumerator AdvanceStepAppearsAfterPanAndCompletes()
        {
            yield return LoadLevel(0, true, UpToPan);
            yield return BattleTestUtil.Wait(0.3f);
            Assert.IsTrue(Tut.Active);
            Assert.IsNull(Tut.CurrentStep, "nothing to say before a unit is out");
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            yield return null;
            yield return null;
            Assert.AreEqual(TutorialSteps.Advance, Tut.CurrentStep, "step 6 follows step 5");
            Assert.AreEqual(6, TutorialSteps.Number(TutorialSteps.Advance));
            Assert.AreEqual(8, TutorialSteps.Guided.Length, "eight guided steps");
            Assert.AreEqual("STEP 6 OF 8", Tut.Guide.Header);
            var overlay = Army.GetComponentInChildren<HexOverlay>();
            Assert.AreEqual(1, overlay.Suggested.Count, "one suggested hex");
            var spot = overlay.Suggested[0];
            Assert.AreEqual(TutorialSteps.AdvanceSuggestSteps, HexGrid.Distance(a.Hex, spot), "about five hexes from the foremost unit");
            Assert.Greater(MapPath.Progress(Army.Grid.Center(spot)) - MapPath.Progress(Army.Grid.Center(a.Hex)), Army.Grid.OuterRadius * 1.5f * 4f - 0.01f, "well along the map's path");

            // A small step does not finish it; four hex rows up the lane does.
            var small = OpenHexNear(Army.Grid.Center(a.Hex) + Vector3.forward * 2.2f, a.Hex);
            Assert.IsTrue(Army.OrderMove(a, small));
            yield return null;
            yield return null;
            Assert.AreEqual(TutorialSteps.Advance, Tut.CurrentStep, "one row is not enough");
            Assert.IsTrue(Army.OrderMove(a, spot));
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep != TutorialSteps.Advance, 3f);
            CollectionAssert.Contains(Tut.Seen, TutorialSteps.Advance, "moving up the lane completes step 6");
            CollectionAssert.Contains(SavedSeen(), TutorialSteps.Advance, "and it is saved");
            Assert.AreEqual(0, overlay.Suggested.Count, "the glow goes with it");
        }

        [UnityTest]
        public IEnumerator AdvanceStepEndsWhenEnemiesAreNear()
        {
            yield return LoadLevel(0, true, UpToPan);
            yield return BattleTestUtil.Wait(0.3f);
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            yield return null;
            yield return null;
            Assert.AreEqual(TutorialSteps.Advance, Tut.CurrentStep);
            EnemyAhead(a, 10f);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep != TutorialSteps.Advance, 5f);
            CollectionAssert.Contains(Tut.Seen, TutorialSteps.Advance, "the tank trigger completes step 6");
            Assert.AreEqual(TutorialSteps.Tank, Tut.CurrentStep, "and the Tank step takes over");
        }

        [UnityTest]
        public IEnumerator TankStepSuggestsHexesWhenTankNotDeployed()
        {
            yield return LoadLevel(0, true, UpToAdvance);
            yield return BattleTestUtil.Wait(0.3f);
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            yield return null;
            var enemy = EnemyAhead(a, 10f);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Tank, 5f);
            Assert.AreEqual(TutorialSteps.Tank, Tut.CurrentStep);
            yield return null;
            var overlay = Army.GetComponentInChildren<HexOverlay>();
            Assert.AreEqual(TutorialSteps.TankSuggestCount, overlay.Suggested.Count, "two hexes glow for the Tank");
            int tankEntry = EntryOf(UnitTypeId.Tank, EntryState.Ready);
            Vector3 from = a.Centroid, to = enemy.transform.position;
            foreach (var h in overlay.Suggested)
            {
                Vector3 c = Army.Grid.Center(h);
                Assert.IsTrue(Army.CanDeploy(tankEntry, h), h + " is deployable for the Tank");
                Assert.Greater(Vector3.Dot(c - from, (to - from).normalized), 0.5f, h + " is in front of the Attacker");
                Assert.Less((c - to).magnitude, (from - to).magnitude, h + " is between the Attacker and the enemy");
            }

            // Placing the Tank on one finishes the step; with the Tank already out the card stays informational.
            Bar.ButtonFor(UnitTypeId.Tank).Click();
            yield return null;
            yield return BattleTestUtil.Tap(Army.Grid.Center(overlay.Suggested[0]));
            yield return null;
            Assert.IsNotNull(FirstTank(), "the Tank deployed on a glowing hex");
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep != TutorialSteps.Tank, 3f);
            CollectionAssert.Contains(Tut.Seen, TutorialSteps.Tank);
        }

        static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        static PlayerUnit FirstTank()
        {
            foreach (var u in Army.Units) if (u.Type.id == UnitTypeId.Tank) return u;
            return null;
        }

        [UnityTest]
        public IEnumerator TankStepWithTankDeployedSuggestsNothing()
        {
            yield return LoadLevel(0, true, UpToAdvance);
            yield return BattleTestUtil.Wait(0.3f);
            var units = BattleTestUtil.DeployAll(Army.spawnPoint + Vector3.forward * 4f);
            yield return null;
            EnemyAhead(units[0], 10f);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Tank, 5f);
            Assert.AreEqual(TutorialSteps.Tank, Tut.CurrentStep);
            Assert.AreEqual(0, Army.GetComponentInChildren<HexOverlay>().Suggested.Count, "informational: no glowing hexes");
        }

        [UnityTest]
        public IEnumerator CaptureStepPreemptsTankCard()
        {
            yield return LoadLevel(0, true, UpToAdvance);
            yield return BattleTestUtil.Wait(0.3f);
            var units = BattleTestUtil.DeployAll(Army.spawnPoint + Vector3.forward * 4f);
            yield return null;
            EnemyAhead(units[0], 10f);
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Tank, 5f);
            Assert.AreEqual(TutorialSteps.Tank, Tut.CurrentStep, "the Tank card shows (the Tank is out: information only)");

            // Point 1 clears while the Tank card is up: the capture step comes at once, not after the card's 5 s.
            var bm = BattleManager.Instance;
            var cp = bm.capturePoints[0];
            float clear = bm.battleConfig.captureRadius + TutorialSteps.CaptureClearMargin;
            foreach (var h in new List<Health>(UnitRegistry.Of(Team.Enemy)))
                if (h != null && h.IsAlive && h.kind != TargetKind.Structure && (h.transform.position - cp.transform.position).magnitude < clear + 1f) h.Kill(TeamColorId.Red);
            float t0 = Time.realtimeSinceStartup;
            yield return UITestUtil.WaitUntil(() => Tut.CurrentStep == TutorialSteps.Capture, 5f);
            float took = Time.realtimeSinceStartup - t0;
            Debug.Log($"[Test] capture step after {took:0.00}s");
            Assert.AreEqual(TutorialSteps.Capture, Tut.CurrentStep);
            Assert.Less(took, 1f, "a clear point ends the Tank card immediately");
            CollectionAssert.Contains(Tut.Seen, TutorialSteps.Tank);

            // A pointer on the pad itself: one glowing pad hex.
            var overlay = Army.GetComponentInChildren<HexOverlay>();
            Assert.AreEqual(1, overlay.Suggested.Count, "one glowing hex on the pad");
            Assert.IsTrue(Army.IsPadHex(overlay.Suggested[0], cp), "the glow is on point 1's pad");
        }

        [UnityTest]
        public IEnumerator EveryDeployedUnitShowsItsGlyphBadge()
        {
            yield return Setup();
            var a = DeployType(UnitTypeId.Attacker, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f));
            var t = DeployType(UnitTypeId.Tank, OpenHexNear(Army.spawnPoint + Vector3.forward * 4f, a.Hex));
            yield return null;
            yield return null;
            var ba = Hud.Badges.For(a);
            var bt = Hud.Badges.For(t);
            Assert.IsTrue(ba.GlyphShown, "an Attacker at full health, not selected, shows its glyph");
            Assert.IsTrue(bt.GlyphShown, "so does the Tank");
            Assert.IsFalse(ba.Shown, "pips only when damaged or selected");
            Assert.IsFalse(bt.Shown);
            Assert.AreEqual("A", ba.Glyph);
            Assert.AreEqual("T", bt.Glyph);
            Assert.AreEqual(a.Type.accent, ba.AccentColor, "badge in the type's accent");
            Assert.AreEqual(t.Type.accent, bt.AccentColor);
            Assert.AreNotEqual(a.Type.accent, t.Type.accent, "types have distinct accents");
            // The accents stay apart from every team color.
            foreach (TeamColorId id in System.Enum.GetValues(typeof(TeamColorId)))
            {
                if (BattleColors.Palette == null) break;
                var tc = BattleColors.Palette.Get(id);
                if (tc.id != id) continue;
                foreach (var acc in new[] { a.Type.accent, t.Type.accent })
                {
                    float d = Mathf.Abs(acc.r - tc.primary.r) + Mathf.Abs(acc.g - tc.primary.g) + Mathf.Abs(acc.b - tc.primary.b);
                    Assert.Greater(d, 0.2f, "accent " + acc + " is not the " + id + " team color");
                }
            }
            // The unit bar uses the same accent.
            Assert.AreEqual(a.Type.accent, Bar.AccentOf(UnitTypeId.Attacker));
            Assert.AreEqual(t.Type.accent, Bar.AccentOf(UnitTypeId.Tank));
            // And the soldiers carry it (helmet rim), with team colors kept.
            foreach (var u in new[] { a, t })
                foreach (var s in u.Soldiers)
                {
                    var acc = s.tint.CurrentAccent;
                    Assert.IsNotNull(acc, "soldier accent material");
                    Assert.AreNotSame(s.tint.baseAccent, acc, "the plain accent was swapped");
                    Color got = acc.GetColor("_BaseColor"), want = u.Type.accent;
                    float err = Mathf.Min(Diff(got, want), Diff(got, want.linear));
                    Assert.Less(err, 0.02f, "tinted in the type's accent: " + got + " vs " + want);
                    bool used = false;
                    foreach (var r in s.bodyRenderers) foreach (var m in r.sharedMaterials) if (m == acc) used = true;
                    Assert.IsTrue(used, "a renderer uses the accent material");
                }
            Assert.Greater(t.Soldiers[0].transform.localScale.x, a.Soldiers[0].transform.localScale.x * 1.25f, "Tank soldiers are clearly bigger");

            // Selecting shows the pips too; deselecting keeps the glyph.
            Army.Select(a);
            yield return null;
            Assert.IsTrue(ba.Shown && ba.GlyphShown);
            Army.Select(null);
            yield return null;
            Assert.IsFalse(ba.Shown);
            Assert.IsTrue(ba.GlyphShown);
        }

        [UnityTest]
        public IEnumerator BackupBadgeReadsBackupAndFits()
        {
            yield return Setup();
            var label = Hud.BackupLabel;
            Assert.IsNotNull(label);
            Assert.AreEqual("BACKUP", label.text);
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate();
            float width = label.rectTransform.rect.width;
            Debug.Log($"[Test] backup label: text width {label.textBounds.size.x:0.0} in {width:0.0}, font {label.fontSize:0.0}");
            Assert.LessOrEqual(label.textBounds.size.x, width + 0.5f, "the label fits inside the badge");
            Assert.GreaterOrEqual(label.fontSize, 16f, "and stays readable");
        }
    }
}
