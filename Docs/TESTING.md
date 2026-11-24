# Testing

How to run each test suite and tool, what each covers, and the results of the latest full regression run (checkpoint 1 of the unit squads plan, 2026-11-24). The 2026-11-17 run is kept below for history.

All commands assume the project root as the working directory and `UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity`. Only one Unity instance may have the project open; the project convention is `mkdir .unity.lock` before launching and `rmdir .unity.lock` after. Do not pass `-nographics` for PlayMode (screenshots and perf sampling need a graphics device). The machine is an Apple M2 with 8 GB RAM, so run one Unity at a time.

## Open battlefield run (Plan 2 task 2b), 2026-11-24 (branch feature/unit-squads)

Hidden grid with round markers, soldiers 20 percent bigger (grid outer radius 1.5), three biomes. One full run of each suite.

| Suite | Total | Passed | Failed | Inconclusive | Skipped | Duration | Result file |
|---|---|---|---|---|---|---|---|
| EditMode | 70 | 70 | 0 | 0 | 0 | under 1 s | `Logs/editmode.xml` |
| PlayMode | 113 | 112 | 1 | 0 | 0 | 667 s | `Logs/playmode.xml` |

- The one failure, `AnimationStateTests`: no soldier died inside its fixed 9 s fight window with the wider unit spacing. The test now samples at least 9 s and on until the first death (up to 25 s); all six states are still required. Re-run alone: passed (Die seen 284 times).
- New tests: `NoGridIsDrawnInBattle`, `HoverMarkerSnapsAndShowsValidity`, `HoveringABlockedSpotShowsTheInvalidMarker` (was `OverlayMarksBlockedHexes`), `ActivePadRingGlowsOnlyWhileArmedOrSelected` (was `OverlayTintsActivePadHexes`) in ArmyInputTests; `SoldiersInAUnitDoNotOverlap` (ArmyBattleTests); `EveryLevelSpawnCanReachBothPoints` and three look captures (BiomeLookTests); `PlayerFacingTextHasNoHexWording`, `TutorialCopyUsesSpots` (EditMode PlayerTextTests). `HexAlignmentCapture` is gone with the painted hexes.
- Bot on Level 1: three VICTORY runs in 21, 22 and 22 s of game time, 8 of 9 survivors each.
- The hex-era browser captures `checkpoint1_*` and `checkpoint1b_*` were deleted (they showed the old hex ground); `look_browser_*.png` replace them.

## Playtest fixes run (Task 9), 2026-11-24 (branch feature/unit-squads)

Fixes for the eight problems of the first browser playthrough. One full run of each suite after the code changes.

| Suite | Total | Passed | Failed | Inconclusive | Skipped | Duration | Result file |
|---|---|---|---|---|---|---|---|
| EditMode | 58 | 58 | 0 | 0 | 0 | under 1 s | `Logs/editmode.xml` |
| PlayMode | 84 | 84 | 0 | 0 | 0 | 397 s | `Logs/playmode.xml` |

- **Failures: none.** No test carries `[Ignore]`. `error CS`: 0 in both logs.
- New tests: `SameFrameClickIsSeenAsPressAndRelease`, `SameFrameClickDeploysArmedUnit`, `OverlayMarksBlockedHexes`, `OverlayTintsActivePadHexes` (ArmyInputTests); `UnitOnEdgePadHexCaptures` (ArmyBattleTests); `AdvanceStepAppearsAfterPanAndCompletes`, `AdvanceStepEndsWhenEnemiesAreNear`, `TankStepSuggestsHexesWhenTankNotDeployed`, `TankStepWithTankDeployedSuggestsNothing`, `CaptureStepPreemptsTankCard`, `EveryDeployedUnitShowsItsGlyphBadge` (ArmyHudTests); `LevelOneGradeIgnoresTime`, `RewardEstimateTracksLayoutCounts` (MetaTests). `TapUnitSelectsTapHexMovesTapAgainDeselects` became `TapUnitSelectsTapHexMovesTapAgainKeepsSelection` (spec 5.3 changed).
- Bot on Level 1 (enemy damage now 0.9): three VICTORY runs in 22 s, 20 s and 22 s of game time, survivors 8, 9 and 8 of 9.
- Web build: exit 0, `[WebBuild] ok 64.8 MB (67955710 bytes, 4 scenes, 154 s)`. Replayed Level 1 in Playwright Chromium from a fresh save with plain quick clicks (no hold): every map click registered, re-clicking the selected unit kept it selected, blocked hexes showed dark, steps 1 to 8 all appeared (the capture card for under a second, since the point was taken fast), VICTORY with 3 of 9 survivors in 6:33, grade C with a dash for time. Screenshots `Docs/Screenshots/checkpoint1b_*.png`. Console: 0 errors; 6 `WebGL: INVALID_ENUM` warnings and 54 AudioContext autoplay warnings.
- New captures: `overlay_blocked_hexes`, `tut_6_advance`, `tut_7_tank`, `tut_8_capture`, `units_attacker_vs_tank` (the old `tut_6_tank` and `tut_7_capture` were renamed with the new step numbers).

## Checkpoint 1 run, 2026-11-24 (branch feature/unit-squads)

Run in this order, one after the other: EditMode, PlayMode, project validator, web build. Unity 6000.3.22f1, batchmode, no `-nographics`. Result files are `Logs/editmode.xml` and `Logs/playmode.xml`.

| Suite | Total | Passed | Failed | Inconclusive | Skipped | Duration | Result file |
|---|---|---|---|---|---|---|---|
| EditMode | 56 | 56 | 0 | 0 | 0 | 0.24 s (12 s with editor start) | `Logs/editmode.xml` |
| PlayMode | 72 | 72 | 0 | 0 | 0 | 374 s (385 s with editor start) | `Logs/playmode.xml` |

- **Failures: none**, in a single full run of each suite. No test carries `[Ignore]`.
- `error CS` occurrences: **0** in `Logs/editmode.log`, `Logs/playmode.log`, `Logs/validate.log`.
- Project validator: `Logs/validation.txt` reads "Scanned: 4 scenes, 48 prefabs, 2485 game objects, 156 materials. Issues: 0".
- Bot playthrough (`BotWinsLevelOne`, Level 1, Attacker x2 plus Tank x1, 3x speed): three VICTORY runs in 23 s, 22 s and 23 s of game time, survivors 7, 8 and 6 of 9 (`Logs/bot_playthrough.txt`).
- Perf (editor, M2, 1280x720): attack battle main thread 3.40 ms average with 9 player soldiers; heavy defense battle 7.14 ms average (`Logs/perf_battle.txt`, `Logs/perf_defense.txt`).
- The PlayMode run regenerated `Docs/Screenshots/*.png`. Stale stroke-era screenshots with no capturing test (`battle_formation_circle`, `battle_formation_line`, `battle_stroke_drawing`, `battle_u_formation`, `ui_shape_hint`, `ui_tutorial`) were deleted.
- Web build: exit 0, `[WebBuild] ok 64.8 MB (67926337 bytes, 4 scenes, 192 s)`. Played in a Playwright Chromium session from a fresh save: team select, menu, Level 1 tutorial through all seven steps, both points captured, VICTORY (survivors 3 of 9, 5:09 including the tutorial). Screenshots `Docs/Screenshots/checkpoint1_*.png`. Browser console: 0 errors; 6 `WebGL: INVALID_ENUM: getInternalformatParameter` warnings, 3 unsupported hidden URP shader logs at startup, and 26 "Trying to get length of sound which is not loaded yet" logs.

## Final run, 2026-11-17 (history)

Run in this order, one after the other, from `Logs/` outputs named `final_*`. Unity 6000.3.22f1, batchmode, no `-nographics`.

| Suite | Total | Passed | Failed | Inconclusive | Skipped | Duration | Result file |
|---|---|---|---|---|---|---|---|
| EditMode | 42 | 42 | 0 | 0 | 0 | 0.15 s | `Logs/final_editmode.xml` |
| PlayMode | 30 | 30 | 0 | 0 | 0 | 449 s (7.5 min) | `Logs/final_playmode.xml` |

- **Failures: none.** Both suites ran clean in a single full run (the earlier 29 of 30 PlayMode result followed by a re-run of one bot test was superseded by this run).
- `error CS` occurrences: **0** in `Logs/final_editmode.log`, `Logs/final_playmode.log` and `Logs/final_validate.log`.
- Unhandled exceptions: none found. The word "Exception" appears 5 times (EditMode) and 91 times (PlayMode) in the logs, and every occurrence is a Unity Test Runner stack frame name (`ExecuteEnumerableAndRecordExceptions`, `CaptureException`); there are no exception messages or stack traces from game code. The Test Runner also fails any test that logs an error, and none failed.
- One log line `[Licensing::Module] Error: Access token is unavailable; failed to update` appears in each log. It is a Unity licensing client message in batchmode, unrelated to the project.
- Project validator (`TactileWars.EditorTools.ProjectValidator.Run`, exit 0): `Logs/validation.txt` reads "Scanned: 4 scenes, 48 prefabs, 2554 game objects, 156 materials. Issues: 0".
- The PlayMode run regenerated `Docs/Screenshots/*.png` (45 files), `Logs/perf_battle.txt`, `Logs/perf_defense.txt` and appended to `Logs/bot_playthrough.txt`. It did not regenerate `Docs/Comparisons/*.png`.

Bot playthrough outcomes in this run (`Logs/bot_playthrough.txt`; only the base 1 test asserts a win, the others record the outcome):

| Base | Loadout | Outcome | Game time | Soldiers lost |
|---|---|---|---|---|
| b01 Training Grounds | no research | VICTORY | 100 s | 11 of 40 |
| b05 Magenta Bastion | researched | VICTORY | 90 s | 41 of 52 |
| b10 Prism Citadel | no research | DEFEAT | 147 s | 50 of 40 |
| b10 Prism Citadel | researched | VICTORY | 291 s | 125 of 60 |

The b10 researched result differs from the previous recorded run (DEFEAT in 420 s), so base 10 outcomes are not stable between runs.

Perf sample from this run (editor, M2, 1280x720 render target): attack battle CPU main thread 9.60 ms average, about 4960 draw calls, about 1.30 M triangles; heavy defense battle 7.29 ms average, about 4130 draw calls (`Logs/perf_battle.txt`, `Logs/perf_defense.txt`). `PERFORMANCE_REPORT.md` holds the earlier write-up of the same measurement. These are editor numbers; no device was profiled.

### Not covered by any test

- Real touch input on a device, device builds, device performance.
- Audio output: no test asserts that sounds play, and no human has listened to them.
- Soak (20 consecutive battles) and memory growth over a long battle.
- A dedicated "all soldiers dead gives Defeat" assertion (defeat is reached by bot runs and captured in a screenshot, but not asserted by its own test).
- UI layout correctness is checked only by looking at screenshots, not by assertions.
- Visual comparison with the original is manual (`VISUAL_FIDELITY_REPORT.md`).

## EditMode suite (56 tests, about a second)

```
$UNITY -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
```

Files in `Assets/Tests/EditMode`:

| Fixture | Tests | Covers |
|---|---|---|
| `PathResamplerTests` | 5 | Path length (open and closed), equal arc-length resampling, closed loop spacing, Chaikin smoothing keeps endpoints, polyline sampling |
| `ShapeRecognizerTests` | 5 | Line, circle, square, free curve, closed-stroke detection |
| `FormationSolverTests` | 12 (7 plus 5 parameter cases) | Slot count and minimum spacing for several stroke lengths, spacing cap on long lines, extra rows for short lines, closed ring, filled inner rings, cluster for degenerate strokes, assignment is a permutation and non-crossing |
| `MetaTests` | 9 | Save round trip, corrupt save fallback to backup, version 1 migration, old tutorial flags load with an empty seen list, XP to level and level rewards, research purchase and level lock, mercenary unlock and hire, loadout from research, battle rewards saved |
| `HexGridTests` | 9 | Hex center round trip, point to hex mapping, neighbors and distance, bounds, blocked cells follow the NavGrid, reservation rules, the three soldier slots stay inside the hex and apart |
| `DrawsFireTests` | 4 | Target choice: nearest by default, a draws-fire unit (Tank) in range outranks a nearer one, out of range or dead draws-fire targets are ignored |
| `UnitRosterTests` | 2 | The six unit types with glyphs, accents, descriptions and role values; `UnitStatBarsComeFromConfig` (the Army card bars come from the type multipliers: Tank Health, Sniper Range and Vanguard Speed longer than the Attacker's, the Healer's Damage empty) |
| `BaseDefenseTests` | 13 | Raids are off while `MetaGame.BaseFeaturesEnabled` is false (no raid, no gold lost, last seen still updates) and run again when it is on, placement validity and grid snap, zone limits, overlap, budget cap, space and type caps, research lock, purchase from stock and rebuy of wrecks, commit pays gold and survives restart, defense results consume placements and tests change nothing, simulated raid determinism and label, NavGrid routing without allocations, recognizer sequence line then arc then circle |

## PlayMode suite (72 tests, about 6.5 minutes)

```
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
```

Run a single category by adding `-testCategory <name>`; categories: `Screenshots`, `UIScreenshots`, `DefenseScreenshots`, `Lineup`, `Perf`, `Bot`. Files in `Assets/Tests/PlayMode`:

| Fixture | Tests | Covers |
|---|---|---|
| `AnimationStateTests` | 1 | Animator states play at the right moments |
| `BattlePlayTests` | 4 | Advancing a unit into an enemy squad fires and damages, a unit on a capture point raises progress, bubble tap wipes a squad, reinforcements restore a lost unit when the meter is full |
| `ArmyTests` | 8 | Deploy spawns three soldiers on the hex, no double deploy or deploy on occupied or blocked hexes, move reserves the destination and frees the origin, moves to occupied or unreachable hexes are rejected, destroyed while moving frees the destination, restore the last destroyed unit, Tank health and draws-fire |
| `ArmyInputTests` | 16 | Arm then click deploys, click unit selects, click a spot moves, re-click keeps the selection, switching selection, drag pans and does not command, refused clicks while armed, bubble click still pops while armed, markers only while armed or selected, the hover marker on blocked and open spots (`overlay_invalid_spot`), the hover marker snaps from spot to spot and shows validity (blocked, taken, enemy, off the field, reject flash), no grid or hex mesh is ever drawn, the active capture ring glows only while armed or selected, no input after the battle ends |
| `BiomeLookTests` | 4 | The look captures per biome (`look_<meadow,canyon,ridge,dusk>_<topdown,start,hover_valid,hover_invalid,capture>`) |
| `LevelLayoutTests` | 7 | `EveryLevelHasItsOwnReachableMap` (Levels 1 to 5: each on its own `Map_levelN` in its biome; spawn, camera start and pan bounds, nav and hidden grid bounds, counterattack hatches and the path follow the map; a nav path from the spawn to an open pad spot of each point, from every hatch to both points and from the spawn to every enemy socket; at least 6 open pad spots); `BotFollowsTheBentPathOfTwinPasses` (Level 4 with no enemies: the bot takes both circles along the bent route with no unit stuck); captures `level_N_start` and `level_N_topdown` for the five levels |
| `ArmyHudTests` | 15 | Unit bar buttons, counts, arm and disarm, greyed at zero, relight on restore, clicks on the bar do not reach the map, no bar in defense, unit badges, backup badge and prompt, the Level 1 tutorial's guided steps, hints, skip and ordering |
| `ArmyBattleTests` | 10 | Attack battles use the army not the squad, the three resting soldiers of every unit type stand at least one soldier width apart (`SoldiersInAUnitDoNotOverlap`), camera pan and clamp, deployed unit captures, units on pad hexes (including soldiers past the pad radius) count, defeat only when every unit is lost and the meter cannot restore one, backup with nothing lost keeps its charge |
| `DefenseBattleTests` | 8 | Defense battle runs to an outcome and records consumption, attacker AI forms lines, rings and clusters, tesla chain hits, sniper aims and hits, geyser erupts, wall routing through a gate, counterattack after first capture, base editor place, validate, save and survive restart |
| `MetaFlowTests` | 2 | Generated base deploys layout and teams, full flow Boot to menu to battle to victory to rewards to return |
| `LadderFlowTests` | 5 | Level ladder and squad builder: a fresh save has only Level 1 fightable; winning Level 1 shows "Unlocked: Healer, +1 squad slot", opens Level 2 and adds a fourth slot the Healer's card can fill; ADD fills the first empty slot, locked cards read "Clear Level N to unlock" and cannot be added, the last unit stays; the chosen squad and a hired mercenary (its own one soldier button, never restored by backup) reach the battle |
| `ArmyScreenTests` | 6 | The trimmed menu has no Profile, Research or Base button (Attack, Army and settings stay, no raid); the Army screen has no Soldier Stats panel and keeps Heroes on a second tab; six unit cards in unlock order (Attacker and Tank addable, the others "Clear Level 1" to "Clear Level 4"; after Level 1 the Healer is addable and NEW for one visit); ADD fills the first empty slot for free; a full squad shows "Squad is full. Click a slot to remove a unit first."; the Army dot and the two one-time callouts (`menu_army`, `menu_add_unit`) |
| `UnitAbilityTests` | 12 | Healer heal and revive, Sniper reach, Vanguard capture weight, Demolisher shells (structure first, no friendly damage, a turret in at most 40 percent of an Attacker unit's time, at least 1.5 times its time against soldiers) |
| `BotPlaythroughTests` (category Bot) | 2 | `BotWinsLevelOne`: `ArmyBot` plays Level 1 (b01, no research, Attacker x2 plus Tank x1) three times in one test; each run must win in under 150 s of game time with at least 4 of 9 soldiers alive and no unit stuck. `BotPlaysLevelsTwoToFive`: one run each of Levels 2 to 5 with seven units of all six types; win or loss is recorded, the run must end or take a circle with no unit stuck. Runs are appended to `Logs/bot_playthrough.txt`. The bot's "forward" is the map's path (`MapPath`) |
| `LevelOneTests` | 6 | Direct-fire defenses (turret, sniper tower) shoot a Tank unit before a nearer Attacker, the cannon does not; Level 1 has no traps, one turret, two passive squads and no counterattack, and nobody moves when nothing is deployed; passive squads wake within weapon range plus 2; the tutorial's Tank step comes before its capture step (Level 1 is loaded through its loadout, on the Training Field map) |
| `PerfCapture`, `DefensePerfCapture` (Perf) | 2 | Frame timing and render counters for the attack and the heavy defense battle |
| `ScreenshotCapture` (Screenshots) | 1 | Gameplay captures into `Docs/Screenshots` |
| `UIScreenshotCapture` (UIScreenshots) | 4 | Every reachable menu screen and the HUD; the Level 1 tutorial steps and hints (`tut_*`); the ladder, squad builder, unlock line and a full unit bar (`ui_ladder_*`, `ui_squad_builder*`, `ui_victory_unlock`, `hud_unit_bar_full*`); the trimmed menu and Army (`ui_menu_trimmed`, `ui_menu_new_unit`, `ui_army_fresh`, `ui_army_new_unit`, `ui_army_full`, `ui_army_heroes`, `ui_army_tall`) |
| `DefenseScreenshotCapture` (DefenseScreenshots) | 2 | Base editor, defense battle and map variant captures |
| `SoldierLineupCapture` (Lineup) | 1 | Soldier lean lineups |

### The balance bot (`Assets/Tests/PlayMode/ArmyBot.cs`)

`ArmyBot` drives the `PlayerArmy` through its public API only (`Deploy`, `OrderMove`, `CallReinforcements`), so it plays any unit type and level. Policy: deploy every ready unit on free hexes 3 to 5 hexes short of the nearest enemy on the way to the current capture point, draws-fire units (Tanks) in the row nearest the enemy; every 1.5 game seconds order Tanks onto the pad hexes nearest its center and the other units one hex behind them, holding while an enemy is within the unit's weapon range; call backup when the meter is full and a unit was lost, and deploy it. It runs at 3x through `GameSpeed`. To balance another level, add a test to `BotPlaythroughTests` that loads that base (`Load(index)`), sets the squad before the launch with `MetaGame.SetSquadSlot` (the real loadout path; mark the levels before it cleared in `defeatedBases` so the types and slots are unlocked) or with `PlayerArmy.SetSquad` after it, and calls `new ArmyBot().Play(limit)`; read `Victory`, `GameTime`, `Survivors` and `Line(name)`.

The capture and perf tests pass as long as they run; they assert little. Treat their PNG and text output as the evidence, and look at the images.

## Web build

Build the WebGL player into `TactileWarsWeb/` (enabled build scenes, current Player settings, gzip compressed). Exits 0 and logs `[WebBuild] ok <total size>` on success, exits 1 on failure. A rebuild with a warm Library took 200 s on the M2 on 2026-11-24 (`[WebBuild] ok 64.8 MB (67926337 bytes, 4 scenes, 192 s)`); a cold first build takes much longer.

```
$UNITY -batchmode -projectPath . -buildTarget WebGL -executeMethod TactileWars.EditorTools.WebBuild.Build -quit -logFile Logs/webbuild.log
```

Serve it locally on `127.0.0.1` (default port 8765). The server sets `Content-Encoding` for `.gz` and `.br` files, the right content types for `.wasm`, `.js` and `.data`, and `Cache-Control: no-store`.

```
python3 Tools/serve_web.py [port]
```

Then open `http://127.0.0.1:8765/`.

## Tools

| Tool | Command or menu | Output |
|---|---|---|
| Project validator | `$UNITY -batchmode -projectPath . -executeMethod TactileWars.EditorTools.ProjectValidator.Run -quit -logFile Logs/validate.log` (menu TactileWars, Validate Project) | `Logs/validation.txt` (scans scenes, prefabs and materials for missing script references, broken or missing materials and shaders, missing textures and meshes) |
| Screenshots | menu TactileWars, Capture Screenshots, or `-runTests -testPlatform PlayMode -testCategory Screenshots` | `Docs/Screenshots/*.png` |
| UI screenshots | menu Capture UI Screenshots, or category `UIScreenshots` | `Docs/Screenshots/ui_*.png` |
| Perf sampler | menu Measure Frame Time, or category `Perf` | `Logs/perf_battle.txt`, `Logs/perf_defense.txt` |
| Bot playthrough | `-testCategory Bot` (about 2 minutes: one editor start, three Level 1 runs at 3x game speed) | `Logs/bot_playthrough.txt` (appended each run) |
| Compile check | any batchmode launch; grep the log for `error CS` | log |
| Audio check | `python3 Tools/audio/verify_audio.py` | ffprobe and volume report |
| FBX check | `python3 Tools/blender/fbx_inspect.py <file.fbx>` (prints node transforms, axes and take names); `Tools/blender/verify_env_fbx.py` | console report |

Em dash check used for docs: search `Docs/*.md` and `README.md` for the Unicode character U+2014; the result should be empty (it was empty on 2026-11-17).
