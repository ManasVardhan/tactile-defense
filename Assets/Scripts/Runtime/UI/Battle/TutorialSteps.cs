using System.Collections.Generic;
using TactileWars.Army;
using TactileWars.Battle;
using TactileWars.Combat;
using TactileWars.Core;
using TactileWars.Save;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>
    /// What the Level 1 tutorial says and when (design spec 6.2): the ids saved in PlayerProfile.tutorialSeen,
    /// the copy of the eight guided steps and five one-time hints, and the world checks that start and end steps 6
    /// to 8 and pick the glowing hexes. TutorialOverlay owns the order and the presentation.
    /// </summary>
    public static class TutorialSteps
    {
        public const string BaseId = "b01";

        // Guided steps, in order.
        public const string Pick = "pick", Deploy = "deploy", Unit = "unit", Move = "move", Pan = "pan", Advance = "advance", Tank = "tank", Capture = "capture";
        // One-time hints.
        public const string HintPips = "hint_pips", HintBubble = "hint_bubble", HintReject = "hint_reject", HintBackup = "hint_backup", HintGoal = "hint_goal", HintContested = "hint_contested";

        public static readonly string[] Guided = { Pick, Deploy, Unit, Move, Pan, Advance, Tank, Capture };
        public static readonly string[] Hints = { HintPips, HintBubble, HintReject, HintBackup, HintGoal, HintContested };
        public static readonly string[] All = { Pick, Deploy, Unit, Move, Pan, Advance, Tank, Capture, HintPips, HintBubble, HintReject, HintBackup, HintGoal, HintContested };

        /// <summary>Step 7 (tank) starts, and step 6 (advance) ends, when an enemy soldier is this close to any player
        /// soldier.</summary>
        public const float EnemyNearDistance = 12f;
        /// <summary>Step 6 suggests a hex this many steps along the map's path from the foremost unit.</summary>
        public const int AdvanceSuggestSteps = 5;
        /// <summary>Step 6 is done once a unit stands this many hex rows further along the map's path than the foremost unit did
        /// when the step started.</summary>
        public const int AdvanceRows = 4;
        /// <summary>Step 7 suggests this many hexes for the Tank when it is not deployed yet.</summary>
        public const int TankSuggestCount = 2;
        /// <summary>Step 8 starts when no living enemy soldier is within the first point's radius plus this.</summary>
        public const float CaptureClearMargin = 3f;
        /// <summary>Step 3 has nothing to do: it moves on after this many seconds (or once a unit is selected).</summary>
        public const float ReadSeconds = 4.5f;
        public const float HintSeconds = 5f;
        /// <summary>Step 5 is done once the view moved this far (world units).</summary>
        public const float PanDistance = 1.5f;
        /// <summary>Step 7 with the Tank already on the field is information only: it is marked seen after this many
        /// seconds (or at once when the Tank is moved or another Tank is deployed).</summary>
        public const float TankInfoSeconds = 5f;
        /// <summary>Step 4 suggests a hex this many steps along the map's path from the unit.</summary>
        public const int MoveSuggestSteps = 4;

        public static string Copy(string id) => id switch
        {
            Pick => "This is your squad. Click the Attacker.",
            Deploy => "Now click a glowing spot to drop them in.",
            Unit => "A unit is 3 soldiers. They shoot on their own when an enemy is close.",
            Move => "Click your unit, then click where it should go.",
            Pan => "Drag the ground to look around.",
            Advance => "Now move your units along the track toward the enemy.",
            Tank => "Enemies ahead. Tanks are tough and enemies shoot them first. Put your Tank in front.",
            Capture => "Stand on the circle to capture it.",
            HintPips => "The dots over a unit show how many soldiers it has left.",
            HintBubble => "Pop the bubble to knock out the whole enemy squad.",
            HintReject => "You can't go there. Pick an open spot.",
            HintBackup => "Backup is ready. Click it to get a lost unit back.",
            HintGoal => "Capture both circles to win. You lose if all your units are gone.",
            HintContested => "Enemies are on the circle. Clear them to capture.",
            _ => "",
        };

        /// <summary>1 based position among the guided steps, 0 for a hint.</summary>
        public static int Number(string id) => System.Array.IndexOf(Guided, id) + 1;

        public static bool IsSeen(PlayerProfile p, string id) => p != null && p.tutorialSeen != null && p.tutorialSeen.Contains(id);

        /// <summary>Profiles saved before the advance step existed: a player who already got past it (saw the Tank or
        /// capture step, or skipped) is not sent back to it. Returns true when the profile changed.</summary>
        public static bool Upgrade(PlayerProfile p)
        {
            if (p == null || p.tutorialSeen == null || p.tutorialSeen.Contains(Advance)) return false;
            if (!p.tutorialSeen.Contains(Tank) && !p.tutorialSeen.Contains(Capture)) return false;
            p.tutorialSeen.Add(Advance);
            return true;
        }

        public static bool AllSeen(PlayerProfile p)
        {
            for (int i = 0; i < All.Length; i++) if (!IsSeen(p, All[i])) return false;
            return true;
        }

        /// <summary>Step 7 trigger: an enemy soldier within EnemyNearDistance of any living player soldier.</summary>
        public static bool EnemyNearArmy(PlayerArmy army)
        {
            if (army == null) return false;
            var units = army.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var list = units[i].Soldiers;
                for (int k = 0; k < list.Count; k++)
                    if (list[k] != null && list[k].IsAlive && UnitRegistry.CountWithin(Team.Enemy, list[k].transform.position, EnemyNearDistance) > 0) return true;
            }
            return false;
        }

        /// <summary>Step 8 trigger: the first capture point is open and no enemy soldier stands within its radius plus
        /// CaptureClearMargin.</summary>
        public static bool FirstPointClear(BattleManager bm)
        {
            var cp = FirstPoint(bm);
            if (cp == null || !cp.Active || cp.Captured) return false;
            float r = cp.Radius + CaptureClearMargin;
            return UnitRegistry.CountWithin(Team.Enemy, cp.transform.position, r) == 0;
        }

        /// <summary>Step 8 also starts once units are already capturing the first point (progress rising, not
        /// contested), even with an enemy lingering just past the clear margin.</summary>
        public static bool FirstPointCapturing(BattleManager bm)
        {
            var cp = FirstPoint(bm);
            return cp != null && cp.Active && !cp.Captured && cp.Progress > 0f && !cp.Contested && cp.PlayersInside > 0;
        }

        /// <summary>Step 8 stays up at least this long (it completes at once when units already stand on the circle,
        /// and a card that flashes for one frame is never read).</summary>
        public const float CaptureMinSeconds = 3f;

        public static CapturePoint FirstPoint(BattleManager bm) =>
            bm != null && bm.capturePoints != null && bm.capturePoints.Length > 0 ? bm.capturePoints[0] : null;

        /// <summary>Step 2: up to 'count' deployable hexes nearest the spawn point whose centers are on screen and
        /// clear of the HUD.</summary>
        public static void DeploySuggestions(PlayerArmy army, int entry, Camera cam, int count, List<HexCoord> into)
        {
            into.Clear();
            var g = army.Grid;
            var best = new List<KeyValuePair<float, HexCoord>>();
            foreach (var c in g.Cells)
            {
                if (!army.CanDeploy(entry, c) || !Usable(cam, g.Center(c))) continue;
                best.Add(new KeyValuePair<float, HexCoord>((g.Center(c) - army.spawnPoint).sqrMagnitude, c));
            }
            best.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < best.Count && into.Count < count; i++) into.Add(best[i].Value);
        }

        /// <summary>Step 4: a free hex MoveSuggestSteps from the unit, reachable, as far along the map's path as
        /// possible and then nearest the line ahead of the unit; on screen and clear of the HUD when one is.</summary>
        public static bool MoveSuggestion(PlayerArmy army, PlayerUnit unit, Camera cam, out HexCoord hex) =>
            LaneSuggestion(army, unit, MoveSuggestSteps, cam, out hex);

        /// <summary>The unit whose hex (where it stands or is heading) is furthest along the map's path (MapPath), null
        /// when none.</summary>
        public static PlayerUnit Foremost(PlayerArmy army)
        {
            PlayerUnit best = null;
            if (army == null) return null;
            var g = army.Grid;
            var u = army.Units;
            for (int i = 0; i < u.Count; i++)
                if (best == null || MapPath.Progress(g.Center(u[i].Hex)) > MapPath.Progress(g.Center(best.Hex))) best = u[i];
            return best;
        }

        /// <summary>A free hex 'steps' from the unit, reachable, as far along the map's path (MapPath) as possible and
        /// then nearest the line ahead of the unit; on screen and clear of the HUD when one is. False when none.</summary>
        public static bool LaneSuggestion(PlayerArmy army, PlayerUnit unit, int steps, Camera cam, out HexCoord hex)
        {
            hex = default;
            if (army == null || unit == null) return false;
            var g = army.Grid;
            var path = new List<Vector3>();
            Vector3 from = g.Center(unit.Hex);
            float fromProgress = MapPath.Progress(from);
            Vector3 ahead = MapPath.Forward(from);
            float bestScore = float.MinValue;
            bool found = false;
            foreach (var c in g.Cells)
            {
                if (HexGrid.Distance(unit.Hex, c) != steps || !g.IsFree(c)) continue;
                Vector3 p = g.Center(c);
                if (UnitRegistry.CountWithin(Team.Enemy, p, g.OuterRadius * 2f) > 0) continue;
                float dz = MapPath.Progress(p) - fromProgress;
                Vector3 off = p - from; off.y = 0f;
                float side = Mathf.Abs(off.x * ahead.z - off.z * ahead.x); // distance from the line ahead of the unit
                float score = dz - 0.25f * side + (dz > 0f && Usable(cam, p) ? 1000f : 0f);
                if (score <= bestScore || !army.Nav.FindPath(from, p, path)) continue;
                bestScore = score;
                hex = c;
                found = true;
            }
            return found;
        }

        /// <summary>Step 7 with the Tank not deployed: up to 'count' deployable hexes for it between the foremost unit and
        /// the nearest enemy soldier, a little in front of that unit (nearest a spot 2.5 hexes toward the enemy).</summary>
        public static void TankSuggestions(PlayerArmy army, int entry, Camera cam, int count, List<HexCoord> into)
        {
            into.Clear();
            var front = Foremost(army);
            if (army == null || entry < 0 || front == null) return;
            Vector3 from = front.Centroid; from.y = 0f;
            Health enemy = null;
            float bestD = float.MaxValue;
            foreach (var h in UnitRegistry.Of(Team.Enemy))
            {
                if (h == null || !h.IsAlive || h.kind == TargetKind.Mine) continue;
                float d = (h.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; enemy = h; }
            }
            var g = army.Grid;
            Vector3 dir = MapPath.Forward(from);
            float dist = 12f;
            if (enemy != null)
            {
                Vector3 d = enemy.transform.position - from; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) { dist = d.magnitude; dir = d / dist; }
            }
            Vector3 want = from + dir * Mathf.Min(g.OuterRadius * 2.5f * 1.5f, dist * 0.5f);
            var best = new List<KeyValuePair<float, HexCoord>>();
            foreach (var c in g.Cells)
            {
                if (!army.CanDeploy(entry, c)) continue;
                Vector3 p = g.Center(c);
                float along = Vector3.Dot(p - from, dir);
                if (along < g.OuterRadius || along > dist - 3f) continue; // in front of the unit, short of the enemy
                float score = (p - want).sqrMagnitude + (Usable(cam, p) ? 0f : 1000f);
                best.Add(new KeyValuePair<float, HexCoord>(score, c));
            }
            best.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < best.Count && into.Count < count; i++) into.Add(best[i].Value);
        }

        /// <summary>Step 8: the free hex of the first point's pad nearest its center (a pointer on the pad itself).</summary>
        public static bool CaptureSuggestion(PlayerArmy army, BattleManager bm, out HexCoord hex)
        {
            hex = default;
            var cp = FirstPoint(bm);
            if (army == null || cp == null) return false;
            var g = army.Grid;
            float best = float.MaxValue;
            foreach (var c in g.Cells)
            {
                if (!g.IsFree(c) || !army.IsPadHex(c, cp)) continue;
                Vector3 d = g.Center(c) - cp.transform.position; d.y = 0f;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; hex = c; }
            }
            return best < float.MaxValue;
        }

        /// <summary>On screen with a margin, and not under a HUD element.</summary>
        static bool Usable(Camera cam, Vector3 world)
        {
            if (cam == null) return true;
            Vector3 s = cam.WorldToScreenPoint(world);
            float w = cam.pixelWidth, h = cam.pixelHeight;
            if (s.z <= 0f || s.x < w * 0.12f || s.x > w * 0.88f || s.y < h * 0.22f || s.y > h * 0.75f) return false;
            return !PointerInput.IsOverUI(new Vector2(s.x, s.y));
        }
    }
}
