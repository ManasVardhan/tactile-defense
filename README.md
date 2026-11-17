# Paint Squad Wars (Tactile Wars study)

An original-asset Unity 6 recreation of the 2015 mobile game "Tactile Wars" by Ankama. In game it is titled **"Paint Squad Wars"**. This is an **unofficial fan study, not affiliated with or endorsed by Ankama**. No assets from the original game are used (see "Dependencies and licenses").

You command a squad of tiny helmeted paint soldiers with one finger: tap to march, draw a stroke to reshape the squad into a line, arc, circle or any free shape, capture two points per base, then build and test your own base. It is 3D models under an orthographic camera (owner requirement; the original is 2D sprites).

Status in one paragraph: all gameplay, meta game, base editor, defense battles, audio and UI described below exist and are covered by automated tests that run in the Unity editor. It has never been built or run on a phone or tablet, audio has never been listened to by a human, and several features of the original brief are partial or missing (see "Known limitations" and `Docs/STATUS_CHECKLIST.md`).

## Requirements

- Unity **6000.3.22f1** (Unity 6.3), Universal Render Pipeline. `ProjectSettings/ProjectVersion.txt` pins this version.
- macOS used for all development (Apple M2, 8 GB RAM). Nothing was tested on Windows or Linux.
- Only needed to regenerate assets: Blender 5.3 (`blender` on PATH), Python 3 with numpy, ffmpeg (music encode). The generated models, textures and audio are already committed under `Assets/`, so you do not need these to open or play the project.

## Open the project

1. Install Unity Hub and the editor version 6000.3.22f1.
2. In Unity Hub choose Add, then Add project from disk, and select this folder (`TactileWars`).
3. Open it with 6000.3.22f1. The first import takes several minutes.
4. Open `Assets/Scenes/Boot.unity` (the start scene). Build settings order: Boot, MainMenu, Battle, BaseEditor.

## Run

Press Play with `Boot.unity` open. Boot loads the main menu. From the menu: ATTACK picks one of ten AI bases, ARMY manages troops and the mercenary, RESEARCH spends gold, BASE shows your base and opens the base editor, PROFILE shows level, league and stats. A first-run team select lets you pick one of six faction colours.

Save data is `profile.json` in `Application.persistentDataPath`. Delete it to start a fresh profile.

## Controls

Mouse and touch share one input path (`Assets/Scripts/Runtime/Core/PointerInput.cs`).

In battle:
- **Drag on the ground** to draw a formation stroke. The squad reshapes along it (line, arc, circle, square, free shape; closed shapes spread around the loop).
- **Tap the ground** to move the squad with its formation kept (routes around walls).
- **Tap a flag-bearer bubble** to pop it and wipe that enemy squad.
- **Tap the POW badge, or press R**, to call reinforcements when the squad badge meter is full.
- **Pause tab** (top left) opens resume, restart, settings and quit to base.
- Touches that start on the HUD do not start a stroke.

In the base editor:
- Drag a card from the tray onto the map, or tap a card then tap the ground, to place a defense. A ghost snapped to a 1 unit grid shows green when valid and red when not (budget, space, zone, overlap and research limits are enforced).
- Tap a placed defense to select it: ROTATE, MOVE and REMOVE buttons appear (or drag it).
- Drag empty ground to pan; mouse wheel or the overview button to zoom.
- SAVE commits and pays gold; TEST runs a defense battle against a simulated AI attacker with no rewards.
- In defense battles there are 1x, 2x and 4x speed buttons and a Paint Strike button.

There is **no pinch zoom** in battle and no keyboard shortcut other than R.

## Editor menu (TactileWars)

| Menu item | What it does |
|---|---|
| Project Setup | Configures URP, layers, tags, input handling (`ProjectSetup.Run`). |
| Build Assets | Materials, import settings, animator controllers, prefabs, ScriptableObjects; keeps tuned values. |
| Build Assets (reset tunables) | Same, but rewrites ScriptableObject values from builder defaults. |
| Build Scenes | Builds Boot, MainMenu, Battle, BaseEditor (`SceneBuilder.BuildAll`). |
| Build Everything | Assets plus scenes (`SceneBuilder.BuildEverything`). |
| Build UI and Scenes | Assets, fonts, UI art (rendered from prefabs), progression data, maps, then scenes (`UIBuild.BuildAll`). |
| Build Data and Scenes | Same without re-rendering UI art (`UIBuild.BuildDataAndScenes`). |
| Build UI Art Only | UI art only (`UIBuild.BuildArtOnly`). |
| Build Map Variants | Home, Canyon Pass, Fort Ridge map data (`MapBuilder`). |
| Validate Project | Writes `Logs/validation.txt` (`ProjectValidator.Run`). |
| Capture Screenshots | PlayMode category `Screenshots`. |
| Capture UI Screenshots | PlayMode category `UIScreenshots`. |
| Measure Frame Time | PlayMode category `Perf`. |

## Batchmode commands

Take the lock first if more than one person or agent works on the machine (`mkdir .unity.lock`, and `rmdir` it afterwards). Only one Unity instance can have the project open. Replace `$UNITY` with `/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity`.

```
# Build assets, UI art, data and scenes
$UNITY -batchmode -projectPath . -executeMethod TactileWars.EditorTools.UIBuild.BuildAll -quit -logFile Logs/build.log

# Rebuild assets and scenes only (no UI art re-render)
$UNITY -batchmode -projectPath . -executeMethod TactileWars.EditorTools.UIBuild.BuildDataAndScenes -quit -logFile Logs/build.log

# Validate the project (writes Logs/validation.txt)
$UNITY -batchmode -projectPath . -executeMethod TactileWars.EditorTools.ProjectValidator.Run -quit -logFile Logs/validate.log

# Tests (see Docs/TESTING.md for categories)
$UNITY -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log

# Screenshots into Docs/Screenshots (rendering needs a graphics device: do not pass -nographics)
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testCategory Screenshots -testResults Logs/shots.xml -logFile Logs/shots.log
```

Other screenshot categories: `UIScreenshots`, `DefenseScreenshots`, `Lineup`. Performance: category `Perf`. Bot playthroughs: category `Bot`.

## Regenerate models and audio

Models (Blender 5.3, headless). Each script writes FBX files into `Assets/Art/Models/...` and preview PNGs into `Docs/Previews/`:

```
blender -b -P Tools/blender/build_soldier.py          # Soldier, SoldierHeavy, Hero
blender -b -P Tools/blender/build_flag.py             # Flag
blender -b -P Tools/blender/build_structures.py       # Turret, Tank, Mine, Bunker, Cannon, HQ, CapturePoint, ...
blender -b -P Tools/blender/build_environment.py      # trees, rocks, props, HexTile
blender -b -P Tools/blender/build_defenses_p5.py      # Sniper, Geyser
blender -b -P Tools/blender/build_lineups.py          # preview lineups
```

Audio (deterministic, fixed seeds; needs python3 and numpy, ffmpeg for the OGG music):

```
python3 Tools/audio/synth_sfx.py      # Assets/Audio/SFX/*.wav
python3 Tools/audio/synth_music.py    # Assets/Audio/Music/*.ogg
python3 Tools/audio/verify_audio.py   # ffprobe and volume report
```

After regenerating models, run `Build Assets` so Unity re-imports and rebuilds prefabs. Details: `Assets/Audio/README_AUDIO.md`.

## Build for Android and iOS

**Android and iOS build support modules are NOT installed on this machine** (`/Applications/Unity/Hub/Editor/6000.3.22f1/PlaybackEngines` contains only WebGL support). **No device build has been made or tested**, and no Android or iOS player settings (bundle id, signing, icons) have been configured. The project contains `Assets/Art/UI` art but no app icon wired into player settings.

Steps to get there:
1. Unity Hub, Installs, the cog on 6000.3.22f1, Add modules. For Android tick Android Build Support with OpenJDK and Android SDK and NDK Tools. For iOS tick iOS Build Support. iOS builds also need Xcode and an Apple developer team for device signing.
2. Open the project, File, Build Profiles, add the Android or iOS profile and click Switch Platform (re-import takes a while).
3. Player Settings: set a bundle identifier, product name, icon, minimum API level (Android) and target minimum iOS version. Default orientation is currently auto-rotation (`defaultScreenOrientation: 4`); set landscape if desired.
4. Add scenes in order: Boot, MainMenu, Battle, BaseEditor (already set in Build Profiles scene list).
5. Build, then install on a device and profile it. Expect to reduce draw calls first (see Known limitations). Consider moving the HUD to an overlay camera on device (see `Docs/Comparisons/NOTES.md`).

## Implemented features

Everything below exists on disk; the matching evidence is in `Docs/STATUS_CHECKLIST.md` and `Docs/TESTING.md`.

- Orthographic battle camera (31 degrees elevation) that follows the squad, hex ground shader, sand biome, three map layouts (Home Base, Canyon Pass, Fort Ridge).
- Formation drawing: stroke recorder, resampler, shape recognizer (line, circle, square, free), formation solver, live single-file chain, hop and squash animation.
- Squad movement: tap to move, formation preserved, A* routing around walls, cheer on capture.
- Combat: auto-firing paint soldiers, pooled paint projectiles, paint splats, death ghosts, flag-bearer bubbles that wipe a squad when tapped, reinforcement meter and POW, two sequential capture points, counterattack wave after the first capture, victory and defeat.
- Defenses: mine, geyser, turret, defender squad, tesla antenna, sniper tower, bunker, cannon, tank.
- Base editor with budget, space and zone limits, rotate, move, remove, stock and rebuy, JSON save; TEST and DEFEND battles with an AI attacker; simulated offline raids labelled as such.
- Meta: gold, prisms, XP and levels, research tree (Troops, Offense, Defense) gated by level and paid in gold, six faction colours, four leagues, ten AI bases, three mercenaries (one hired per battle), grades S to D, save migration.
- UI: main menu, attack list, army, research, base, profile, settings, team select, pause menu, tutorial overlay, result cards, all generated by code from a UI skin with OFL fonts.
- Audio: 42 synthesized sound effect files and two music loops with per-category volume.
- Test tooling: 42 EditMode tests, 30 PlayMode tests, bot playthroughs, perf sampler, screenshot capture, project validator.

## Known limitations

These are the gaps found in the docs and contract deviations; none is hidden.

- **Five of the original's defense types are not implemented**: heli-tower, tracker, vannon, robotank and trapped slab are named in the research sources but have no model, prefab or behaviour. Nine of our own defense types exist (mine, geyser, turret, defender squad, tesla, sniper, bunker, cannon, tank), all with our own behaviour. The decorative `TeslaAntenna` environment prop is separate from the `Tesla` defense.
- **No real multiplayer or online services.** Raids on your base are a simulated statistical model (`RaidSimulator`), labelled "SIMULATED OFFLINE RAID: NOT A REAL PLAYER" in the UI. The leaderboard and alliance roster are synthetic data from `LocalOfflineProvider`; there is no alliance screen, only the service. There is no real-money store.
- **Audio has never been listened to by a human.** Levels and category multipliers were chosen from measured loudness only.
- **No device profiling.** All performance numbers are editor play mode on an Apple M2 in batchmode (`Docs/PERFORMANCE_REPORT.md`): about 5000 draw calls and 1.3 M triangles in the attack battle, which is far too high for a phone as it stands. Soldier materials, the forest border and splats are the first things to cut.
- **Base 10 balance is unproven**: the bot playthrough tests only assert a win on base 1. In the final run (`Logs/bot_playthrough.txt`) the bot won base 1, won base 5 with research, lost base 10 without research and won base 10 with research, but the previous recorded run lost base 10 with research, so the outcome is not stable. A human may do better or worse.
- **No pinch zoom** and no other camera control in battle: zoom is constant by design.
- **Post-processing (vignette, bloom, grading) tints the HUD** because canvases render with the main camera. A Low quality toggle disables post; an overlay UI camera was tried and reverted.
- **Mercenaries share one model** (the Hero mesh); Brick, Banner and Spark differ only in data. There is no `Mercenary.fbx`.
- **All numeric balance values are estimates** (squad size, health, damage, rates, costs, research, league thresholds, grade formula). The references give no stats. They live in ScriptableObjects under `Assets/Data`.
- **Visuals**: no lava biome, no rain, soldiers and enemy spacing differ from the reference (see `Docs/VISUAL_FIDELITY_REPORT.md`). Only the sand biome exists.
- **No daily gold gift, no VS splash screen before a battle, no world map** (a list of AI bases is used instead), no chests or keys, no boss battles.
- **No soak or long-run memory test** (20 consecutive battles, 5 minute memory growth) was written or run.
- **No PlayMode assertion that audio sources fire** on key events.
- **App icon, bundle id and player build settings** are not configured; the project has only been run in the editor.
- Screenshots in `Docs/Screenshots` are produced at 1280x720 in the editor; they are not device captures.

## Dependencies and licenses

- Unity 6000.3.22f1 with URP, Input System 1.20.0, TextMeshPro and Test Framework, under the Unity package license. No Asset Store content.
- **All models, textures, shaders, UI art, VFX and audio are original and generated for this project**: models by Blender scripts in `Tools/blender`, textures and UI art by editor scripts in `Assets/Scripts/Editor`, audio by oscillator and noise synthesis in `Tools/audio` (no samples).
- **Fonts** (third party, SIL Open Font License 1.1, texts next to the files): Lilita One and Fredoka. See `Assets/Art/Fonts/LICENSES.md`. The Fredoka file is a static SemiBold instance derived from the upstream variable font.
- **No assets from the original game** are in `Assets/`.
- **`Docs/References` (screenshots, Play Store images, video frames, research notes) are third-party copyrighted material kept for private study only. Do not redistribute them.** If this repository is ever published, remove `Docs/References`, and the left halves of the images in `Docs/Comparisons`, first.
- The names "Tactile Wars" and "Ankama" belong to their owners. The in-game title is "Paint Squad Wars". Clear or change any remaining use of the original name before any distribution.

## Documentation map

- `Docs/STATUS_CHECKLIST.md` point by point status against the brief, with gaps first.
- `Docs/TESTING.md` how to run tests and the results of the final run.
- `Docs/VISUAL_FIDELITY_REPORT.md` reference versus build, screen by screen.
- `Docs/PERFORMANCE_REPORT.md` editor measurements.
- `Docs/ARCHITECTURE_CONTRACT.md` architecture, conventions and dated deviation notes.
- `Docs/GAME_DESIGN_DOCUMENT.md`, `Docs/GAMEPLAY_MECHANICS.md`, `Docs/VISUAL_STYLE_GUIDE.md`, `Docs/REFERENCE_COMPARISON.md`, `Docs/ASSET_INVENTORY.md`, `Docs/IMPLEMENTATION_ROADMAP.md`.
