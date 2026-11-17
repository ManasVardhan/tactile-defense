using System;
using System.Collections.Generic;
using TactileWars.Audio;
using TactileWars.Battle;
using TactileWars.Core;
using TactileWars.Meta;
using TactileWars.Units;
using UnityEngine;

namespace TactileWars.UI
{
    /// <summary>
    /// The base editor scene root (BaseEditor.unity). Shows the player's own base map (MetaDatabase.homeMap) under
    /// the game camera; pan by dragging empty ground, zoom with the overview button or the mouse wheel. Defenses are
    /// placed by dragging a tray card onto the map (or tapping a card, then the ground); a ghost snapped to the 1 unit
    /// square grid shows green when valid and red when not. Tapping a placed defense selects it (range ring, rotate,
    /// move, remove). Wrecks of consumed defenses can be rebought. Edits live in a BaseEditSession and are written
    /// with Save (gold is paid then). All input goes through PointerInput (mouse and touch share one path).
    /// </summary>
    public class BaseEditorRoot : MonoBehaviour
    {
        public UISkin skin;
        public Camera cam;
        public MetaDatabase database;
        public TeamVisuals visuals;
        public BattlePrefabs prefabs;
        public Renderer ground;
        public Transform worldRoot;
        public GameObject capturePointPrefab;
        public Mesh quad;
        [Tooltip("Transparent unlit material (TW/Particle) for ghosts and wrecks.")] public Material ghostMaterial;
        [Tooltip("Tiled grid lines over the placeable zone.")] public Material gridMaterial;
        [Tooltip("Soft disc for footprints and no build zones.")] public Material discMaterial;
        [Tooltip("Ring arc for ranges (TW/RingArc).")] public Material ringMaterial;

        [Header("Camera (estimates, matches the battle view)")]
        public float elevation = 35f;
        public float distance = 60f;
        public float normalZoom = 13.5f;
        public float overviewZoom = 33f;
        public float minZoom = 10f;

        public static BaseEditorRoot Instance { get; private set; }
        public BaseEditSession Session { get; private set; }
        public BaseEditorHud Hud { get; private set; }
        public MapLayoutData Map { get; private set; }
        public TeamColorId Team { get; private set; }
        public PlacementError LastError { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public int SelectedWreck { get; private set; } = -1;
        public string ArmedId { get; private set; }
        public bool Overview { get; private set; }
        public bool MoveMode { get; private set; }
        public string GhostId { get; private set; }
        public bool GhostValid { get; private set; }
        public Vector3 Focus => focus;
        public event Action Changed;

        readonly List<GameObject> views = new List<GameObject>();
        readonly List<GameObject> wreckViews = new List<GameObject>();
        GameObject ghost;
        Transform defensesRoot;
        Renderer ghostDisc, selectDisc, rangeRing;
        MaterialPropertyBlock mpb;
        Material ringMat;
        Vector3 focus;
        float zoom;
        float targetZoom;

        // Pointer state.
        enum Gesture { None, Pan, Card, MoveDrag, Tap }
        Gesture gesture;
        Vector2 pressScreen;
        Vector3 pressWorld;
        Vector3 panStartFocus;
        string pressCard;
        int pressIndex = -1;
        bool dragged;

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        public static readonly Color ValidColor = new Color(0.45f, 1f, 0.4f, 0.72f);
        static readonly Color InvalidColor = new Color(1f, 0.25f, 0.2f, 0.75f);
        static readonly Color WreckColor = new Color(0.45f, 0.45f, 0.5f, 0.5f);

        void Awake() { Instance = this; mpb = new MaterialPropertyBlock(); }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ringMat != null) Destroy(ringMat);
        }

        void Start()
        {
            if (database != null) MetaGame.Db = database;
            var db = MetaGame.Db;
            var p = MetaGame.Profile;
            Team = p.team;
            UIFactory.PlayerTeam = Team;
            if (cam == null) cam = Camera.main;
            GameSettings.Apply(p.settings, cam);
            Map = db != null ? db.homeMap : null;
            if (worldRoot == null) worldRoot = new GameObject("World").transform;
            BuildWorld();
            Session = MetaGame.BeginBaseEdit();
            defensesRoot = new GameObject("Defenses").transform;
            defensesRoot.SetParent(worldRoot, false);
            RebuildViews();
            focus = new Vector3(0f, 0f, 34f);
            zoom = targetZoom = normalZoom;
            ApplyCamera();
            Hud = BaseEditorHud.Create(this);
            Refresh();
        }

        // ------------------------------------------------------------------ world

        void BuildWorld()
        {
            if (Map == null) return;
            MapApplier.SpawnProps(Map, worldRoot);
            if (ground != null) MapApplier.ApplyGround(Map, ground);
            if (capturePointPrefab != null)
                for (int i = 0; i < Map.capturePoints.Length; i++)
                {
                    var cp = Instantiate(capturePointPrefab, Map.capturePoints[i], Quaternion.identity, worldRoot);
                    cp.name = "CapturePoint_" + (i + 1);
                    foreach (var c in cp.GetComponentsInChildren<MonoBehaviour>(true))
                        if (c is CapturePoint) c.enabled = false;
                }
            // Everything with a team slot here belongs to the player.
            var mats = visuals != null ? visuals.Get(Team) : null;
            foreach (var t in worldRoot.GetComponentsInChildren<TeamTint>(true)) { t.applyOnStart = false; if (mats != null) t.Apply(mats); }

            var z = Map.placeable;
            // Grid over the placeable zone.
            if (gridMaterial != null && quad != null)
            {
                var gm = new Material(gridMaterial);
                gm.mainTextureScale = new Vector2((z.z - z.x), (z.w - z.y));
                Flat("Grid", new Vector3((z.x + z.z) * 0.5f, 0.025f, (z.y + z.w) * 0.5f), new Vector2(z.z - z.x, z.w - z.y), gm, new Color(1f, 1f, 1f, 0.32f));
            }
            // Attacker start zone and capture pads: red no build areas.
            if (discMaterial != null && quad != null)
            {
                float zmin = Map.mapMin.y - 1f;
                var zoneMat = ghostMaterial != null ? ghostMaterial : discMaterial;
                Flat("AttackerZone", new Vector3(0f, 0.03f, (zmin + Map.attackerZoneMaxZ) * 0.5f), new Vector2(Map.mapMax.x - Map.mapMin.x, Map.attackerZoneMaxZ - zmin), zoneMat, new Color(0.95f, 0.3f, 0.22f, 0.16f));
                foreach (var c in Map.capturePoints)
                    Flat("PadZone", c + Vector3.up * 0.02f, Vector2.one * (Map.padClearance * 2.3f), discMaterial, new Color(0.95f, 0.3f, 0.22f, 0.35f));
            }
            ringMat = ringMaterial != null ? new Material(ringMaterial) : null;
            ghostDisc = MakeDisc("GhostDisc");
            selectDisc = MakeDisc("SelectDisc");
            if (ringMat != null && quad != null)
            {
                rangeRing = Flat("RangeRing", Vector3.zero, Vector2.one, ringMat, Color.white);
                rangeRing.gameObject.SetActive(false);
            }
        }

        Renderer MakeDisc(string name)
        {
            if (discMaterial == null || quad == null) return null;
            var r = Flat(name, Vector3.zero, Vector2.one, discMaterial, ValidColor);
            r.gameObject.SetActive(false);
            return r;
        }

        Renderer Flat(string name, Vector3 pos, Vector2 size, Material m, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(worldRoot, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, color);
            return r;
        }

        /// <summary>Tints every renderer under 'go' through property blocks (ghost copies for captures).</summary>
        public void ColorRenderers(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) SetColor(r, c);
        }

        void SetColor(Renderer r, Color c, float fill = 1f)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            mpb.SetFloat(FillId, fill);
            mpb.SetFloat(AlphaId, 1f);
            r.SetPropertyBlock(mpb);
        }

        void RebuildViews()
        {
            foreach (var v in views) if (v != null) Destroy(v);
            foreach (var v in wreckViews) if (v != null) Destroy(v);
            views.Clear();
            wreckViews.Clear();
            var cat = Session.Catalog;
            for (int i = 0; i < Session.Placements.Count; i++)
            {
                var d = Session.Placements[i];
                var v = DefenseDisplay.Make(d.defenseId, cat, prefabs, visuals, Team, defensesRoot);
                v.transform.SetPositionAndRotation(new Vector3(d.x, 0f, d.z), Quaternion.Euler(0f, d.rotation, 0f));
                views.Add(v);
            }
            for (int i = 0; i < Session.Destroyed.Count; i++)
            {
                var d = Session.Destroyed[i];
                var v = DefenseDisplay.Make(d.defenseId, cat, prefabs, visuals, Team, defensesRoot);
                v.transform.SetPositionAndRotation(new Vector3(d.x, 0f, d.z), Quaternion.Euler(0f, d.rotation, 0f));
                v.transform.localScale = new Vector3(1f, 0.35f, 1f);   // flattened rubble look
                if (ghostMaterial != null)
                {
                    DefenseDisplay.SetAllMaterials(v, ghostMaterial);
                    foreach (var r in v.GetComponentsInChildren<Renderer>(true)) SetColor(r, WreckColor);
                }
                wreckViews.Add(v);
            }
        }

        public Vector3 WreckPosition(int i) => i >= 0 && i < Session.Destroyed.Count ? new Vector3(Session.Destroyed[i].x, 0f, Session.Destroyed[i].z) : Vector3.zero;

        // ------------------------------------------------------------------ camera

        void ApplyCamera()
        {
            if (cam == null) return;
            var rot = Quaternion.Euler(elevation, 0f, 0f);
            Vector2 fmin = Map != null ? new Vector2(Map.mapMin.x + 8f, Map.mapMin.y + 6f) : new Vector2(-10f, 5f);
            Vector2 fmax = Map != null ? new Vector2(Map.mapMax.x - 8f, Map.mapMax.y - 8f) : new Vector2(10f, 100f);
            if (Overview) { fmin = fmax = new Vector2(0f, Map != null ? (Map.mapMin.y + Map.mapMax.y) * 0.5f + 1f : 55f); }
            focus.x = Mathf.Clamp(focus.x, fmin.x, fmax.x);
            focus.z = Mathf.Clamp(focus.z, fmin.y, fmax.y);
            focus.y = 0f;
            cam.orthographic = true;
            cam.orthographicSize = zoom;
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }

        public void SetOverview(bool on)
        {
            Overview = on;
            targetZoom = on ? overviewZoom : normalZoom;
            if (!on) focus.z = Mathf.Clamp(focus.z, 20f, 90f);
            Refresh();
        }

        public void ToggleOverview() => SetOverview(!Overview);

        public void FocusOn(Vector3 p, bool instant = true)
        {
            focus = p;
            if (instant) ApplyCamera();
        }

        public void SnapZoom() { zoom = targetZoom; ApplyCamera(); }

        // ------------------------------------------------------------------ editing API (UI and tests)

        public PlacementError TryPlace(string id, Vector3 world)
        {
            var err = Session.Place(id, world, MetaGame.BaseConfig != null ? MetaGame.BaseConfig.defaultYaw : 180f);
            LastError = err;
            if (err != PlacementError.None) { Feedback(err); return err; }
            AudioManager.Play(SoundId.UiPurchase, world);
            RebuildViews();
            Select(Session.Placements.Count - 1);
            if (!ShiftArmed()) ArmedId = null;
            Refresh();
            return err;
        }

        bool ShiftArmed() => false;

        /// <summary>Shows the placement ghost for 'id' at 'world' (snapped). Returns the validation result.</summary>
        public PlacementError ShowGhost(string id, Vector3 world, int ignore = -1, bool adding = true)
        {
            Vector3 p = Session.Snap(world);
            var err = Session.Check(id, p, ignore, adding);
            GhostValid = err == PlacementError.None;
            if (ghost == null || GhostId != id)
            {
                if (ghost != null) Destroy(ghost);
                ghost = DefenseDisplay.Make(id, Session.Catalog, prefabs, visuals, Team, worldRoot);
                if (ghostMaterial != null) DefenseDisplay.SetAllMaterials(ghost, ghostMaterial);
                GhostId = id;
            }
            ghost.SetActive(true);
            float yaw = ignore >= 0 && ignore < Session.Placements.Count ? Session.Placements[ignore].rotation : (MetaGame.BaseConfig != null ? MetaGame.BaseConfig.defaultYaw : 180f);
            ghost.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            Color c = GhostValid ? ValidColor : InvalidColor;
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true)) SetColor(r, c);
            float fp = Session.TryEntry(id, out var e) ? e.footprint : 1f;
            if (ghostDisc != null)
            {
                ghostDisc.gameObject.SetActive(true);
                ghostDisc.transform.position = p + Vector3.up * 0.04f;
                ghostDisc.transform.localScale = new Vector3(fp * 2.4f, fp * 2.4f, 1f);
                SetColor(ghostDisc, c);
            }
            ShowRange(p, e.range, GhostValid ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.4f, 0.3f, 0.9f));
            LastError = err;
            return err;
        }

        public void HideGhost()
        {
            if (ghost != null) ghost.SetActive(false);
            if (ghostDisc != null) ghostDisc.gameObject.SetActive(false);
            GhostId = ghost != null ? GhostId : null;
            UpdateSelectionVisuals();
        }

        void ShowRange(Vector3 p, float range, Color c)
        {
            if (rangeRing == null) return;
            bool on = range > 0.1f;
            rangeRing.gameObject.SetActive(on);
            if (!on) return;
            rangeRing.transform.position = p + Vector3.up * 0.05f;
            float s = range * 2.06f;
            rangeRing.transform.localScale = new Vector3(s, s, 1f);
            SetColor(rangeRing, c);
        }

        public void Select(int index)
        {
            SelectedIndex = index >= 0 && index < Session.Placements.Count ? index : -1;
            SelectedWreck = -1;
            MoveMode = false;
            UpdateSelectionVisuals();
            Refresh();
        }

        public void SelectWreck(int index)
        {
            SelectedWreck = index >= 0 && index < Session.Destroyed.Count ? index : -1;
            SelectedIndex = -1;
            MoveMode = false;
            UpdateSelectionVisuals();
            Refresh();
        }

        public void Deselect()
        {
            SelectedIndex = -1;
            SelectedWreck = -1;
            MoveMode = false;
            UpdateSelectionVisuals();
            Refresh();
        }

        void UpdateSelectionVisuals()
        {
            bool sel = SelectedIndex >= 0;
            bool wreck = SelectedWreck >= 0;
            if (selectDisc != null) selectDisc.gameObject.SetActive(sel || wreck);
            if (!sel && !wreck) { if (rangeRing != null && (ghost == null || !ghost.activeSelf)) rangeRing.gameObject.SetActive(false); return; }
            var d = sel ? Session.Placements[SelectedIndex] : Session.Destroyed[SelectedWreck];
            Vector3 p = new Vector3(d.x, 0f, d.z);
            float fp = Session.TryEntry(d.defenseId, out var e) ? e.footprint : 1f;
            if (selectDisc != null)
            {
                selectDisc.transform.position = p + Vector3.up * 0.04f;
                selectDisc.transform.localScale = new Vector3(fp * 2.6f, fp * 2.6f, 1f);
                Color tc = UIColors.Team(Team); tc.a = 0.55f;
                SetColor(selectDisc, wreck ? new Color(0.6f, 0.6f, 0.65f, 0.5f) : tc);
            }
            if (sel && (ghost == null || !ghost.activeSelf)) ShowRange(p, e.range, UIColors.Lighter(UIColors.Team(Team), 0.35f));
            else if (rangeRing != null && (ghost == null || !ghost.activeSelf)) rangeRing.gameObject.SetActive(false);
        }

        public void RotateSelected()
        {
            if (SelectedIndex < 0) return;
            Session.Rotate(SelectedIndex, MetaGame.BaseConfig != null ? MetaGame.BaseConfig.rotateStep : 45f);
            var d = Session.Placements[SelectedIndex];
            if (SelectedIndex < views.Count && views[SelectedIndex] != null) views[SelectedIndex].transform.rotation = Quaternion.Euler(0f, d.rotation, 0f);
            AudioManager.Play(SoundId.UiClick, Vector3.zero);
            Refresh();
        }

        public void BeginMoveSelected()
        {
            if (SelectedIndex < 0) return;
            MoveMode = !MoveMode;
            if (MoveMode) Hud?.Toast("Tap the new spot (or drag the defense)", UIIcon.Move, UIColors.WithAlpha(UIColors.Slate, 0.95f));
            Refresh();
        }

        public PlacementError MoveSelectedTo(Vector3 world)
        {
            if (SelectedIndex < 0) return PlacementError.Unknown;
            var err = Session.Move(SelectedIndex, world);
            LastError = err;
            MoveMode = false;
            if (err != PlacementError.None) { Feedback(err); RefreshViewPositions(); return err; }
            AudioManager.Play(SoundId.FormationSet, world);
            RefreshViewPositions();
            UpdateSelectionVisuals();
            Refresh();
            return err;
        }

        void RefreshViewPositions()
        {
            for (int i = 0; i < views.Count && i < Session.Placements.Count; i++)
            {
                var d = Session.Placements[i];
                if (views[i] != null) views[i].transform.SetPositionAndRotation(new Vector3(d.x, 0f, d.z), Quaternion.Euler(0f, d.rotation, 0f));
            }
        }

        public void RemoveSelected()
        {
            if (SelectedIndex < 0) return;
            string id = Session.Placements[SelectedIndex].defenseId;
            Session.Remove(SelectedIndex);
            SelectedIndex = -1;
            RebuildViews();
            AudioManager.Play(SoundId.UiBack, Vector3.zero);
            Hud?.Toast(NameOf(id) + " returned to stock (place it again for free)", UIIcon.Trash, UIColors.WithAlpha(UIColors.Slate, 0.95f));
            UpdateSelectionVisuals();
            Refresh();
        }

        public void RebuySelectedWreck()
        {
            if (SelectedWreck < 0) return;
            var err = Session.Rebuy(SelectedWreck);
            LastError = err;
            if (err != PlacementError.None) { Feedback(err); return; }
            SelectedWreck = -1;
            AudioManager.Play(SoundId.UiPurchase, Vector3.zero);
            RebuildViews();
            Select(Session.Placements.Count - 1);
        }

        public void DismissSelectedWreck()
        {
            if (SelectedWreck < 0) return;
            Session.DismissWreck(SelectedWreck);
            SelectedWreck = -1;
            RebuildViews();
            UpdateSelectionVisuals();
            Refresh();
        }

        public int RebuyAll()
        {
            int n = Session.RebuyAll(out var err);
            if (n > 0) AudioManager.Play(SoundId.UiPurchase, Vector3.zero);
            if (err != PlacementError.None) Feedback(err);
            else if (n > 0) Hud?.Toast("Rebought " + n + (n == 1 ? " defense" : " defenses"), UIIcon.Coin, UIColors.WithAlpha(UIColors.GoldDark, 0.95f));
            RebuildViews();
            Deselect();
            return n;
        }

        public void Arm(string id)
        {
            ArmedId = ArmedId == id ? null : id;
            if (ArmedId != null)
            {
                Deselect();
                Hud?.Toast("Tap the ground to place " + NameOf(id), UIIcon.Finger, UIColors.WithAlpha(UIColors.Slate, 0.95f));
            }
            Refresh();
        }

        public bool Save()
        {
            if (!MetaGame.CommitBaseEdit(Session))
            {
                Hud?.Toast("Not enough gold to pay for this base", UIIcon.Coin, UIColors.WithAlpha(UIColors.RedDark, 0.95f));
                AudioManager.Play(SoundId.UiError, Vector3.zero);
                return false;
            }
            // Fresh session from the saved profile (pending gold is now paid).
            int sel = SelectedIndex;
            Session = MetaGame.BeginBaseEdit();
            RebuildViews();
            SelectedIndex = sel < Session.Placements.Count ? sel : -1;
            UpdateSelectionVisuals();
            AudioManager.Play(SoundId.UiPurchase, Vector3.zero);
            Hud?.Toast("Base saved", UIIcon.Check, UIColors.WithAlpha(UIColors.GreenDark, 0.95f));
            Refresh();
            return true;
        }

        public void RequestBack()
        {
            if (Session.Dirty && Hud != null)
            {
                Hud.Dialog.ShowChoice("Save your base?", "You have unsaved changes" + (Session.PendingGold > 0 ? " and " + Session.PendingGold + " gold of purchases" : "") + ".",
                    "SAVE", ButtonColor.Green, () => { if (Save()) Leave(BattleLaunch.MenuScene); },
                    "DISCARD", () => Leave(BattleLaunch.MenuScene));
                return;
            }
            Leave(BattleLaunch.MenuScene);
        }

        public void RequestTest()
        {
            if (Session.Dirty && Hud != null)
            {
                Hud.Dialog.ShowChoice("Save before testing?", "The test attack uses your saved base. Save your changes first.",
                    "SAVE & TEST", ButtonColor.Green, () => { if (Save()) LaunchTest(); }, null, null);
                return;
            }
            LaunchTest();
        }

        void LaunchTest()
        {
            if (ScreenTransition.Busy) return;
            BattleLaunch.PrepareDefense(true);
            ScreenTransition.Load(BattleLaunch.BattleScene, Team);
        }

        void Leave(string scene)
        {
            if (ScreenTransition.Busy) return;
            ScreenTransition.Load(scene, Team);
        }

        void Feedback(PlacementError err)
        {
            AudioManager.Play(SoundId.UiError, Vector3.zero);
            Hud?.Toast(BaseEditSession.Describe(err), UIIcon.Close, UIColors.WithAlpha(UIColors.RedDark, 0.95f));
        }

        public string NameOf(string id) => Session != null && Session.TryEntry(id, out var e) ? e.displayName : id;

        void Refresh()
        {
            Hud?.Refresh();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ input

        /// <summary>Called by a tray card on pointer down.</summary>
        public void OnCardPressed(string id)
        {
            pressCard = id;
            gesture = Gesture.Card;
            dragged = false;
            pressScreen = PointerInput.Position;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            HandlePointer();
            HandleWheel();
            zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-10f * dt));
            ApplyCamera();
        }

        void HandleWheel()
        {
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return;
            float s = m.scroll.ReadValue().y;
            if (Mathf.Abs(s) > 0.01f && !PointerInput.IsOverUI(PointerInput.Position))
            {
                targetZoom = Mathf.Clamp(targetZoom - Mathf.Sign(s) * 1.5f, minZoom, overviewZoom);
                Overview = targetZoom >= overviewZoom - 0.01f;
            }
#endif
        }

        void HandlePointer()
        {
            if (cam == null || Session == null || (Hud != null && Hud.ModalOpen)) return;
            Vector2 sp = PointerInput.Position;
            bool overUI = PointerInput.IsOverUI(sp);
            bool hasWorld = PointerInput.ScreenToGround(cam, sp, out Vector3 w);

            if (PointerInput.PressedThisFrame && gesture != Gesture.Card)
            {
                pressScreen = sp;
                dragged = false;
                if (overUI) { gesture = Gesture.None; return; }
                pressWorld = w;
                panStartFocus = focus;
                pressIndex = Session.PickAt(w);
                gesture = pressIndex >= 0 && (pressIndex == SelectedIndex || MoveMode) ? Gesture.MoveDrag : Gesture.Pan;
            }

            if (PointerInput.IsDown)
            {
                if ((sp - pressScreen).magnitude > Screen.height * 0.015f) dragged = true;
                switch (gesture)
                {
                    case Gesture.Pan:
                        if (dragged && hasWorld)
                        {
                            // Keep the pressed ground point under the finger.
                            var rot = Quaternion.Euler(elevation, 0f, 0f);
                            Vector3 camAtPress = panStartFocus - rot * Vector3.forward * distance;
                            var ray = cam.ScreenPointToRay(sp);
                            Vector3 o = ray.origin - cam.transform.position + camAtPress;
                            float t = -o.y / ray.direction.y;
                            Vector3 now = o + ray.direction * t;
                            focus = panStartFocus + (pressWorld - now);
                        }
                        break;
                    case Gesture.Card:
                        if (!overUI && hasWorld && pressCard != null) ShowGhost(pressCard, w);
                        else if (ghost != null && ghost.activeSelf) HideGhost();
                        break;
                    case Gesture.MoveDrag:
                        if (dragged && hasWorld && pressIndex >= 0 && pressIndex < Session.Placements.Count)
                        {
                            if (SelectedIndex != pressIndex) Select(pressIndex);
                            ShowGhost(Session.Placements[pressIndex].defenseId, w, pressIndex, false);
                            if (pressIndex < views.Count && views[pressIndex] != null) views[pressIndex].SetActive(false);
                        }
                        break;
                }
            }

            if (PointerInput.ReleasedThisFrame)
            {
                var g = gesture;
                gesture = Gesture.None;
                switch (g)
                {
                    case Gesture.Card:
                        HideGhost();
                        if (!overUI && hasWorld && pressCard != null && dragged) TryPlace(pressCard, w);
                        else if (overUI && !dragged && pressCard != null) Arm(pressCard);
                        pressCard = null;
                        break;
                    case Gesture.MoveDrag:
                        HideGhost();
                        if (pressIndex >= 0 && pressIndex < views.Count && views[pressIndex] != null) views[pressIndex].SetActive(true);
                        if (dragged && hasWorld) { if (SelectedIndex != pressIndex) Select(pressIndex); MoveSelectedTo(w); }
                        else if (MoveMode && hasWorld && pressIndex != SelectedIndex) MoveSelectedTo(w);
                        else TapAt(w);
                        break;
                    case Gesture.Pan:
                        if (!dragged && hasWorld) TapAt(w);
                        break;
                }
                pressIndex = -1;
            }
        }

        void TapAt(Vector3 w)
        {
            if (ArmedId != null) { TryPlace(ArmedId, w); return; }
            if (MoveMode && SelectedIndex >= 0) { MoveSelectedTo(w); return; }
            int i = Session.PickAt(w);
            if (i >= 0) { Select(i); AudioManager.Play(SoundId.UiClick, w); return; }
            int k = Session.PickWreckAt(w);
            if (k >= 0) { SelectWreck(k); AudioManager.Play(SoundId.UiClick, w); return; }
            Deselect();
        }
    }
}
