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
