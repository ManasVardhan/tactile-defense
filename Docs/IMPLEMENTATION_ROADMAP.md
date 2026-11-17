# Tactile Wars Recreation: Implementation Roadmap

Eight phases from the owner's brief. All boxes start unchecked; a later pass ticks them against what actually exists. Each task is written to be checkable by opening a file, running a command, or viewing a screenshot.

Verification methods used below:
- **Compile**: `Unity -batchmode -nographics -quit -projectPath /Users/manasvardhan/TactileWars -logFile <log>` and confirm exit code 0 and no `error CS` in the log (Unity binary per ARCHITECTURE_CONTRACT; take the `.unity.lock` first, release after).
- **EditMode / PlayMode tests**: `-runTests -testPlatform EditMode|PlayMode -testResults <xml>` and read the XML pass counts.
- **Screenshot**: `TactileWars.EditorTools.Screenshotter.CaptureAll` writes `Docs/Screenshots/*.png`; view each with the Read tool and compare with the reference frames named.
- **Blender**: `blender -b -P Tools/blender/<script>.py` then check FBX and `Docs/Previews/<Asset>.png` exist and view them.
- **Static**: `grep`, `ls`, `wc` checks.

Rules for every phase: no em dashes in any project file; estimates stay in ScriptableObjects; do not claim verification that was not run.

## Phase 1: Research

Goal: sourced, labeled documents that guide the build.

Tasks
- [ ] `Docs/References/web/RESEARCH_MECHANICS.md` exists and every claim is tagged VERIFIED, INFERRED or UNVERIFIED.
- [ ] `Docs/References/VISUAL_ANALYSIS.md` exists and cites image filenames.
- [ ] Reference images present: `Docs/References/playstore/ps_01..ps_12`, `Docs/References/video_frames/f_0001..f_0098` and burst frames.
- [ ] `Docs/GAME_DESIGN_DOCUMENT.md` has a "Design decisions for this recreation" section with at least 20 numbered decisions.
- [ ] `Docs/GAMEPLAY_MECHANICS.md` has a value, status and ScriptableObject field for every numeric row.
- [ ] `Docs/VISUAL_STYLE_GUIDE.md` contains a palette table with hex values and the 2D versus 3D note.
- [ ] `Docs/ASSET_INVENTORY.md` lists every asset in the contract with status.
- [ ] `Docs/REFERENCE_COMPARISON.md` has all nine sections plus the cannot-verify and helpful-material lists.
- [ ] a grep for the Unicode em dash (U+2014) across `Docs/*.md` returns nothing.

Acceptance: all six docs exist, no em dashes, no estimate is presented as verified (spot check 10 numeric rows).
Verification: static grep and `wc -l`; manual spot check.

## Phase 2: Visual foundation

Goal: project skeleton, art pipeline, first models visible under the target camera.

Tasks
- [ ] Unity project opens at 6000.3.22f1 in batchmode with exit code 0.
- [ ] `ProjectSetup.Run` configures URP, layers 6 to 12 with the contract names, Input Handling Both, and tags.
- [ ] Assembly definitions `TactileWars.Runtime`, `TactileWars.Editor` and test assemblies exist.
- [ ] `Core/PointerInput.cs`, `Core/Pool<T>`, `Core/GameConfig` implemented.
- [ ] `Tools/blender/` has scripts for Soldier, Flag, and the environment set and each writes an FBX and `Docs/Previews/<Asset>.png`.
- [ ] Soldier.fbx has bones Root, Hips, Spine, Head, Arm.L, Arm.R, Hand.R, Leg.L, Leg.R, Gun, and takes Idle, Run, Shoot, Hit, Die, Cheer.
- [ ] Soldier under 2.5k triangles (check in Unity import stats or Blender).
- [ ] All material slot names match the contract (TeamColor, TeamColorDark, Skin, Dark, Metal, Accent, Paint, Wood, Leaf, LeafLight, Trunk, Rock, Sand, Glass).
- [ ] `AssetBuilder.BuildAll` creates materials, import settings, animator controller, prefabs.
- [ ] Hex ground shader and material created.
- [ ] `BattleCamera` orthographic at 35 degrees, orthoSize 14 from `CameraConfig`.
- [ ] A test scene shows 40 soldiers on the hex ground; screenshot saved.

Acceptance: screenshot shows an orthographic oblique view, about 28 soldier-heights tall, helmet-dominant soldiers, low-saturation ground.
Verification: batchmode compile; `Screenshotter.CaptureAll`; compare to `ps_12.png` for ratio, density and palette (VISUAL_STYLE_GUIDE section 9 checklist).

## Phase 3: Core playable battle

Goal: draw formations, fight, capture two points, win or lose.

Tasks
- [ ] `StrokeRecorder`, `PathResampler`, `ShapeRecognizer`, `FormationSolver`, `StrokeRenderer` implemented per GAMEPLAY_MECHANICS section 2.
- [ ] EditMode tests: resampled point spacing within 1 percent of target; line, circle, square strokes recognized from synthetic input; free stroke falls through; N slots equals soldier count.
- [ ] EditMode test: spacing along a stroke lies between 1.0 and 1.3 widths for a stroke long enough.
- [ ] `SquadController` tap-to-move keeps formation; `SoldierMotor` steering with separation.
- [ ] `Health`, `Targeting`, `Weapon`, `PaintProjectile` (pooled), `PaintSplatManager`, `DamageFeedback`.
- [ ] Soldier death sequence: ghost, puff, splat, event fired.
- [ ] `EnemySquad` with flag-bearer; bubble spawns on bearer death; tap pops and kills the squad.
- [ ] Structures: Turret, Mine, Tank, Cannon, Bunker, Tesla implemented with `StructureConfig` values.
- [ ] `BattleManager`: two sequential capture points, win on both captured, lose at zero soldiers.
- [ ] `ReinforcementMeter` fills from kills and summons a batch.
- [ ] `BattleEvents` static events; no gameplay class references UI.
- [ ] `BattleCamera` follows centroid, clamped to bounds, light shake.
- [ ] All stats read from ScriptableObjects (grep shows no magic combat numbers in runtime scripts).
- [ ] PlayMode test: scripted squad walks to point 1, captures, walks to point 2, captures, `BattleManager` reports Victory.
- [ ] PlayMode test: killing all player soldiers reports Defeat.
- [ ] PlayMode test: killing a flag-bearer spawns a bubble and popping it removes the whole squad.
- [ ] Battle.unity playable end to end with mouse input.

Acceptance: a full battle can be won and lost; tests pass; 60 fps target on M2 with 60 soldiers and 400 projectiles (measure frame time in a PlayMode test, record the average).
Verification: batchmode compile; EditMode and PlayMode results XML; screenshot of mid-battle for comparison with `f_0056.jpg` and `burst_B_030.jpg`.

## Phase 4: Visual polish

Goal: match the visual read of the references.

Tasks
- [ ] Squad ring with spiral blot, ring-team color, split color when contested.
- [ ] Stroke feedback: solid inner plus dashed outer line.
- [ ] Projectile dots with white core and team edge; muzzle flash.
- [ ] Persistent paint splats with a cap and fade of the oldest.
- [ ] Death ghost and puff; explosion starburst and dust; star coin pickup with combo "x" numbers.
- [ ] Bubble with team arc ring and pennant; capture flag plant.
- [ ] Blob shadows under all units.
- [ ] Toon shader with thin white helmet rim; helmet reads at 3.5 percent of screen height.
- [ ] Lighting and post per VISUAL_STYLE_GUIDE section 8.
- [ ] Props placed: pines, spike trees, tents, barricades, crates, walls, spawn pad decals.
- [ ] Off-screen helmet indicators clamped to the screen edge.
- [ ] Screenshot QA checklist (VISUAL_STYLE_GUIDE section 9) passes all 8 items.

Acceptance: side-by-side screenshot with `ps_12.png` shows matching framing, palette and effect vocabulary; team color is the most saturated element.
Verification: `Screenshotter.CaptureAll`; viewed against `ps_12.png`, `ps_06.png`, `f_0056.jpg`; sampled ground color within 10 percent of #C7B78D or #7C6F66 per biome.

## Phase 5: Base defense

Goal: editable bases with space and budget rules.

Tasks
- [ ] `BaseEditor.unity` shows the lane map in an editing view.
- [ ] Place, move and remove defenses from a palette; rotation optional.
- [ ] Space cap and budget cap enforced from `BaseEditorConfig`; placement refused with a message when exceeded.
- [ ] Caps rise with player level.
- [ ] Gold cost to buy defenses; consumed defenses must be rebought.
- [ ] Base saved and loaded as JSON in `Application.persistentDataPath`.
- [ ] "Test attack" launches Battle.unity against the player's own base.
- [ ] Seeded AI base generator respects the same caps; same seed gives the same base.
- [ ] EditMode tests: cap enforcement, save/load round trip, generator determinism.
- [ ] PlayMode test: a base built in the editor loads into Battle with the same defenses.

Acceptance: a player can build a base under the caps, save, quit, reload, and test-attack it.
Verification: EditMode and PlayMode XML; screenshot of the editor scene.

## Phase 6: UI and progression

Goal: menus, economy, research, result flow.

Tasks
- [ ] Boot, MainMenu scenes; scene flow Boot to MainMenu to Battle or BaseEditor and back.
- [ ] Battle HUD: pause tab, squad badge as reinforcement meter, POW badge; nothing else.
- [ ] Pause menu with resume, restart, quit.
- [ ] Opponent selection list with rank and reward preview.
- [ ] Result screen: victory card (gold, EXP, MEDALS, SURVIVORS, COMBOS, TIME, grade) and a defeat card.
- [ ] Gold, Prisms, XP, level in `Meta/*`; `SaveSystem` persists profile.
- [ ] Research list with level locks and gold costs; paying cannot unlock early.
- [ ] Faction picker for six colors; opponents never share the player's color.
- [ ] Squad rebuy cost applied after battle.
- [ ] OFL fonts imported with license texts present in `Assets/Art/Fonts`.
- [ ] EditMode tests: XP to level curve, research gating, reward formula, save/load.
- [ ] Screenshot of every screen.

Acceptance: a new profile can play battles, earn gold and XP, level up, unlock research, rebuy troops, and persist across restarts.
Verification: tests; screenshots compared with `f_0086.jpg`, `f_0091.jpg`, `f_0094.jpg` for layout vocabulary.

## Phase 7: Additional systems

Goal: factions, leagues, mercenary, offline online-layer.

Tasks
- [ ] Four leagues with rank points; league shown in profile.
- [ ] `Online/*` interfaces with a local offline provider (matchmaking, leaderboard, alliance, base repository).
- [ ] Synthetic leaderboard and alliance roster labeled offline.
- [ ] Daily gold gift and free Prism grants per the GDD decisions.
- [ ] One mercenary unit with one ability (stretch).
- [ ] Second biome (lava) with cracked hex ground.
- [ ] Synthesized audio: all SFX and two music loops from `Tools/audio`, wired through `Audio/*` with a mixer and volume setting.
- [ ] Optional rain VFX.
- [ ] EditMode tests: league promotion and demotion, matchmaking returns bases within the rank window.

Acceptance: all features reachable from the UI; audio plays on shot, hit, death, capture, victory.
Verification: tests; a PlayMode test that asserts audio sources triggered on key events; manual listen note recorded.

## Phase 8: Testing and refinement

Goal: stability, performance, honest final comparison.

Tasks
- [ ] Full EditMode and PlayMode suites pass with zero failures.
- [ ] Batchmode build compile with zero `error CS` and a recorded warning count.
- [ ] Performance run: 60 soldiers vs 60 enemies, 400 projectiles, 300 splats, average frame time recorded (target 16.6 ms on M2).
- [ ] Memory check: no unbounded growth over a 5 minute battle (pool and splat counts recorded).
- [ ] Soak test: 20 consecutive scripted battles with no exceptions in the log.
- [ ] Update `ASSET_INVENTORY.md` statuses against files on disk.
- [ ] Re-run `Screenshotter.CaptureAll` and write the final comparison against the reference list in `VISUAL_ANALYSIS.md` section 11.
- [ ] Update `REFERENCE_COMPARISON.md` deviations with what was actually built.
- [ ] Tune estimates (spacing, speeds, fire rate, camera pitch) by side-by-side playtest; record changed values and the reason in the GDD decisions table.
- [ ] Confirm no assets from the original are in `Assets/` (check file list and hashes against `Docs/References`).
- [ ] Confirm fonts have OFL texts; confirm project title cleared or changed for any distribution.
- [ ] `.unity.lock` removed and no Unity process left running.

Acceptance: every checked box can be shown with a log, XML, file or screenshot; remaining gaps are listed in a short "Known issues" note in `Docs/`.
Verification: all of the above commands; a final reviewer reads the docs and screenshots.
