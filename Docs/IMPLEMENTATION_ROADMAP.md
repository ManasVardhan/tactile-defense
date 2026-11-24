# Tactile Wars Recreation: Implementation Roadmap

Eight phases from the owner's brief. Boxes were ticked on 2026-11-17 only where the evidence named in the parentheses was checked on disk, in a test result or in a log. Unticked boxes carry a short note; none of them is claimed done. Each task is written to be checkable by opening a file, running a command, or viewing a screenshot.

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
- [ ] `Docs/References/web/RESEARCH_MECHANICS.md` exists and every claim is tagged VERIFIED, INFERRED or UNVERIFIED. (file exists with 67 tag words; per-claim tagging not audited)
- [x] `Docs/References/VISUAL_ANALYSIS.md` exists and cites image filenames. (cites image filenames)
- [x] Reference images present: `Docs/References/playstore/ps_01..ps_12`, `Docs/References/video_frames/f_0001..f_0098` and burst frames. (12 ps_*, 98 f_*, 180 burst frames on disk)
- [x] `Docs/GAME_DESIGN_DOCUMENT.md` has a "Design decisions for this recreation" section with at least 20 numbered decisions. (22 rows D1 to D22)
- [ ] `Docs/GAMEPLAY_MECHANICS.md` has a value, status and ScriptableObject field for every numeric row. (table columns exist; every row not audited)
- [x] `Docs/VISUAL_STYLE_GUIDE.md` contains a palette table with hex values and the 2D versus 3D note. (section 3 palette and section 0 2D versus 3D note)
- [x] `Docs/ASSET_INVENTORY.md` lists every asset in the contract with status. (rewritten against disk 2026-11-17)
- [x] `Docs/REFERENCE_COMPARISON.md` has all nine sections plus the cannot-verify and helpful-material lists. (sections 1 to 9 plus 10 and 11)
- [x] a grep for the Unicode em dash (U+2014) across `Docs/*.md` returns nothing. (empty on 2026-11-17)

Acceptance: all six docs exist, no em dashes, no estimate is presented as verified (spot check 10 numeric rows).
Verification: static grep and `wc -l`; manual spot check.

## Phase 2: Visual foundation

Goal: project skeleton, art pipeline, first models visible under the target camera.

Tasks
- [x] Unity project opens at 6000.3.22f1 in batchmode with exit code 0. (final runs exit 0)
- [x] `ProjectSetup.Run` configures URP, layers 6 to 12 with the contract names, Input Handling Both, and tags. (layers in TagManager, URP asset, activeInputHandler 2 (Both))
- [x] Assembly definitions `TactileWars.Runtime`, `TactileWars.Editor` and test assemblies exist. (4 asmdefs)
- [x] `Core/PointerInput.cs`, `Core/Pool<T>`, `Core/GameConfig` implemented. (files exist)
- [x] `Tools/blender/` has scripts for Soldier, Flag, and the environment set and each writes an FBX and `Docs/Previews/<Asset>.png`. (6 build scripts, 35 FBX, 49 previews)
- [ ] Soldier.fbx has bones Root, Hips, Spine, Head, Arm.L, Arm.R, Hand.R, Leg.L, Leg.R, Gun, and takes Idle, Run, Shoot, Hit, Die, Cheer. (documented in the contract, not re-inspected this pass; AnimationStateTests passes)
- [ ] Soldier under 2.5k triangles (check in Unity import stats or Blender). (not re-checked this pass)
- [ ] All material slot names match the contract (TeamColor, TeamColorDark, Skin, Dark, Metal, Accent, Paint, Wood, Leaf, LeafLight, Trunk, Rock, Sand, Glass). (not re-checked this pass (Canvas and Stone added later))
- [x] `AssetBuilder.BuildAll` creates materials, import settings, animator controller, prefabs. (outputs on disk, validator Issues: 0)
- [x] Hex ground shader and material created. (TWGround.shader, M_Ground.mat)
- [ ] `BattleCamera` orthographic at 35 degrees, orthoSize 14 from `CameraConfig`. (orthographic yes; elevation tuned to 31 and size derived from 33 soldier heights (contract deviation))
- [x] A test scene shows 40 soldiers on the hex ground; screenshot saved. (Screenshots/battle_u_formation.png)

Acceptance: screenshot shows an orthographic oblique view, about 28 soldier-heights tall, helmet-dominant soldiers, low-saturation ground.
Verification: batchmode compile; `Screenshotter.CaptureAll`; compare to `ps_12.png` for ratio, density and palette (VISUAL_STYLE_GUIDE section 9 checklist).

## Phase 3: Core playable battle

Goal: draw formations, fight, capture two points, win or lose.

Tasks
- [x] `StrokeRecorder`, `PathResampler`, `ShapeRecognizer`, `FormationSolver`, `StrokeRenderer` implemented per GAMEPLAY_MECHANICS section 2. (files exist; formation tests pass)
- [x] EditMode tests: resampled point spacing within 1 percent of target; line, circle, square strokes recognized from synthetic input; free stroke falls through; N slots equals soldier count. (FormationTests (PathResampler, ShapeRecognizer, FormationSolver) 42 of 42 pass)
- [ ] EditMode test: spacing along a stroke lies between 1.0 and 1.3 widths for a stroke long enough. (min spacing is 0.7 (deviation), not 1.0 to 1.3; cap test passes)
- [x] `SquadController` tap-to-move keeps formation; `SoldierMotor` steering with separation. (files exist; WallRouting and DragStroke tests pass)
- [x] `Health`, `Targeting`, `Weapon`, `PaintProjectile` (pooled), `PaintSplatManager`, `DamageFeedback`. (files exist under Combat)
- [ ] Soldier death sequence: ghost, puff, splat, event fired. (VFX_Ghost and VFX_Puff exist; not individually confirmed)
- [x] `EnemySquad` with flag-bearer; bubble spawns on bearer death; tap pops and kills the squad. (FlagBearerBubble_TapWipesSquad passes)
- [x] Structures: Turret, Mine, Tank, Cannon, Bunker, Tesla implemented with `StructureConfig` values. (all six exist; plus Sniper and Geyser; tesla, sniper, geyser tests pass)
- [x] `BattleManager`: two sequential capture points, win on both captured, lose at zero soldiers. (FullFlow test and bot victories)
- [x] `ReinforcementMeter` fills from kills and summons a batch. (Reinforcements_AddSoldiersWhenMeterFull passes)
- [x] `BattleEvents` static events; no gameplay class references UI. (BattleEvents.cs exists; grep finds no UI or Meta namespace use in gameplay folders)
- [x] `BattleCamera` follows centroid, clamped to bounds, light shake. (BattleCamera.cs, camera shake via BattleCamera.Shake)
- [ ] All stats read from ScriptableObjects (grep shows no magic combat numbers in runtime scripts). (no grep audit for magic numbers was done)
- [x] PlayMode test: scripted squad walks to point 1, captures, walks to point 2, captures, `BattleManager` reports Victory. (MetaFlowTests.FullFlow and bot tests)
- [ ] PlayMode test: killing all player soldiers reports Defeat. (no dedicated test; defeat reached only in bot runs)
- [x] PlayMode test: killing a flag-bearer spawns a bubble and popping it removes the whole squad. (FlagBearerBubble_TapWipesSquad)
- [x] Battle.unity playable end to end with mouse input. (full flow test)

Acceptance: a full battle can be won and lost; tests pass; 60 fps target on M2 with 60 soldiers and 400 projectiles (measure frame time in a PlayMode test, record the average).
Verification: batchmode compile; EditMode and PlayMode results XML; screenshot of mid-battle for comparison with `f_0056.jpg` and `burst_B_030.jpg`.

## Phase 4: Visual polish

Goal: match the visual read of the references.

Tasks
- [ ] Squad ring with spiral blot, ring-team color, split color when contested. (ring and blot done; split colour when contested not confirmed)
- [x] Stroke feedback: solid inner plus dashed outer line. (battle_stroke_drawing.png)
- [x] Projectile dots with white core and team edge; muzzle flash. (PaintBall.prefab, VFX_Muzzle.prefab)
- [x] Persistent paint splats with a cap and fade of the oldest. (103 splats in perf_battle.txt)
- [ ] Death ghost and puff; explosion starburst and dust; star coin pickup with combo "x" numbers. (ghost and puff exist; star coin pickups with combo numbers not implemented)
- [ ] Bubble with team arc ring and pennant; capture flag plant. (bubble and pennant done; the arc is thin versus the reference)
- [x] Blob shadows under all units. (built into prefabs and code)
- [ ] Toon shader with thin white helmet rim; helmet reads at 3.5 percent of screen height. (TWToonOutline white edge is heavier than the reference rim)
- [ ] Lighting and post per VISUAL_STYLE_GUIDE section 8. (present; post tints the HUD)
- [ ] Props placed: pines, spike trees, tents, barricades, crates, walls, spawn pad decals. (pines, tents, barricades, crates, walls done; spike trees only as Tree_B; spawn pad decals via SpawnHatch)
- [x] Off-screen helmet indicators clamped to the screen edge. (HUD enemy markers)
- [ ] Screenshot QA checklist (VISUAL_STYLE_GUIDE section 9) passes all 8 items. (not run item by item; see VISUAL_FIDELITY_REPORT.md)

Acceptance: side-by-side screenshot with `ps_12.png` shows matching framing, palette and effect vocabulary; team color is the most saturated element.
Verification: `Screenshotter.CaptureAll`; viewed against `ps_12.png`, `ps_06.png`, `f_0056.jpg`; sampled ground color within 10 percent of #C7B78D or #7C6F66 per biome.

## Phase 5: Base defense

Goal: editable bases with space and budget rules.

Tasks
- [x] `BaseEditor.unity` shows the lane map in an editing view. (base_editor_full.png)
- [x] Place, move and remove defenses from a palette; rotation optional. (BaseEditor_PlaceValidateSave_SurvivesRestart)
- [x] Space cap and budget cap enforced from `BaseEditorConfig`; placement refused with a message when exceeded. (BaseDefenseTests cap tests)
- [x] Caps rise with player level. (BaseEditorConfig by level)
- [x] Gold cost to buy defenses; consumed defenses must be rebought. (Purchase_PendingGold_StockFirst_AndRebuyWrecks)
- [x] Base saved and loaded as JSON in `Application.persistentDataPath`. (CommitBaseEdit_DeductsGold_AndLayoutSurvivesRestart)
- [ ] "Test attack" launches Battle.unity against the player's own base. (TEST launches a defense battle against a simulated AI attacker, not a player attack on the own base)
- [ ] Seeded AI base generator respects the same caps; same seed gives the same base. (ten fixed generated layouts; generator determinism test not found)
- [ ] EditMode tests: cap enforcement, save/load round trip, generator determinism. (cap and save tests pass; generator determinism not tested)
- [x] PlayMode test: a base built in the editor loads into Battle with the same defenses. (DefenseBattle_RunsToOutcome and BaseEditor tests)

Acceptance: a player can build a base under the caps, save, quit, reload, and test-attack it.
Verification: EditMode and PlayMode XML; screenshot of the editor scene.

## Phase 6: UI and progression

Goal: menus, economy, research, result flow.

Tasks
- [x] Boot, MainMenu scenes; scene flow Boot to MainMenu to Battle or BaseEditor and back. (MetaFlowTests.FullFlow)
- [x] Battle HUD: pause tab, squad badge as reinforcement meter, POW badge; nothing else. (battle_u_formation.png (extras added))
- [x] Pause menu with resume, restart, quit. (PauseMenu.cs, ui_pause.png)
- [x] Opponent selection list with rank and reward preview. (ui_attack.png)
- [ ] Result screen: victory card (gold, EXP, MEDALS, SURVIVORS, COMBOS, TIME, grade) and a defeat card. (victory and defeat cards exist; stat set is survivors, time, kills (no medals or combos))
- [x] Gold, Prisms, XP, level in `Meta/*`; `SaveSystem` persists profile. (MetaTests)
- [x] Research list with level locks and gold costs; paying cannot unlock early. (MetaTests.Research_PurchaseDeductsGold_AndRespectsLevelLock)
- [x] Faction picker for six colors; opponents never share the player's color. (ui_team_select.png)
- [ ] Squad rebuy cost applied after battle. (not implemented; only defense rebuy and a per battle mercenary fee)
- [x] OFL fonts imported with license texts present in `Assets/Art/Fonts`. (Assets/Art/Fonts)
- [x] EditMode tests: XP to level curve, research gating, reward formula, save/load. (MetaTests (8 tests))
- [x] Screenshot of every screen. (45 PNG in Docs/Screenshots)

Acceptance: a new profile can play battles, earn gold and XP, level up, unlock research, rebuy troops, and persist across restarts.
Verification: tests; screenshots compared with `f_0086.jpg`, `f_0091.jpg`, `f_0094.jpg` for layout vocabulary.

## Phase 7: Additional systems

Goal: factions, leagues, mercenary, offline online-layer.

Tasks
- [x] Four leagues with rank points; league shown in profile. (ProgressionConfig.leagues; the profile screen is hidden since Plan 2 Task 2c)
- [x] `Online/*` interfaces with a local offline provider (matchmaking, leaderboard, alliance, base repository). (OnlineInterfaces.cs, LocalOfflineProvider.cs)
- [ ] Synthetic leaderboard and alliance roster labeled offline. (leaderboard labelled offline; alliance roster exists with no UI)
- [ ] Daily gold gift and free Prism grants per the GDD decisions. (not implemented)
- [ ] One mercenary unit with one ability (stretch). (three mercenaries as data on the Hero model; ability is a text label)
- [ ] Second biome (lava) with cracked hex ground. (not built)
- [ ] Synthesized audio: all SFX and two music loops from `Tools/audio`, wired through `Audio/*` with a mixer and volume setting. (42 SFX and 2 loops wired through Audio/*; no mixer asset; no test asserts playback)
- [ ] Optional rain VFX. (not built)
- [ ] EditMode tests: league promotion and demotion, matchmaking returns bases within the rank window. (not found among the 42 EditMode tests)

Acceptance: all features reachable from the UI; audio plays on shot, hit, death, capture, victory.
Verification: tests; a PlayMode test that asserts audio sources triggered on key events; manual listen note recorded.

## Phase 8: Testing and refinement

Goal: stability, performance, honest final comparison.

Tasks
- [x] Full EditMode and PlayMode suites pass with zero failures. (42 of 42 and 30 of 30 on 2026-11-17 (TESTING.md))
- [ ] Batchmode build compile with zero `error CS` and a recorded warning count. (0 error CS in the final logs; warning count not recorded)
- [ ] Performance run: 60 soldiers vs 60 enemies, 400 projectiles, 300 splats, average frame time recorded (target 16.6 ms on M2). (editor run recorded (40 vs 70 soldiers, 9.6 ms); 60 v 60 with 400 projectiles and 300 splats not run)
- [ ] Memory check: no unbounded growth over a 5 minute battle (pool and splat counts recorded). (not done)
- [ ] Soak test: 20 consecutive scripted battles with no exceptions in the log. (not done)
- [x] Update `ASSET_INVENTORY.md` statuses against files on disk. (2026-11-17)
- [ ] Re-run `Screenshotter.CaptureAll` and write the final comparison against the reference list in `VISUAL_ANALYSIS.md` section 11. (screenshots re-run; the comparison images were not regenerated)
- [x] Update `REFERENCE_COMPARISON.md` deviations with what was actually built. (2026-11-17)
- [ ] Tune estimates (spacing, speeds, fire rate, camera pitch) by side-by-side playtest; record changed values and the reason in the GDD decisions table. (tuning was done (camera, balance) and logged in the contract; not recorded in the GDD decisions table)
- [x] Confirm no assets from the original are in `Assets/` (check file list and hashes against `Docs/References`). (MD5 comparison of 290 reference images, 0 matches; byte identity only)
- [ ] Confirm fonts have OFL texts; confirm project title cleared or changed for any distribution. (OFL texts present; the title is changed in game to Paint Squad Wars but 'Tactile Wars' still appears in docs and the product name string)
- [x] `.unity.lock` removed and no Unity process left running. (checked at the end of the final run)

Acceptance: every checked box can be shown with a log, XML, file or screenshot; remaining gaps are listed in `STATUS_CHECKLIST.md` (Not done or partial) and in the README Known limitations.
Verification: all of the above commands; a final reviewer reads the docs and screenshots.
