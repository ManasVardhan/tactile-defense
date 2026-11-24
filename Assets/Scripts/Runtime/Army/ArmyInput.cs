using TactileWars.Battle;
using TactileWars.CameraRig;
using TactileWars.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TactileWars.Army
{
    /// <summary>
    /// Pointer controls for unit battles. A click (press and release within tapMaxDragPixels, press not over UI, also
    /// when both land in the same frame) resolves in order: a tappable pickup (bubble), a deploy of the armed entry,
    /// selecting a friendly unit (by its soldiers or its badge; clicking the selected unit keeps it selected), a move
    /// order for the selected unit.
    /// A press that moves farther pans the camera instead. Escape disarms, or deselects when nothing is armed. Ignored while the army's input is off or the battle is
    /// over. Lives under the PlayerArmy object, so it is inactive in defense battles. No rendering here.
    /// </summary>
    public class ArmyInput : MonoBehaviour
    {
        public static ArmyInput Instance { get; private set; }

        public PlayerArmy army;
        public BattleCamera cameraRig;
        public Camera cam;
        [Tooltip("A press that moves farther than this many pixels pans instead of clicking.")] public float tapMaxDragPixels = 12f;

        const float SoldierPickRadius = 0.6f;
        /// <summary>A soldier's drawn height (feet to helmet top) and half width at size 1, before Soldier.SizeScale
        /// and the type's modelScale (both carried by the soldier's scale or applied here).</summary>
        public const float SoldierBodyHeight = 1.0f, SoldierBodyHalfWidth = 0.35f;
        /// <summary>Screen pixels added around each soldier's drawn body when picking.</summary>
        public const float BodyPickPadPixels = 6f;

        /// <summary>Screen point to the unit whose on-screen badge covers it (null when none). Installed by the HUD's
        /// badge layer; badges are not raycast targets, so clicks on them reach this input.</summary>
        public static System.Func<Vector2, PlayerUnit> BadgeHitTest;

        /// <summary>Squad entry index waiting to be deployed by the next map click, -1 when none.</summary>
        public int ArmedEntry { get; private set; } = -1;
        /// <summary>True while an entry is armed or a unit is selected and the pointer is over the ground (not the HUD).</summary>
        public bool HasHover { get; private set; }
        /// <summary>True when the hovered ground point lies on the field (Hover is then its spot).</summary>
        public bool HoverOnMap { get; private set; }
        /// <summary>The spot under the pointer (valid while HoverOnMap).</summary>
        public HexCoord Hover { get; private set; }
        /// <summary>The ground point under the pointer (valid while HasHover).</summary>
        public Vector3 HoverWorld { get; private set; }
        /// <summary>The friendly unit a click would select right now (pointer over its drawn soldiers, its badge, or
        /// its ground), while a unit is selected and nothing is armed; null otherwise.</summary>
        public PlayerUnit HoverUnit { get; private set; }

        bool pressing, pressOverUI, panning;
        Vector2 pressPos, lastPos;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        bool InputOn => army != null && army.InputEnabled && (BattleManager.Instance == null || !BattleManager.Instance.Over);

        Camera Cam => cam != null ? cam : (cameraRig != null ? cameraRig.Cam : Camera.main);

        /// <summary>Arms a ready entry so the next map click deploys it. Clears the selection.</summary>
        public void Arm(int entryIndex)
        {
            if (!InputOn) return;
            var squad = army.Squad;
            if (entryIndex < 0 || entryIndex >= squad.Count || squad[entryIndex].state != EntryState.Ready) return;
            ArmedEntry = entryIndex;
            army.Select(null);
        }

        public void Disarm() { ArmedEntry = -1; }

        void Update()
        {
            if (!InputOn)
            {
                // Paused keeps the armed entry; a finished battle drops it.
                if (BattleManager.Instance != null && BattleManager.Instance.Over) ArmedEntry = -1;
                pressing = panning = false;
                HasHover = HoverOnMap = false;
                HoverUnit = null;
                return;
            }
            if (ArmedEntry >= 0 && (ArmedEntry >= army.Squad.Count || army.Squad[ArmedEntry].state != EntryState.Ready)) ArmedEntry = -1;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (ArmedEntry >= 0) Disarm();
                else army.Select(null);
            }
#endif
            Vector2 pos = PointerInput.Position;
            UpdateHover(pos);

            if (PointerInput.PressedThisFrame)
            {
                pressing = true;
                panning = false;
                pressOverUI = PointerInput.IsOverUI(pos);
                pressPos = lastPos = pos;
                // A quick click can press and release inside one frame: handle it as a release right away.
                if (PointerInput.IsDown || !PointerInput.ReleasedThisFrame) return;
            }
            if (!pressing) return;
            if (PointerInput.IsDown)
            {
                if (!pressOverUI)
                {
                    if (!panning && (pos - pressPos).magnitude > tapMaxDragPixels)
                    {
                        panning = true;
                        lastPos = pressPos; // the first pan step covers the whole move since the press
                    }
                    if (panning && cameraRig != null && pos != lastPos) cameraRig.Pan(pos - lastPos);
                }
                lastPos = pos;
                return;
            }
            // Released.
            bool click = !pressOverUI && !panning && (pos - pressPos).magnitude <= tapMaxDragPixels;
            pressing = panning = false;
            if (click) Click(pos);
        }

        void UpdateHover(Vector2 screen)
        {
            // Only while the markers show, so the grid is not baked before anything needs it.
            HasHover = HoverOnMap = false;
            HoverUnit = null;
            if (ArmedEntry < 0 && army.Selected == null) return;
            if (PointerInput.IsOverUI(screen)) return; // no marker under HUD buttons
            // Same picking as the click, so the cue and the click cannot disagree.
            if (ArmedEntry < 0) HoverUnit = UnitUnderPointer(screen);
            if (!PointerInput.ScreenToGround(Cam, screen, out Vector3 world)) return;
            HasHover = true;
            HoverWorld = world;
            if (army.Grid.TryWorldToHex(world, out HexCoord h)) { Hover = h; HoverOnMap = true; }
        }

        void Click(Vector2 screen)
        {
            var c = Cam;
            if (c == null) return;
            // 1. Tappable pickups (a flag bearer's bubble) win over everything else.
            if (Physics.Raycast(c.ScreenPointToRay(screen), out RaycastHit hit, 500f, Layers.PickupMask, QueryTriggerInteraction.Collide))
            {
                var tappable = hit.collider.GetComponentInParent<ITappable>();
                if (tappable != null && tappable.CanTap) { tappable.OnTapped(); return; }
            }
            if (!PointerInput.ScreenToGround(c, screen, out Vector3 world)) return;
            var grid = army.Grid;
            bool onMap = grid.TryWorldToHex(world, out HexCoord hex);

            // 2. Deploy the armed entry, or refuse and stay armed.
            if (ArmedEntry >= 0)
            {
                if (onMap && army.CanDeploy(ArmedEntry, hex) && army.Deploy(ArmedEntry, hex) != null) Disarm();
                else army.NotifyRejected(hex);
                return;
            }

            // 3. Select the friendly unit under the pointer. Clicking the selected unit again keeps it selected
            // (players re-click out of habit before the next order); Escape deselects.
            var unit = UnitUnderPointer(screen);
            if (unit != null)
            {
                army.Select(unit);
                return;
            }

            // 4. Move the selected unit; it stays selected whether or not the order is accepted.
            if (army.Selected != null) army.OrderMove(army.Selected, hex);
        }

        /// <summary>The friendly unit a click at this screen point picks, or null. One function for the click and the
        /// hover cue. In order: a living soldier whose drawn body (feet to helmet top, padded) covers the point, the
        /// one nearest the camera when several do; a unit's badge; the unit on the spot under the pointer's ground
        /// point; a soldier within SoldierPickRadius of that ground point (clicks between a unit's soldiers).</summary>
        public PlayerUnit UnitUnderPointer(Vector2 screen)
        {
            var c = Cam;
            if (c == null || army == null) return null;
            PlayerUnit best = null;
            float bestDepth = float.MaxValue;
            var units = army.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var list = units[i].Soldiers;
                for (int k = 0; k < list.Count; k++)
                {
                    var s = list[k];
                    if (s == null || !s.IsAlive) continue;
                    if (BodyCovers(c, s.transform, screen, out float depth) && depth < bestDepth) { bestDepth = depth; best = units[i]; }
                }
            }
            if (best != null) return best;
            if (BadgeHitTest != null)
            {
                var b = BadgeHitTest(screen);
                if (b != null) return b;
            }
            if (!PointerInput.ScreenToGround(c, screen, out Vector3 world)) return null;
            var unit = army.Grid.TryWorldToHex(world, out HexCoord hex) ? army.UnitAt(hex) : null;
            return unit != null ? unit : UnitNear(world);
        }

        /// <summary>True when the soldier's drawn body (an upright box from its feet to its helmet top at its current
        /// size, projected to the screen and padded) covers the point; depth is its feet's distance from the camera.</summary>
        public static bool BodyCovers(Camera c, Transform soldier, Vector2 screen, out float depth)
        {
            float scale = Units.Soldier.SizeScale * soldier.lossyScale.y;
            Vector3 feet = soldier.position;
            Vector3 f = c.WorldToScreenPoint(feet);
            Vector3 t = c.WorldToScreenPoint(feet + Vector3.up * (SoldierBodyHeight * scale));
            depth = f.z;
            if (f.z <= 0f || t.z <= 0f) return false;
            Vector3 side = c.WorldToScreenPoint(feet + c.transform.right * (SoldierBodyHalfWidth * scale));
            float half = Mathf.Abs(side.x - f.x) + BodyPickPadPixels;
            float minX = Mathf.Min(f.x, t.x) - half, maxX = Mathf.Max(f.x, t.x) + half;
            float minY = Mathf.Min(f.y, t.y) - BodyPickPadPixels, maxY = Mathf.Max(f.y, t.y) + BodyPickPadPixels;
            return screen.x >= minX && screen.x <= maxX && screen.y >= minY && screen.y <= maxY;
        }

        /// <summary>The unit with a living soldier within SoldierPickRadius of the ground point (nearest first).</summary>
        PlayerUnit UnitNear(Vector3 world)
        {
            PlayerUnit best = null;
            float bestD = SoldierPickRadius * SoldierPickRadius;
            var units = army.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var list = units[i].Soldiers;
                for (int k = 0; k < list.Count; k++)
                {
                    var s = list[k];
                    if (s == null || !s.IsAlive) continue;
                    Vector3 d = s.transform.position - world;
                    float d2 = d.x * d.x + d.z * d.z;
                    if (d2 <= bestD) { bestD = d2; best = units[i]; }
                }
            }
            return best;
        }
    }
}
