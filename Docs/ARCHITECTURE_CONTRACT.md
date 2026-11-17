# Tactile Wars Recreation: Architecture Contract

This file is the shared contract between all agents working on the project. If you
need to deviate, append a dated note to the "Deviations" section at the bottom so
later agents can see it. Never use em dashes in any project file.

Project root (Unity project): `/Users/manasvardhan/TactileWars`
Unity: 6000.3.22f1 at `/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity`
Blender: 5.3.0, `blender` on PATH (`/opt/homebrew/bin/blender`), run headless with `blender -b -P script.py`
Machine: Apple M2, 8 GB RAM, about 12 GB free disk. Be frugal: no large downloads,
textures at most 1024 px, never run two Unity instances, do not run Unity and a
heavy Blender render at the same time if you can avoid it.

## Evidence sources (read before designing anything)

- `Docs/References/web/RESEARCH_MECHANICS.md`: sourced mechanics research.
- `Docs/References/VISUAL_ANALYSIS.md`: frame-based visual analysis (may still be in progress).
- `Docs/References/playstore/`, `Docs/References/video_frames/`: real reference images. LOOK at them with the Read tool.

Anything not supported by these is an estimate and must be labeled as such in docs
and kept configurable (ScriptableObjects).

## Visual targets from the real reference (verified by viewing frames)

Key reference images: `Docs/References/playstore/ps_12.png` and `ps_06.png` (plain gameplay),
plus `Docs/References/video_frames/f_*.jpg`. Full notes: `Docs/References/VISUAL_ANALYSIS.md`.

- The original renders 2D sprites; this project uses 3D models (owner requirement) but must
  reproduce the same read: ORTHOGRAPHIC camera, elevation about 30 to 40 degrees above the
  horizon looking down the +Z axis, constant zoom, pans with the squad. The screen is about
  25 to 30 soldier-heights tall. Soldiers are tiny on screen (about 3.5 percent of screen
  height), so silhouettes must be bold: helmet dome is half the soldier, white face with two
  black dot eyes, tiny body, chunky grey gun, thin white rim on the helmet.
- Player squad: 30 to 50 soldiers arranged single file along the drawn stroke, spacing about
  1.0 to 1.3 soldier widths. A large flat team-colored ring with a spiral paint blot at its
  center marks the squad anchor on the ground.
- Enemy squads stand in loose clusters around a flag bearer inside a white bubble with a
  team-colored arc ring; small pennant flags with a skull emblem; dark disc mines with a
  skull mark scattered on the ground.
- Projectiles: small round dots, white core with team-colored edge, fired in dotted streams.
  Persistent paint splats on the ground in the shooter's team color. Dead units leave a
  translucent ghost wisp and a white puff. NO health bars or damage numbers on soldiers.
- Ground: low saturation, mid value sand/olive (about #B9AD8A to #C7B78D) with a subtle
  hexagonal tile pattern; team colors are the only strongly saturated colors. Props: pines
  with layered cones, canvas tents, wooden barricades, cardboard crates, crenellated beige
  walls, stone bunker. Lighting is soft and ambient, soft blob shadows under units.
- HUD: dark rounded pause tab top-left; round squad badge top-right (white ring, three
  soldier busts, team-colored fill arc); hexagonal POW badge next to it. Nothing else.
- Team colors seen: red about #DA4029, teal about #229A8A, pink about #DB4793, green about
  #8DB52B. Default: player Red, enemy Teal.

## Unity lock

Only one Unity process may have the project open. Before launching Unity:
`mkdir /Users/manasvardhan/TactileWars/.unity.lock` (atomic; if it fails, another agent
holds it: wait and retry every 60 s). Always `rmdir` it when your Unity process has exited,
including on failure. Never leave Unity running when you finish.

## Units and orientation

- 1 Unity unit = 1 meter. Y up. Characters face +Z in Unity at rotation zero.
- Model origin at the feet / ground contact center.
- Soldier total height about 1.0 unit (head and helmet roughly half of it).
- Battlefield is the XZ plane at y = 0.

## Folders

```
Assets/
  Art/Models/Characters/   Soldier.fbx, Flag.fbx
  Art/Models/Structures/   Turret.fbx, Tank.fbx, Mine.fbx, Bunker.fbx, Cannon.fbx,
                           TeslaAntenna.fbx, HQ.fbx, CapturePoint.fbx, Sandbags.fbx
  Art/Models/Environment/  Tree_A/B/C.fbx, Bush_A/B.fbx, Rock_A/B/C.fbx, GrassTuft.fbx,
                           Flower_A/B.fbx, Fence.fbx, Crate.fbx, Barrel.fbx, Log.fbx
  Art/Materials/  Art/Shaders/  Art/Textures/  Art/VFX/  Art/UI/  Art/Fonts/
  Audio/SFX/  Audio/Music/
  Data/            ScriptableObject assets
  Prefabs/Units/ Prefabs/Structures/ Prefabs/Environment/ Prefabs/VFX/ Prefabs/UI/
  Scenes/          Boot.unity (start scene), MainMenu.unity, Battle.unity, BaseEditor.unity
  Scripts/Runtime/ Core, Formation, Squad, Combat, Units, Structures, Battle, CameraRig,
                   Meta, Save, UI, Audio, Online   (namespace TactileWars.<Folder>)
  Scripts/Editor/  builders and automation (namespace TactileWars.EditorTools)
  Settings/        URP pipeline and renderer assets, volume profiles
  Tests/EditMode/  Tests/PlayMode/
Tools/blender/     asset generation scripts (source of truth for all models)
Tools/audio/       audio synthesis scripts
Docs/              research, reports, previews (Docs/Previews/*.png)
```

## Model contract (Blender to Unity)

All models: smooth shaded, rounded/beveled cartoon forms, no textures required
(flat colored materials), modest poly counts (soldier under 2.5k tris, props under 1.5k,
large structures under 4k). FBX export must import in Unity upright, facing +Z, at
scale 1 with no extra rotation on the root (use `apply_scale_options='FBX_SCALE_ALL'`,
`axis_forward='-Z'`, `axis_up='Y'`, `bake_space_transform=True`, and verify).

Material slot names are part of the contract. Unity builders remap them to project
materials by name:

- `TeamColor`: the part tinted to the owning team color at runtime (uniform, helmet,
  turret shell, flag cloth). Author it as a mid blue.
- `TeamColorDark`: darker trim of the team color.
- `Skin`, `Dark` (boots, gun body, tracks, near black-blue), `Metal` (light grey),
  `Accent` (white/cream), `Paint` (paint tank / nozzle glow, tinted to team color),
  `Wood`, `Leaf`, `LeafLight`, `Trunk`, `Rock`, `Sand`, `Glass`.

Soldier.fbx:
- One skinned mesh `Soldier_Mesh`, armature `Soldier_Rig`, bones: `Root, Hips, Spine,
  Head, Arm.L, Arm.R, Hand.R, Leg.L, Leg.R, Gun` (gun is part of the mesh, weighted to `Gun`,
  child of `Hand.R`). An empty/bone tip `Muzzle` at the barrel end is desirable.
- Animation takes (looping noted): `Idle` (loop), `Run` (loop, about 0.5 s cycle),
  `Shoot` (about 0.25 s, recoil squash), `Hit` (0.2 s flinch), `Die` (0.6 s, pop and fall
  flat), `Cheer` (loop). Use anticipation, overshoot, and squash and stretch.
- Imported in Unity as Generic rig.

Structures with code-driven moving parts use child object names:
- Turret.fbx: `Base`, `Head` (yaws toward target), `Muzzle` (empty at barrel tip, child of Head).
- Tank.fbx: `Hull`, `Turret`, `Muzzle`.
- Cannon.fbx: `Base`, `Barrel`, `Muzzle`.
- CapturePoint.fbx: `Pad` (flat circular disc, radius 2.0), `Pole`, `Flag` (TeamColor).
- HQ.fbx: single mesh, footprint about 5 x 5, height about 4.
- Mine.fbx: disc radius about 0.35 with a `Paint` cap.

Every Blender script must also render a preview PNG (about 800 px, 3/4 top-down view
similar to the game camera, soft lighting) to `Docs/Previews/<AssetName>.png`, and the
authoring agent must view the preview and iterate until it looks appealing.

## Runtime architecture (C#)

Assembly definitions: `TactileWars.Runtime` (Assets/Scripts/Runtime), `TactileWars.Editor`
(Assets/Scripts/Editor), test assemblies under Assets/Tests. Legacy Input Manager APIs are
not used: use the Input System package only if needed, otherwise wrap input in
`Core/PointerInput.cs` so touch and mouse share one path. (Set Active Input Handling to
"Both" in project settings.)

Physics layers: 6 `Ground`, 7 `PlayerUnit`, 8 `EnemyUnit`, 9 `Structure`, 10 `Obstacle`,
11 `Projectile`, 12 `Pickup`.

Teams: `TeamColorId { Blue, Green, Orange, Red, Pink, Purple }` with a `TeamPalette`
ScriptableObject (primary, dark, paint colors). Player picks one; enemies use another.

Key systems (gameplay logic separate from presentation):
- `Formation/StrokeRecorder` (pointer to world path), `Formation/PathResampler`
  (smoothing, arc-length resample), `Formation/ShapeRecognizer` (line, circle, square, free),
  `Formation/FormationSolver` (slots along or inside the shape, min spacing, assignment by
  minimal total travel), `Formation/StrokeRenderer` (painted line feedback).
- `Squad/SquadController` (formation anchor, tap to move keeps formation, slot targets),
  `Squad/SoldierMotor` (steering, separation, accel/decel, smooth turning; no NavMesh needed
  for milestone one, simple obstacle avoidance against layer Obstacle/Structure).
- `Combat/Health`, `Combat/Targeting` (auto target nearest enemy in range),
  `Combat/Weapon`, `Combat/PaintProjectile` (pooled), `Combat/PaintSplatManager` (pooled
  ground decals, capped), `Combat/DamageFeedback`.
- `Units/Soldier`, `Units/EnemySquad` (with flag bearer: killing it spawns a tappable bubble
  that wipes the squad), `Structures/Turret, Mine, Tank, Bunker, Cannon`.
- `Battle/BattleManager` (two capture points in sequence on a linear map, win when both
  captured, lose when all player soldiers are dead), `Battle/CapturePoint`,
  `Battle/ReinforcementMeter` (fills from kills, calls in backup), `Battle/BattleEvents`
  (static C# events the UI subscribes to; gameplay never references UI).
- `CameraRig/BattleCamera` (follows squad centroid, clamped to map bounds, light shake).
- `Core/Pool<T>`, `Core/GameConfig` ScriptableObjects in `Assets/Data`.
- `Meta/*` (profile, XP, gold, prisms, research/upgrades), `Save/SaveSystem` (JSON in
  `Application.persistentDataPath`), `Online/*` interfaces only with a local offline provider.

Editor automation (menu `TactileWars/...` and callable via `-executeMethod`):
- `TactileWars.EditorTools.ProjectSetup.Run` (URP, layers, quality, input, tags)
- `TactileWars.EditorTools.AssetBuilder.BuildAll` (materials, model import settings,
  animator controllers, prefabs, ScriptableObjects)
- `TactileWars.EditorTools.SceneBuilder.BuildAll` (all scenes, build settings)
- `TactileWars.EditorTools.Screenshotter.CaptureAll` (renders each scene camera to
  `Docs/Screenshots/*.png` so results can be inspected without the GUI)

Honesty rules: do not claim something compiles, runs, or looks right unless you ran it
and looked. Record what you verified and how in your final report.

## Deviations

(append dated notes here)

### 2026-11-17 Structures and environment props (environment/prop agent)

Scripts: `Tools/blender/tw_env_common.py` (shared helpers), `build_structures.py`,
`build_environment.py`, `build_lineups.py`, `verify_env_fbx.py`.
- New material slots: `Canvas` (tent cloth, preview #E6DAB9) and `Stone` (beige wall,
  bunker, HQ and pad stone, preview #CDBE9B). Unity builders should map them like the others.
- `Leaf`/`LeafLight` and some props use baked two-tone faceting: faces turned toward the key
  light use `LeafLight`. Keep `LeafLight` clearly lighter than `Leaf` in Unity.
- `Sand` is used for sandbags and tent seams (preview #B5A174, darker than the ground #C2B48C).
  If the ground uses a `Sand` material, keep the prop `Sand` darker or sandbags will vanish.
- FBX exporter bug workaround: with `bake_space_transform=True`, Blender 5.3's exporter writes
  wrong local transforms for objects two levels deep (Muzzle under Head under Base, Flag under
  Pole under Pad). `tw_env_common.patch_fbx_exporter()` monkeypatches
  `io_scene_fbx.fbx_utils.ObjectWrapper.fbx_object_matrix` at export time so every child gets
  `G @ local @ G^-1`. Export settings are otherwise exactly the contract ones. Verified by
  dumping Lcl Translation/Rotation from the FBX files. Any other agent exporting nested
  non-bone hierarchies with baking is affected by the same bug.
- Pivots: Turret `Head` origin on the yaw axis at y 0.82; Tank `Turret` at y 0.96; Cannon
  `Barrel` on the trunnion at y 0.78 with a rest pitch of 8 degrees up (local X rotation).
  Every `Muzzle` is an empty with identity local rotation, its +Z (Unity) along the bore.
- CapturePoint hierarchy is `Pad` > `Pole` > `Flag`; `Flag` origin is the attach point at the
  pole top (local y 3.0 above the Pole origin), cloth extends toward Unity +X.
- Mine `Paint` cap is a raised ring around the skull emblem.
- Wall is 2.0 long on X, tiles every 2.0 units; WallCorner is a 0.98 square pillar centered at
  its origin, to be placed at wall junctions/ends. HQ is 5 x 5, roof top about 3.9, flag mount
  tip 4.6. Tent.fbx, SpawnHatch.fbx, Wall.fbx, WallCorner.fbx (Structures) and HexTile.fbx
  (Environment, 3.0 across flats, flats facing Unity +-Z) are extra files not in the folder list.

- 2026-11-17 (character artist, Tools/blender/build_soldier.py, build_flag.py):
  - Armature node orientation: Blender's FBX exporter does not apply `bake_space_transform`
    to armatures (it wrote Lcl Rotation -90 X on `Soldier_Rig`). The scripts therefore author
    the rig object pre-rotated +90 X in Blender so the exported `*_Rig` node, `Root` bone and
    `*_Mesh` node all have identity transforms (checked with Tools/blender/fbx_inspect.py).
    Export settings are otherwise exactly the contract ones. FBX GlobalSettings: Y up, +Z front,
    UnitScaleFactor 100 with meter-valued data (Unity file scale 1). Characters face +Z, Y up.
  - Takes are exported from NLA strips (`bake_anim_use_nla_strips=True`), so FBX take names
    are exactly `Idle, Run, Shoot, Hit, Die, Cheer` (Blender's re-importer shows them as
    `<Rig>|<Take>`; that prefix is Blender import naming only). 30 fps, every frame keyed.
    Looping takes (Idle 0-60, Run 0-15, Cheer 0-24) have last frame equal to first.
  - Extra bone `Muzzle` (child of `Gun`, no weights) at the barrel tip. `Root` has no weights;
    vertex weights are rigid per part except the torso (Hips to Spine blend).
  - `Skin` is used for the face and the white mitten hands. Hero.fbx has no `Skin` slot
    (visor face: `Dark` visor with `Accent` eyes).
  - Variants use the same rig and take names: `SoldierHeavy.fbx` (1.0 tall, shoulder bazooka),
    `Hero.fbx` (1.5 tall boxy trooper). Flag.fbx has two meshes: `Pole` (origin at base) and
    `Cloth` (origin at the top attach point, Lcl Translation (0, 1.52, 0); UV.x is 0 at the pole
    and 1 at the free edge for a vertex-shader wave). Flag is static (no armature).

### 2026-11-17 Unity gameplay and rendering, milestone one (Unity lead agent)

- `TeamColorId` gains `Teal = 6`. The contract names Teal as the default enemy color but the enum
  lacked it. Player default Red, enemy default Teal (`Assets/Data/GameConfig.asset`).
- Characters (Soldier, SoldierHeavy, Hero) remap their slots to outlined unit materials `M_U_<Slot>`
  (shader `TW/ToonOutline`, white inverted hull at about 1.25 px) for readability at tiny size.
  Props and structures use `M_<Slot>` (`TW/Toon`). Per team shared materials live in
  `Assets/Art/Materials/M_Team_<Team>_*` and are swapped in by `TeamTint` (no per renderer
  instances, SRP batcher friendly). `Canvas` and `Stone` slots are mapped like the others.
- Role mapping: `Hero.fbx` is the player squad leader (one per squad, scale 0.85), `SoldierHeavy.fbx`
  is the enemy flag bearer, `Flag.fbx` is carried by the bearer and stays inside the bubble.
- Scale and camera: soldier prefabs use model scale 0.9 and the camera shows 33 soldier heights
  (`CameraConfig.screenSoldierHeights`), not 25 to 30, because the authored helmet dome reads larger
  than 1.0 at 35 degrees. Measured result: soldiers are about 22 to 28 px tall at 720 p, matching
  the reference 3.5 percent. Formation `minSpacing` is 0.7 (task said about 0.6) to avoid helmets
  overlapping; both are estimates in ScriptableObjects.
- Units have no colliders. Obstacle avoidance uses `Physics.OverlapSphereNonAlloc` against
  `Obstacle | Structure`; taps on bubbles raycast the `Pickup` layer (trigger colliders). The ground
  is not a collider: pointer rays intersect the y = 0 plane mathematically. Collision matrix: unit
  layers ignore each other and Projectile/Pickup; Projectile ignores Projectile/Pickup; Pickup only
  receives raycasts; Structure/Obstacle ignore each other.
- Mines are not targetable (hidden traps); they trigger on proximity only.
- The flag bearer bubble's ground arc fills in the attacker's (player) color as the bearer takes
  damage. Interpretation of ps_12 and burst_B_030, where enemy bubbles carry a red arc.
- Capture points and defender props with TeamColor slots (SpawnHatch, HQ, TeslaAntenna) start in
  the defender color; a captured point repaints to the player color.
- Closed strokes always spread evenly around the full loop (spacing may exceed `maxSpacing`);
  extra rows for closed shapes fill rings inside the loop first, then rings outside it.
- `AssetBuilder.BuildAll` keeps existing tunable values in `Assets/Data`; `BuildAllResetData` (menu
  "Build Assets (reset tunables)") rewrites them from builder defaults.
- `Screenshotter.CaptureAll` runs the PlayMode test category `Screenshots` through the Test Runner
  API (gameplay needs play mode), then exits in batchmode. Command line equivalent:
  `-runTests -testPlatform PlayMode -testCategory Screenshots`.
- One MonoBehaviour or ScriptableObject per file named after the class (Unity serialization rule),
  so config classes have their own files (WeaponConfig, CombatConfig, CameraConfig, TeamPalette,
  TeamVisuals, EnemySquadConfig, BattlePrefabs, PooledVfx, ProjectileManager).
- Only `Boot.unity` and `Battle.unity` exist for milestone one (no MainMenu or BaseEditor yet).

### 2026-11-17 Phase 6: UI, HUD, menus, meta game and save (UI and meta agent)

Build: `TactileWars.EditorTools.UIBuild.BuildAll` (menu "TactileWars/Build UI and Scenes") runs AssetBuilder, FontBuilder,
UIArtBuilder (+ UIArtRenderer), ProgressionBuilder, then SceneBuilder. UI captures: `Screenshotter.CaptureUI`
(PlayMode category `UIScreenshots`).

- UI construction: screens and the HUD are generated by code at scene load (`UI/Kit/UIFactory`) from the
  `Assets/Data/UISkin.asset` built by the editor. Scenes only contain root components placed by SceneBuilder
  (`MenuRoot` in MainMenu, `BattleHUD` in Battle). Nothing is hand authored and no default uGUI look is used.
- Canvases are Screen Space Camera on the scene's main camera (not Overlay) so camera render-texture captures include
  the UI. Consequence: the gentle post stack (vignette, bloom, grading) also touches the UI. `AdaptiveCanvasScaler`
  (1920x1080 reference, match 0.5) measures the camera pixel size so captures at any aspect lay out correctly.
  The paint-wipe `ScreenTransition` is the only Overlay canvas (persistent, never in captures).
- TextMeshPro Essential Resources were imported into `Assets/TextMesh Pro` (required for TMP shaders). Fonts: Lilita One
  and a static SemiBold instance of Fredoka (see `Assets/Art/Fonts/LICENSES.md`). Headings get extra word spacing
  because the outline swallows Lilita's narrow space.
- UI art: `Assets/Art/UI/Shapes` (9 sliced panels, pills, tab, ribbon, rings, hex, glow, splat), `Icons` (40 icons drawn
  with signed distance shapes, one outline and shadow style), `Renders` (Soldier busts and full bodies, Hero busts,
  defenses and HQ rendered from the project prefabs in all 7 team colors, alpha recovered from black and white passes).
- Meta data (ALL ESTIMATES): `Assets/Data/Meta` (ProgressionConfig, ResearchTree with Troops/Offense/Defense branches,
  three MercenaryDef, DefenseCatalog, BaseRoster with ten BaseDefinition, `Layouts/Layout_b02..b10`). The index
  `MetaDatabase` lives in `Assets/Resources/MetaDatabase.asset` (deviation from "Data/") so any scene, test or the
  next phase can reach it with `MetaDatabase.Load()`.
- Save: `Save/SaveSystem` writes `profile.json` in `Application.persistentDataPath` (version 2, temp file then
  `File.Replace` with a `.bak`; corrupt files are kept as `profile.corrupt.json`, then backup, then a fresh profile;
  `SaveMigrations` upgrades version 1). `SaveSystem.DirectoryOverride` redirects it for tests. The player's own base is
  `SaveSystem.Profile.BaseLayout.placements` (list of `{defenseId, x, z, rotation}`), reserved for the base editor.
  All profile changes go through `Meta/MetaGame`, which saves immediately.
- Scenes: Boot now loads `MainMenu`. Build settings are Boot, MainMenu, Battle, plus `Assets/Scenes/BaseEditor.unity`
  automatically when that file exists at SceneBuilder time. The Base screen loads "BaseEditor" when
  `Application.CanStreamedLevelBeLoaded` says it is in the build, otherwise it shows an under construction card.
- Gameplay hooks added (gameplay still never references UI or Meta):
  - `Core/PointerInput.UIHitTest` delegate (installed by `UIRoot`); `StrokeRecorder` ignores presses that start over UI.
  - `Squad/SquadController.leaderConfig`, `mercenaryConfig`, `mercenaryScale`, `Mercenary`. A hired mercenary is an EXTRA
    Hero model unit with its own stats (it counts in StartCount and in the alive count).
  - `Battle/BattleSetup` (execution order -1000) applies `BattleLoadout.Pending`: clones GameConfig, BattleConfig,
    SquadConfig, UnitConfig and WeaponConfig at runtime (assets are never modified), recolors the squad ring, scales
    enemy health and damage, and for generated bases destroys the handcrafted `Enemies` and `Defenses` children and
    spawns the `BaseLayoutData`. No pending loadout means the scene runs exactly as authored (all milestone tests).
  - `Battle/BaseLayoutData`, `Battle/DefenseCatalog`. Generated layouts only use prop free sockets declared in
    `SceneBuilder.SquadSockets`, `MineSockets`, `DefenseSockets`. All ten AI bases share the lane terrain, props and
    capture points; they differ in enemy color, squads, defenses, enemy stats and rewards. Base one is the handcrafted map.
  - `Audio/MusicPlayer` (persistent, two sources, 1.2 s crossfade, silent in batchmode) now owns music;
    `AudioManager.menuMusic` picks the theme, `AudioManager.SfxVolume` is the player volume. `SoundId` gained UiBack,
    UiOpen, UiPurchase, UiError, CoinPickup, LevelUp (appended); `SoundLibrary.menuMusic` added.
- Rewards: BattleSetup scales the BattleConfig gold and XP terms by the base's reward multiplier; MetaGame then applies a
  grade multiplier (grade = 70 percent survivors, 30 percent speed versus a 150 s par time; S A B C D), league points,
  first win prisms and stats. Prisms are earned on level ups and first wins and can be traded for gold in Profile.
- Teams: the six verified factions are offered; Teal stays an AI only color. An opponent never shares the player's
  color (fallback Teal, or Purple when the player is Teal).
- Quality toggle: Low disables post processing on the scene camera; the quality level switch happens only in player
  builds (in the editor it would rewrite ProjectSettings).
- Tesla antenna: research node and catalog entry point to the decorative TeslaAntenna prop (no behaviour script exists
  yet); AI layouts never place it.
- Tests: `BattleTestUtil.LoadBattle` now uses a temporary save folder (battles write meta progress). Because the UI
  renders with the main camera, the milestone `battle_*.png` captures now show the HUD.
