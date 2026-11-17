# Testing

How to run each test suite and tool, what each covers, and the results of the final regression run on 2026-11-17.

All commands assume the project root as the working directory and `UNITY=/Applications/Unity/Hub/Editor/6000.3.22f1/Unity.app/Contents/MacOS/Unity`. Only one Unity instance may have the project open; the project convention is `mkdir .unity.lock` before launching and `rmdir .unity.lock` after. Do not pass `-nographics` for PlayMode (screenshots and perf sampling need a graphics device). The machine is an Apple M2 with 8 GB RAM, so run one Unity at a time.

## Final run, 2026-11-17

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

## EditMode suite (42 tests, about a second)

```
$UNITY -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
```

Files in `Assets/Tests/EditMode`:

| Fixture | Tests | Covers |
|---|---|---|
| `PathResamplerTests` | 5 | Path length (open and closed), equal arc-length resampling, closed loop spacing, Chaikin smoothing keeps endpoints, polyline sampling |
| `ShapeRecognizerTests` | 5 | Line, circle, square, free curve, closed-stroke detection |
| `FormationSolverTests` | 12 (7 plus 5 parameter cases) | Slot count and minimum spacing for several stroke lengths, spacing cap on long lines, extra rows for short lines, closed ring, filled inner rings, cluster for degenerate strokes, assignment is a permutation and non-crossing |
| `MetaTests` | 8 | Save round trip, corrupt save fallback to backup, version 1 migration, XP to level and level rewards, research purchase and level lock, mercenary unlock and hire, loadout from research, battle rewards saved |
| `BaseDefenseTests` | 12 | Placement validity and grid snap, zone limits, overlap, budget cap, space and type caps, research lock, purchase from stock and rebuy of wrecks, commit pays gold and survives restart, defense results consume placements and tests change nothing, simulated raid determinism and label, NavGrid routing without allocations, recognizer sequence line then arc then circle |

## PlayMode suite (30 tests, about 7.5 minutes)

```
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
```

Run a single category by adding `-testCategory <name>`; categories: `Screenshots`, `UIScreenshots`, `DefenseScreenshots`, `Lineup`, `Perf`, `Bot`. Files in `Assets/Tests/PlayMode`:

| Fixture | Tests | Covers |
|---|---|---|
| `AnimationStateTests` | 1 | Animator states play at the right moments |
| `BattlePlayTests` | 5 | Drag stroke and soldiers reach slots, advancing into an enemy squad fires and damages, capture progress, bubble tap wipes a squad, reinforcements when the meter is full |
| `DefenseBattleTests` | 9 | Defense battle runs to an outcome and records consumption, attacker AI forms lines, rings and clusters, tesla chain hits, sniper aims and hits, geyser erupts, wall routing through a gate, stroke sequence recognition, counterattack after first capture, base editor place, validate, save and survive restart |
| `MetaFlowTests` | 4 | Squad size upgrade changes spawned count, generated base deploys layout and teams, full flow Boot to menu to battle to victory to rewards to return, pointer over HUD does not start a stroke |
| `BotPlaythroughTests` (category Bot) | 4 | Scripted bot plays base 1 (asserted win, under half the squad lost), base 5 researched, base 10 with and without research (outcomes logged, not asserted) |
| `PerfCapture`, `DefensePerfCapture` (Perf) | 2 | Frame timing and render counters for the attack and the heavy defense battle |
| `ScreenshotCapture` (Screenshots) | 1 | Gameplay captures into `Docs/Screenshots` |
| `UIScreenshotCapture` (UIScreenshots) | 1 | Every menu and HUD screen |
| `DefenseScreenshotCapture` (DefenseScreenshots) | 2 | Base editor, defense battle and map variant captures |
| `SoldierLineupCapture` (Lineup) | 1 | Soldier lean lineups |

The capture and perf tests pass as long as they run; they assert little. Treat their PNG and text output as the evidence, and look at the images.

## Tools

| Tool | Command or menu | Output |
|---|---|---|
| Project validator | `$UNITY -batchmode -projectPath . -executeMethod TactileWars.EditorTools.ProjectValidator.Run -quit -logFile Logs/validate.log` (menu TactileWars, Validate Project) | `Logs/validation.txt` (scans scenes, prefabs and materials for missing script references, broken or missing materials and shaders, missing textures and meshes) |
| Screenshots | menu TactileWars, Capture Screenshots, or `-runTests -testPlatform PlayMode -testCategory Screenshots` | `Docs/Screenshots/*.png` |
| UI screenshots | menu Capture UI Screenshots, or category `UIScreenshots` | `Docs/Screenshots/ui_*.png` |
| Perf sampler | menu Measure Frame Time, or category `Perf` | `Logs/perf_battle.txt`, `Logs/perf_defense.txt` |
| Bot playthrough | category `Bot` | `Logs/bot_playthrough.txt` (appended each run) |
| Compile check | any batchmode launch; grep the log for `error CS` | log |
| Audio check | `python3 Tools/audio/verify_audio.py` | ffprobe and volume report |
| FBX check | `python3 Tools/blender/fbx_inspect.py <file.fbx>` (prints node transforms, axes and take names); `Tools/blender/verify_env_fbx.py` | console report |

Em dash check used for docs: search `Docs/*.md` and `README.md` for the Unicode character U+2014; the result should be empty (it was empty on 2026-11-17).
