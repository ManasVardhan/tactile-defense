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
    /// <summary>Level 1 tutorial: the one-time hints (triggers, queue, display time).</summary>
    public partial class TutorialOverlay
    {
        // ------------------------------------------------------------------ hints

        void Enqueue(string id)
        {
            if (!active || IsSeen(id) || hint == id || queue.Contains(id)) return;
            queue.Add(id);
        }

        void OnUnitKilled(Team team, TargetKind kind, Vector3 pos)
        {
            if (team != Team.Player || IsSeen(TutorialSteps.HintPips) || army == null) return;
            // Only when the soldier's unit still stands, so there are dots to look at.
            var u = NearestUnit(pos);
            if (u == null || (u.Centroid - pos).magnitude > 3f) return;
            hintUnit = u;
            hintWorld = pos;
            Enqueue(TutorialSteps.HintPips);
        }

        void OnRejected(HexCoord hex)
        {
            if (IsSeen(TutorialSteps.HintReject)) return;
            var g = army.Grid;
            hintWorld = g.InBounds(hex) ? g.Center(hex) : (BattleCamera.Instance != null ? BattleCamera.Instance.Focus : Vector3.zero);
            Enqueue(TutorialSteps.HintReject);
        }

        void OnPointCaptured(int i) { if (i == 0) Enqueue(TutorialSteps.HintGoal); }

        void OnSquadSpawned(EnemySquad s) { if (s != null) squads.Add(s); }

        PlayerUnit NearestUnit(Vector3 p)
        {
            PlayerUnit best = null;
            float bd = float.MaxValue;
            var u = army.Units;
            for (int i = 0; i < u.Count; i++)
            {
                float d = (u[i].Centroid - p).sqrMagnitude;
                if (d < bd) { bd = d; best = u[i]; }
            }
            return best;
        }

        void PollHints(float dt)
        {
            if (!IsSeen(TutorialSteps.HintContested) && bm != null && bm.capturePoints != null)
                for (int i = 0; i < bm.capturePoints.Length; i++)
                {
                    var cp = bm.capturePoints[i];
                    if (cp == null || !cp.ShowsContested) continue;
                    hintWorld = cp.transform.position;
                    Enqueue(TutorialSteps.HintContested);
                    break;
                }
            if (!IsSeen(TutorialSteps.HintBackup) && bm != null && bm.Meter != null && bm.Meter.Ready && (hud.Backup == null || !hud.Backup.Visible))
                Enqueue(TutorialSteps.HintBackup);
            bubbleCheck -= dt;
            if (bubbleCheck > 0f || IsSeen(TutorialSteps.HintBubble)) return;
            bubbleCheck = 0.25f;
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                if (s == null || s.Bubble == null || !s.Bubble.CanTap) continue;
                Vector3 vp = cam.WorldToViewportPoint(s.Bubble.transform.position);
                if (vp.z <= 0f || vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) continue;
                hintFollow = s.Bubble.transform;
                Enqueue(TutorialSteps.HintBubble);
                return;
            }
        }

        void UpdateHint(float dt)
        {
            if (hint != null)
            {
                hintLeft -= dt;
                bool stale = (hint == TutorialSteps.HintBubble && (hintFollow == null || !hintFollow.gameObject.activeInHierarchy))
                             || (hint == TutorialSteps.HintBackup && hud.Backup != null && hud.Backup.Visible) // the prompt says it louder
                             || (hint == TutorialSteps.HintPips && (hintUnit == null || hintUnit.IsDestroyed));
                // The contested hint stays its full time: it explains what the flashing ring meant.
                if (hintLeft > 0f && !stale) return;
                hint = null;
                tip.Hide();
            }
            if (queue.Count == 0) return;
            hint = queue[0];
            queue.RemoveAt(0);
            hintLeft = TutorialSteps.HintSeconds;
            MarkSeen(hint);
            tip.Show("TIP", TutorialSteps.Copy(hint));
        }
    }
}
