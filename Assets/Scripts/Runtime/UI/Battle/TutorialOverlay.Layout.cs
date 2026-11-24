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
    /// <summary>Level 1 tutorial: where the cards point (HUD elements, units, hexes, the capture circle), what they keep
    /// clear of (every unit, the suggested hexes) and the drag finger.</summary>
    public partial class TutorialOverlay
    {
        /// <summary>Registers every deployed unit and suggested hex with both cards, so they pick a side that leaves
        /// those visible.</summary>
        void CollectAvoid()
        {
            guide.ClearAvoid();
            tip.ClearAvoid();
            var units = army.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].AliveCount > 0) AvoidWorld(units[i].Centroid + Vector3.up * 0.6f, 1.4f);
            for (int i = 0; i < suggest.Count; i++) AvoidWorld(army.Grid.Center(suggest[i]), army.Grid.OuterRadius);
        }

        void AvoidWorld(Vector3 world, float worldRadius)
        {
            if (!ToLocal(world, worldRadius, out Vector2 local, out float r)) return;
            guide.Avoid(local, r);
            tip.Avoid(local, r);
        }

        /// <summary>World point to the safe area's local space, with 'worldRadius' as an on screen radius.</summary>
        bool ToLocal(Vector3 world, float worldRadius, out Vector2 local, out float radius)
        {
            radius = 0f;
            local = default;
            var canvasCam = hud.Root != null ? hud.Root.Camera : cam;
            Vector3 s = cam.WorldToScreenPoint(world);
            Vector3 s2 = cam.WorldToScreenPoint(world + cam.transform.right * worldRadius);
            if (s.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, s, canvasCam, out local)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, s2, canvasCam, out Vector2 edge)) return false;
            radius = Mathf.Max(30f, (edge - local).magnitude);
            return true;
        }

        void PointGuide()
        {
            switch (step)
            {
                case TutorialSteps.Pick:
                    PointAtUI(guide, hud.UnitBar != null ? hud.UnitBar.ButtonFor(UnitTypeId.Attacker) : null);
                    break;
                case TutorialSteps.Deploy:
                    if (suggest.Count > 0)
                    {
                        Vector3 c = Vector3.zero;
                        for (int i = 0; i < suggest.Count; i++) c += army.Grid.Center(suggest[i]);
                        PointAtWorld(guide, c / suggest.Count, 2.2f);
                    }
                    else guide.PlaceAt(new Vector2(0f, 120f));
                    break;
                case TutorialSteps.Unit:
                    PointAtUnit(guide, FirstUnit());
                    break;
                case TutorialSteps.Move:
                    // Preferably below the unit, so the card never hides the suggested spot up the lane.
                    if (army.Selected == null || suggest.Count == 0) PointAtUnit(guide, FirstUnit(), Callout.Side.Below);
                    else PointAtWorld(guide, army.Grid.Center(suggest[0]), army.Grid.OuterRadius);
                    break;
                case TutorialSteps.Pan:
                    guide.PlaceAt(new Vector2(0f, 60f));
                    break;
                case TutorialSteps.Advance:
                    if (suggest.Count > 0) PointAtWorld(guide, army.Grid.Center(suggest[0]), army.Grid.OuterRadius);
                    else guide.PlaceAt(new Vector2(0f, 120f));
                    break;
                case TutorialSteps.Tank:
                    if (TankReady() && hud.UnitBar != null) PointAtUI(guide, hud.UnitBar.ButtonFor(UnitTypeId.Tank));
                    else PointAtUnit(guide, FirstUnit(UnitTypeId.Tank));
                    break;
                case TutorialSteps.Capture:
                    var cp = TutorialSteps.FirstPoint(bm);
                    if (cp != null) PointAtWorld(guide, cp.transform.position, cp.Radius);
                    break;
            }
        }

        void PointTip()
        {
            switch (hint)
            {
                case TutorialSteps.HintPips:
                    if (hintUnit != null && !hintUnit.IsDestroyed) PointAtUnit(tip, hintUnit);
                    else PointAtWorld(tip, hintWorld, 1f);
                    break;
                case TutorialSteps.HintBubble:
                    if (hintFollow != null) PointAtWorld(tip, hintFollow.position, 1.2f);
                    break;
                case TutorialSteps.HintReject:
                    PointAtWorld(tip, hintWorld, army.Grid.OuterRadius);
                    break;
                case TutorialSteps.HintBackup:
                    PointAtUI(tip, hud.PowButton, Callout.Side.Below, 44f); // clear of its caption
                    break;
                case TutorialSteps.HintGoal:
                    PointAtUI(tip, hud.ObjectivesRect, Callout.Side.Below);
                    break;
                case TutorialSteps.HintContested:
                    PointAtWorld(tip, hintWorld, 3.2f);
                    break;
            }
        }

        /// <summary>Points at a HUD element from above or below; 'extra' widens the clearance (captions).</summary>
        void PointAtUI(Callout c, Component target, Callout.Side side = Callout.Side.Auto, float extra = 0f)
        {
            if (target == null) { c.PlaceAt(Vector2.zero); return; }
            var rt = (RectTransform)target.transform;
            Vector2 local = safe.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            c.PointAt(local, rt.rect.height * 0.5f + extra, side, false);
        }

        void PointAtUnit(Callout c, PlayerUnit u, Callout.Side side = Callout.Side.Auto)
        {
            if (u == null) { c.PlaceAt(new Vector2(0f, 120f)); return; }
            PointAtWorld(c, u.Centroid + Vector3.up * 0.8f, 1.6f, side);
        }

        /// <summary>Points at a world spot; 'worldRadius' becomes the on screen size the card keeps clear of.</summary>
        void PointAtWorld(Callout c, Vector3 world, float worldRadius, Callout.Side side = Callout.Side.Auto)
        {
            var canvasCam = hud.Root != null ? hud.Root.Camera : cam;
            Vector3 s = cam.WorldToScreenPoint(world);
            Vector3 s2 = cam.WorldToScreenPoint(world + cam.transform.right * worldRadius);
            if (s.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, s, canvasCam, out Vector2 local)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, s2, canvasCam, out Vector2 edge))
            {
                c.PlaceAt(new Vector2(0f, 120f));
                return;
            }
            Vector2 half = safe.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + 60f, half.x - 60f);
            local.y = Mathf.Clamp(local.y, -half.y + 60f, half.y - 60f);
            c.PointAt(local, Mathf.Max(30f, (edge - local).magnitude), side);
        }

        void AnimateFinger()
        {
            // A finger presses below the card and drags sideways, then lifts and repeats.
            float k = Mathf.Repeat(Time.unscaledTime, 2f) / 2f;
            float u = Mathf.Clamp01((k - 0.15f) / 0.6f);
            float x = Mathf.Lerp(-180f, 180f, UIAnim.InOutSine(u));
            finger.anchoredPosition = new Vector2(x, -120f);
            float press = k < 0.15f ? k / 0.15f : (k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f);
            float sc = Mathf.Lerp(1.15f, 1f, press);
            finger.localScale = new Vector3(sc, sc, 1f);
        }
    }
}
