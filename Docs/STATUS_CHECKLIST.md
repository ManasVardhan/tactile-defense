# Status checklist against the owner's brief

Date: 2026-11-17. Every status below is backed by a file on disk, a test name, a screenshot or a log named in the Evidence column. The brief itself was not available as a file in the project, so requirement lines are reconstructed from `GAME_DESIGN_DOCUMENT.md`, `IMPLEMENTATION_ROADMAP.md` and `ARCHITECTURE_CONTRACT.md`, which mirror it closely; section numbers and titles follow the brief (there is no section 17). Statuses: **Done** (exists and evidenced), **Partial** (exists with a stated gap), **Not done**. Test counts refer to the final run in `TESTING.md`.

## Summary

| Status | Count |
|---|---|
| Done | 109 |
| Partial | 26 |
| Not done | 28 |
| Total lines | 163 |

| Section | Done | Partial | Not done |
|---|---|---|---|
| 1 Primary reference research | 4 | 1 | 0 |
| 2 Visual quality | 6 | 3 | 2 |
| 3 Asset production pipeline | 11 | 1 | 1 |
| 4 Formation drawing | 5 | 1 | 2 |
| 5 Squad movement | 7 | 0 | 0 |
| 6 Combat system | 9 | 1 | 1 |
| 7 Base attacks and defense | 9 | 2 | 2 |
| 8 Camera system | 4 | 1 | 1 |
| 9 User interface | 9 | 2 | 4 |
| 10 Progression and meta-game | 7 | 1 | 2 |
| 11 Multiplayer and online features | 1 | 3 | 2 |
| 12 Unity project requirements | 7 | 1 | 2 |
| 13 Performance | 2 | 1 | 3 |
| 14 Audio and animation polish | 6 | 1 | 2 |
| 15 Visual comparison and quality control | 4 | 1 | 1 |
| 16 Development phases | 5 | 3 | 0 |
| 18 Deliverables | 8 | 1 | 2 |
| 19 Definition of done | 5 | 2 | 1 |

## Not done or partial (gaps first)

| Section | Requirement | Status | Reason or evidence |
|---|---|---|---|
| 2 | Match the original's grey lava biome | Not done | Only the sand biome and three sand map layouts exist; no lava ground or crack glow |
| 2 | Rain or weather VFX | Not done | No rain prefab or code (ps_06 shows rain) |
| 3 | Defense models for every type named in the sources (heli-tower, tracker, vannon, robotank, trapped slab) | Not done | No model or code for these five |
| 4 | Special arrow formation behaviour | Not done | Treated as a free shape (REFERENCE_COMPARISON section 3) |
| 4 | Manual aiming by finger | Not done | Design decision D1: auto-fire only; whether the original had manual aim is unverified |
| 6 | Star coin pickups with x1 and x2 combo numbers | Not done | No in-battle pickups or combo counter; only a coin burst VFX on victory (VFX_Coins) |
| 7 | Real attacks by other players on your base | Not done | No server, no real players |
| 7 | Territory extensions and extra bases | Not done | One player base only |
| 8 | Pinch zoom or free camera | Not done | Not implemented (base editor has mouse wheel and overview button only) |
| 9 | Pre-battle VS splash with tip | Not done | No VS card; the attack list goes straight into battle |
| 9 | World or Colors War map screen | Not done | A list of AI base cards replaces the map |
| 9 | Alliance screen | Not done | Only the IAllianceService stub exists, no screen |
| 9 | UI excluded from post-processing | Not done | HUD and menus are tinted by the post stack; overlay camera attempt was reverted |
| 10 | Daily gold gift or free prism grants | Not done | No daily reward code |
| 10 | Gold cost to rebuy lost soldiers between battles | Not done | No per-soldier rebuy (GAMEPLAY_MECHANICS lists it; code has defense rebuy and a per battle mercenary fee only) |
| 11 | Real multiplayer, matchmaking servers, accounts | Not done | No networking code |
| 11 | Chat, chests and keys, ads, real-money store | Not done | Out of scope per GDD section 16 |
| 12 | Android and iOS player builds | Not done | Build support modules are not installed; no device build made or tested |
| 12 | Player settings: bundle id, icon, signing | Not done | Not configured; no app icon asset |
| 13 | 60 fps on a phone | Not done | Never profiled on a device |
| 13 | Draw calls and triangles suitable for mobile | Not done | About 4990 draw calls and 1.3 M triangles in the attack battle (PERFORMANCE_REPORT.md) |
| 13 | Memory growth over a long battle and soak test | Not done | No 5 minute memory check or 20 battle soak test was written or run |
| 14 | Audio reviewed by a human listener | Not done | Never listened to; levels chosen by measurement only |
| 14 | Haptics | Not done | No vibration code |
| 15 | Comparison images regenerated from the final screenshots | Not done | The 7 comparison PNG are from the earlier pass; the final run refreshes Screenshots but not Comparisons |
| 18 | Android build | Not done | Not built |
| 18 | iOS build | Not done | Not built |
| 19 | Runs on a mobile device | Not done | No device build exists |
| 1 | Native-resolution gameplay timing and motion data | Partial | Only a 360p upscaled let's play (v1.2.2) and 10 fps bursts; timings are estimates |
| 2 | Chibi helmet-dominant soldiers readable at about 3.5 percent of screen height | Partial | 22 to 28 px tall at 720p (contract); eyes rarely resolve and white outline is heavy (Comparisons/NOTES.md 03) |
| 2 | Gentle lighting and post-processing | Partial | TW_BattleVolume.asset present; the stack also tints the HUD (Comparisons/NOTES.md 05a/05b) |
| 2 | Overall closeness to the reference | Partial | Docs/VISUAL_FIDELITY_REPORT.md: six of seven screens 'compared with visible differences' |
| 3 | Mercenary model | Partial | No Mercenary.fbx; mercenaries reuse Hero.fbx |
| 4 | Spacing along the stroke about 1.0 to 1.3 soldier widths | Partial | Solver min spacing 0.7 (deviation, helmets overlap otherwise), max cap tested (FormationSolverTests.LongLine_SpacingCappedAtMax); original spacing is an estimate |
| 6 | Mercenary with a special ability | Partial | Three MercenaryDef assets join as an extra Hero unit with own stats (EditMode MetaTests.Mercenary_UnlockAndHire); the ability is a text label, not scripted behaviour |
| 7 | Defense types named by the sources | Partial | Done: mine, turret, tank, cannon, bunker, tesla, sniper, geyser, defender squad. Not done: heli-tower, tracker, vannon, robotank, trapped slab |
| 7 | Offline raids on your base | Partial | Simulated by RaidSimulator, labelled SIMULATED in the UI; EditMode BaseDefenseTests.SimulatedRaid_IsDeterministic_Labeled_AndApplied |
| 8 | Camera framing matches the reference | Partial | Elevation 31 and 33 soldier-heights are tuned, but the forest border frames differently (VISUAL_FIDELITY_REPORT section 1) |
| 9 | HUD limited to what the original shows | Partial | Extras: capture pill, objective marker, soldier count, POW always visible |
| 9 | Safe area and aspect handling | Partial | UI/Kit/SafeArea.cs, AdaptiveCanvasScaler; tall captures Screenshots/ui_main_menu_tall.png, battle_hud_tall.png; no device test |
| 10 | Balanced difficulty curve across ten bases | Partial | Bot wins b01 and b05, loses b10 with and without research (Logs/bot_playthrough.txt); all values are estimates |
| 11 | Leaderboard | Partial | Synthetic board mixed with your league points (UI/Menu/ProfileScreen.cs), labelled offline |
| 11 | Alliances | Partial | Synthetic roster from LocalOfflineProvider.GetAlliances; no UI |
| 11 | Asynchronous PvP raids | Partial | Simulated only (RaidSimulator), labelled as not a real player |
| 12 | Input System with mouse and touch | Partial | Input System 1.20.0, one PointerInput path; never tested with real touch hardware |
| 13 | Allocation free hot paths | Partial | Pools and preallocated buffers; about 520 bytes per frame GC in the editor, source not isolated |
| 14 | Audio wired to gameplay and UI events | Partial | AudioManager, SoundLibrary, MusicPlayer; no test asserts that sources fire |
| 15 | Iterate until the build closely matches the reference | Partial | One fix pass done (lean, outlines, ring, splats); lava biome, enemy density, bubble arc, victory layout remain |
| 16 | Phase 4 Visual polish | Partial | Done in part; lava biome and rain remain |
| 16 | Phase 7 Additional systems | Partial | Leagues, offline services, mercenary, audio done; lava biome, rain, daily gift not done |
| 16 | Phase 8 Testing and refinement | Partial | Suites run (see TESTING.md); soak and memory tests and device profiling not done |
| 18 | Performance report | Partial | Docs/PERFORMANCE_REPORT.md covers the editor only |
| 19 | Visual result closely matches the reference | Partial | VISUAL_FIDELITY_REPORT.md; known differences listed |
| 19 | Every feature in the brief implemented | Partial | See the Not done or partial list above |

## 1 Primary reference research

| Requirement | Status | Evidence |
|---|---|---|
| Research original mechanics from real sources | Done | Docs/References/web/RESEARCH_MECHANICS.md (67 VERIFIED/INFERRED/UNVERIFIED tags) |
| Analyze visuals from real screenshots and video frames | Done | Docs/References/VISUAL_ANALYSIS.md cites image filenames per claim |
| Collect reference imagery | Done | Docs/References: 12 Play Store images, 98 video frames (f_*), 180 burst frames |
| Label estimates and unknowns honestly | Done | GDD evidence labels, GAMEPLAY_MECHANICS status column, REFERENCE_COMPARISON section 10 (cannot verify list) |
| Native-resolution gameplay timing and motion data | Partial | Only a 360p upscaled let's play (v1.2.2) and 10 fps bursts; timings are estimates |

## 2 Visual quality

| Requirement | Status | Evidence |
|---|---|---|
| 3D models under an orthographic camera that reproduces the 2D read | Done | CameraConfig (orthographic, 31 degrees); Screenshots/battle_u_formation.png |
| Chibi helmet-dominant soldiers readable at about 3.5 percent of screen height | Partial | 22 to 28 px tall at 720p (contract); eyes rarely resolve and white outline is heavy (Comparisons/NOTES.md 03) |
| Team colours are the only strongly saturated colours on low-saturation ground | Done | Screenshots/battle_u_formation.png, base_defense_battle.png |
| Hex-tiled ground | Done | Assets/Art/Shaders/TWGround.shader, M_Ground.mat |
| Props set: pines, tents, barricades, crates, walls, rocks, flowers | Done | 35 FBX; Assets/Prefabs/Environment (23 prefabs); Screenshots/battle_u_formation.png |
| Soft blob shadows, no health bars or damage numbers | Done | Blob shadow built by code and prefabs; no health bar code in battle HUD; screenshots |
| Paint vocabulary: ring with spiral blot, dotted projectiles, persistent splats, death ghost and puff | Done | Screenshots/battle_formation_circle.png (ring, blot, splats, dots); VFX_Ghost.prefab, VFX_Puff.prefab (ghost not individually checked visually) |
| Match the original's grey lava biome | Not done | Only the sand biome and three sand map layouts exist; no lava ground or crack glow |
| Rain or weather VFX | Not done | No rain prefab or code (ps_06 shows rain) |
| Gentle lighting and post-processing | Partial | TW_BattleVolume.asset present; the stack also tints the HUD (Comparisons/NOTES.md 05a/05b) |
| Overall closeness to the reference | Partial | Docs/VISUAL_FIDELITY_REPORT.md: six of seven screens 'compared with visible differences' |

## 3 Asset production pipeline

| Requirement | Status | Evidence |
|---|---|---|
| Scripted Blender modelling exporting FBX | Done | Tools/blender: 6 build scripts plus helpers; 35 FBX under Assets/Art/Models |
| Rigged soldier with takes Idle, Run, Shoot, Hit, Die, Cheer | Done | Soldier.fbx, Soldier.controller; PlayMode AnimationStateTests; contract deviation notes (bones and takes not re-inspected in this pass) |
| Structures and environment set | Done | 15 structure and 16 environment FBX on disk |
| Mercenary model | Partial | No Mercenary.fbx; mercenaries reuse Hero.fbx |
| Preview render per model | Done | Docs/Previews: 49 PNG, every FBX has one (checked) |
| Material slots remapped and materials, prefabs, animator built by editor automation | Done | AssetBuilder; 140 materials, 48 prefabs; Logs/validation.txt |
| Generated textures at most 1024 px | Done | 19 textures in Assets/Art/Textures, none larger than 1024 (checked) |
| UI art generated, no downloaded art | Done | Assets/Art/UI: 19 shapes, 46 icons, 84 renders (UIArtBuilder) |
| Synthesized audio, no samples | Done | Tools/audio scripts; 42 WAV and 2 OGG |
| OFL fonts with license texts | Done | Assets/Art/Fonts/LICENSES.md, OFL_LilitaOne.txt, OFL_Fredoka.txt |
| No assets from the original game | Done | MD5 check of 290 reference images against all Assets and Docs images: 0 matches (byte identity only) |
| Defense models for every type named in the sources (heli-tower, tracker, vannon, robotank, trapped slab) | Not done | No model or code for these five |
| Per asset license and source inventory | Done | Docs/ASSET_INVENTORY.md (rewritten against disk this pass) |

## 4 Formation drawing

| Requirement | Status | Evidence |
|---|---|---|
| Drag to draw a stroke and soldiers reform along it | Done | PlayMode BattlePlayTests.DragStroke_SoldiersReachTheirSlots |
| Recognise line, circle, square and free shapes | Done | EditMode ShapeRecognizerTests; PlayMode DefenseBattleTests.StrokeSequence_LineThenArcThenCircle_AreRecognizedEachTime |
| Spacing along the stroke about 1.0 to 1.3 soldier widths | Partial | Solver min spacing 0.7 (deviation, helmets overlap otherwise), max cap tested (FormationSolverTests.LongLine_SpacingCappedAtMax); original spacing is an estimate |
| Closed shapes and filled shapes | Done | FormationSolverTests.CircleStroke_ClosedRingWithMinSpacing, SmallCircle_FillsInnerRings |
| Stroke feedback: solid inner line plus dashed outer line | Done | StrokeRenderer; Screenshots/battle_stroke_drawing.png (glow ribbon is an extra not in the reference) |
| Live single-file chain while drawing | Done | SquadController.ExtendLiveTail (contract Phase 4 note); Screenshots/battle_stroke_drawing.png; no dedicated test |
| Special arrow formation behaviour | Not done | Treated as a free shape (REFERENCE_COMPARISON section 3) |
| Manual aiming by finger | Not done | Design decision D1: auto-fire only; whether the original had manual aim is unverified |

## 5 Squad movement

| Requirement | Status | Evidence |
|---|---|---|
| Tap to move with formation preserved | Done | SquadController.MoveFormationTo; PlayMode DefenseBattleTests.WallRouting_TapBehindWall_SquadPassesThroughGate |
| Obstacle routing around walls | Done | Squad/NavGrid; EditMode BaseDefenseTests.NavGrid_RoutesThroughGate_WithoutAllocating |
| Steering with separation | Done | Squad/SoldierMotor (no isolated test) |
| Squad anchor ring that lags the squad | Done | SquadRing, SquadConfig; screenshots |
| Movement feel: hop, squash, recoil, cheer on capture | Done | SquadController.HopAll, CheerFor (contract Phase 4 note; no automated test) |
| Leader unit | Done | Hero prefab, one per squad |
| Squad size 40 start, 60 cap via research | Done | SquadConfig.soldierCount 40; PlayMode MetaFlowTests.SquadSizeUpgrade_ChangesSpawnedSoldierCount; bot start squads 40 to 60 |

## 6 Combat system

| Requirement | Status | Evidence |
|---|---|---|
| Auto-targeting and firing | Done | PlayMode BattlePlayTests.AdvanceIntoEnemySquad_FiresAndDamagesEnemies |
| Pooled paint projectiles with muzzle puffs | Done | ProjectileManager, PaintBall.prefab, VFX_Muzzle.prefab; Logs/perf_battle.txt peak 47 projectiles |
| Persistent paint splats with a cap | Done | PaintSplatManager; Logs/perf_battle.txt 103 splats |
| Death ghost, puff, splat | Done | VFX_Ghost, VFX_Puff prefabs; AnimationStateTests covers the Die state (visual not individually reviewed) |
| Flag-bearer bubble: tap wipes the squad | Done | PlayMode BattlePlayTests.FlagBearerBubble_TapWipesSquad |
| Reinforcement meter and POW call | Done | PlayMode BattlePlayTests.Reinforcements_AddSoldiersWhenMeterFull |
| Two sequential capture points and victory | Done | PlayMode BattlePlayTests.SquadOnCapturePoint_ProgressIncreases; MetaFlowTests.FullFlow_BootMenuBattleVictoryRewardsReturn; bot victories in Logs/bot_playthrough.txt |
| Defeat when all soldiers die | Done | Reached by bot runs (Logs/bot_playthrough.txt b10 DEFEAT); Screenshots/ui_defeat.png; no dedicated assertion test |
| Counterattack wave after first capture | Done | PlayMode DefenseBattleTests.Counterattack_AfterFirstCapture_MarchesOnThePoint |
| Star coin pickups with x1 and x2 combo numbers | Not done | No in-battle pickups or combo counter; only a coin burst VFX on victory (VFX_Coins) |
| Mercenary with a special ability | Partial | Three MercenaryDef assets join as an extra Hero unit with own stats (EditMode MetaTests.Mercenary_UnlockAndHire); the ability is a text label, not scripted behaviour |

## 7 Base attacks and defense

| Requirement | Status | Evidence |
|---|---|---|
| Defense types named by the sources | Partial | Done: mine, turret, tank, cannon, bunker, tesla, sniper, geyser, defender squad. Not done: heli-tower, tracker, vannon, robotank, trapped slab |
| Defense behaviours | Done | PlayMode DefenseBattleTests.Tesla_ChainHitsMultipleSoldiers, Sniper_AimsShootsAndDamages, Geyser_EruptsAndDamagesSoldiersAround; turret, tank, cannon, mine in bot battles |
| Base editor: place, move, rotate, remove | Done | PlayMode DefenseBattleTests.BaseEditor_PlaceValidateSave_SurvivesRestart; Screenshots/base_editor_full.png |
| Space and budget caps that rise with level | Done | EditMode BaseDefenseTests.Placement_BudgetCap_IsEnforced, Placement_SpaceCap_And_TypeLimit_AreEnforced |
| Gold cost to buy defenses and rebuy consumed ones | Done | EditMode BaseDefenseTests.Purchase_PendingGold_StockFirst_AndRebuyWrecks |
| Base saved and loaded as JSON | Done | EditMode BaseDefenseTests.CommitBaseEdit_DeductsGold_AndLayoutSurvivesRestart |
| Test your own base | Done | TEST button runs a defense battle against a simulated AI attacker (our design; the original's own-base test was an attack) |
| Defend battles with consumed defenses and rewards | Done | EditMode BaseDefenseTests.DefenseResult_ConsumesPlacements_RewardsOnlyWhenHeld_TestsChangeNothing; PlayMode DefenseBattleTests.DefenseBattle_RunsToOutcome_AndRecordsConsumption |
| Ten AI bases on the same rules | Done | Assets/Data/Meta/Base_b01..b10, BaseRoster; PlayMode MetaFlowTests.GeneratedBase_DeploysLayoutAndTeams |
| Map variants | Done | Map_home, Map_canyon, Map_ridge; Screenshots/map_canyon.png, map_ridge.png |
| Offline raids on your base | Partial | Simulated by RaidSimulator, labelled SIMULATED in the UI; EditMode BaseDefenseTests.SimulatedRaid_IsDeterministic_Labeled_AndApplied |
| Real attacks by other players on your base | Not done | No server, no real players |
| Territory extensions and extra bases | Not done | One player base only |

## 8 Camera system

| Requirement | Status | Evidence |
|---|---|---|
| Orthographic oblique camera that follows the squad | Done | BattleCamera, CameraConfig (elevation 31, follow smooth 0.45 s) |
| Constant zoom | Done | No zoom code in battle; CameraConfig.screenSoldierHeights 33 |
| Bounds clamp and shake | Done | BattleCamera.Shake used by DefenderStrike; GameConfig bounds (shake feel not reviewed) |
| Off-screen enemy and objective markers clamped to the screen edge | Done | BattleHUD enemy markers (6); Screenshots/battle_u_formation.png shows helmet and flag markers |
| Pinch zoom or free camera | Not done | Not implemented (base editor has mouse wheel and overview button only) |
| Camera framing matches the reference | Partial | Elevation 31 and 33 soldier-heights are tuned, but the forest border frames differently (VISUAL_FIDELITY_REPORT section 1) |

## 9 User interface

| Requirement | Status | Evidence |
|---|---|---|
| Battle HUD: pause tab, squad badge, POW badge | Done | Screenshots/battle_u_formation.png; UI/Battle/BattleHUD.cs |
| HUD limited to what the original shows | Partial | Extras: capture pill, objective marker, soldier count, POW always visible |
| Pause menu | Done | UI/Battle/PauseMenu.cs: resume, restart, settings, quit to base; Screenshots/ui_pause.png |
| Main menu with Army, Research, Base, Profile and Attack | Done | Screenshots/ui_main_menu.png |
| Opponent list with rewards | Done | Screenshots/ui_attack.png |
| Victory and defeat result cards | Done | Screenshots/ui_victory.png, ui_defeat.png (victory layout differs from f_0086) |
| Research screen | Done | Screenshots/ui_research.png, ui_research_defense.png |
| Army, profile, settings, team select | Done | Screenshots/ui_army.png, ui_profile.png, ui_settings.png, ui_team_select.png |
| Tutorial and shape hints | Done | Screenshots/ui_tutorial.png, ui_shape_hint.png |
| Pre-battle VS splash with tip | Not done | No VS card; the attack list goes straight into battle |
| World or Colors War map screen | Not done | A list of AI base cards replaces the map |
| Alliance screen | Not done | Only the IAllianceService stub exists, no screen |
| UI excluded from post-processing | Not done | HUD and menus are tinted by the post stack; overlay camera attempt was reverted |
| Safe area and aspect handling | Partial | UI/Kit/SafeArea.cs, AdaptiveCanvasScaler; tall captures Screenshots/ui_main_menu_tall.png, battle_hud_tall.png; no device test |
| Defense and HUD labels for base editor | Done | Screenshots/base_editor_*.png, base_defense_battle.png |

## 10 Progression and meta-game

| Requirement | Status | Evidence |
|---|---|---|
| Gold, prisms, XP and levels | Done | EditMode MetaTests.Progression_XpToLevel_AndLevelUpRewards |
| Research gated by level and paid in gold | Done | EditMode MetaTests.Research_PurchaseDeductsGold_AndRespectsLevelLock |
| Battle rewards and grades | Done | EditMode MetaTests.BattleRewards_GrantedAndSaved (grade formula is an estimate) |
| Four leagues | Done | ProgressionConfig.leagues (Bronze, Silver, Gold, Platinum; thresholds are estimates) |
| Six faction colours, opponents never share the player's colour | Done | TeamSelectScreen; Screenshots/ui_team_select.png; no dedicated test |
| Mercenary unlock and hire | Done | EditMode MetaTests.Mercenary_UnlockAndHire; Screenshots/ui_attack.png |
| Save, corruption fallback, migration | Done | EditMode MetaTests.Save_RoundTrip_PreservesProfile, Save_CorruptFile_FallsBackToBackupThenNewProfile, Save_MigratesVersion1 |
| Daily gold gift or free prism grants | Not done | No daily reward code |
| Gold cost to rebuy lost soldiers between battles | Not done | No per-soldier rebuy (GAMEPLAY_MECHANICS lists it; code has defense rebuy and a per battle mercenary fee only) |
| Balanced difficulty curve across ten bases | Partial | Bot wins b01 and b05, loses b10 with and without research (Logs/bot_playthrough.txt); all values are estimates |

## 11 Multiplayer and online features

| Requirement | Status | Evidence |
|---|---|---|
| Service interfaces with a local offline provider | Done | Assets/Scripts/Runtime/Online/OnlineInterfaces.cs, LocalOfflineProvider.cs |
| Real multiplayer, matchmaking servers, accounts | Not done | No networking code |
| Leaderboard | Partial | Synthetic board mixed with your league points (UI/Menu/ProfileScreen.cs), labelled offline |
| Alliances | Partial | Synthetic roster from LocalOfflineProvider.GetAlliances; no UI |
| Asynchronous PvP raids | Partial | Simulated only (RaidSimulator), labelled as not a real player |
| Chat, chests and keys, ads, real-money store | Not done | Out of scope per GDD section 16 |

## 12 Unity project requirements

| Requirement | Status | Evidence |
|---|---|---|
| Unity 6000.3.22f1 with URP | Done | ProjectSettings/ProjectVersion.txt; Assets/Settings/TW_URP_Mobile.asset |
| Input System with mouse and touch | Partial | Input System 1.20.0, one PointerInput path; never tested with real touch hardware |
| Organised folders and assembly definitions | Done | 4 asmdefs: Runtime, Editor, two test assemblies |
| Tunables in ScriptableObjects | Done | Assets/Data (about 40 assets); a grep for stray magic numbers was not performed |
| Object pooling and event decoupling | Done | Core/Pool.cs, ProjectileManager, BattleEvents; no gameplay folder references UI or Meta namespaces (grep) |
| Scenes Boot, MainMenu, Battle, BaseEditor | Done | Assets/Scenes; build settings order checked in contract |
| Compiles with zero errors | Done | 0 occurrences of 'error CS' in Logs/final_editmode.log, final_playmode.log, final_validate.log (2026-11-17) |
| Project validator passes | Done | Logs/validation.txt: 4 scenes, 48 prefabs, 2554 objects, 156 materials, Issues: 0 |
| Android and iOS player builds | Not done | Build support modules are not installed; no device build made or tested |
| Player settings: bundle id, icon, signing | Not done | Not configured; no app icon asset |

## 13 Performance

| Requirement | Status | Evidence |
|---|---|---|
| Editor frame time on desktop | Done | Logs/perf_battle.txt: 9.81 ms CPU average (attack), perf_defense.txt: 7.18 ms; M2, editor, batchmode |
| 60 fps on a phone | Not done | Never profiled on a device |
| Draw calls and triangles suitable for mobile | Not done | About 4990 draw calls and 1.3 M triangles in the attack battle (PERFORMANCE_REPORT.md) |
| Allocation free hot paths | Partial | Pools and preallocated buffers; about 520 bytes per frame GC in the editor, source not isolated |
| Memory growth over a long battle and soak test | Not done | No 5 minute memory check or 20 battle soak test was written or run |
| Frame cap and quality toggle | Done | BootLoader targetFrameRate 60; GameSettings Low disables post (player builds change quality level) |

## 14 Audio and animation polish

| Requirement | Status | Evidence |
|---|---|---|
| Synthesized sound effects | Done | 42 WAV in Assets/Audio/SFX (verify_audio.py measured levels) |
| Two music loops | Done | menu_theme.ogg, battle_theme.ogg |
| Audio wired to gameplay and UI events | Partial | AudioManager, SoundLibrary, MusicPlayer; no test asserts that sources fire |
| Audio reviewed by a human listener | Not done | Never listened to; levels chosen by measurement only |
| Animator controllers for Idle, Run, Shoot, Hit, Die, Cheer | Done | Soldier.controller, Hero.controller, SoldierHeavy.controller; AnimationStateTests |
| Combat feel: hop, squash, recoil, muzzle puffs, coin burst | Done | Contract Phase 4 notes; Screenshots/ui_victory.png |
| UI transitions and animation | Done | ScreenTransition, UIAnim, UIPanelAnim, UINumberTicker (no test) |
| Volume sliders for music and effects | Done | UI/Menu/SettingsPanel.cs; Screenshots/ui_settings.png |
| Haptics | Not done | No vibration code |

## 15 Visual comparison and quality control

| Requirement | Status | Evidence |
|---|---|---|
| Side-by-side comparison images | Done | Docs/Comparisons: 7 PNG |
| Written comparison notes | Done | Docs/Comparisons/NOTES.md |
| Screenshot of every screen | Done | Docs/Screenshots: 45 PNG (menus, HUD, base editor, defense, maps) |
| Iterate until the build closely matches the reference | Partial | One fix pass done (lean, outlines, ring, splats); lava biome, enemy density, bubble arc, victory layout remain |
| Final fidelity report | Done | Docs/VISUAL_FIDELITY_REPORT.md |
| Comparison images regenerated from the final screenshots | Not done | The 7 comparison PNG are from the earlier pass; the final run refreshes Screenshots but not Comparisons |

## 16 Development phases

| Requirement | Status | Evidence |
|---|---|---|
| Phase 1 Research | Done | Roadmap Phase 1 ticked items; six docs, no em dashes |
| Phase 2 Visual foundation | Done | Roadmap Phase 2; project, camera, models, shaders |
| Phase 3 Core playable battle | Done | Roadmap Phase 3; PlayMode BattlePlayTests |
| Phase 4 Visual polish | Partial | Done in part; lava biome and rain remain |
| Phase 5 Base defense | Done | Roadmap Phase 5; BaseDefenseTests and DefenseBattleTests |
| Phase 6 UI and progression | Done | Roadmap Phase 6; MetaTests and MetaFlowTests |
| Phase 7 Additional systems | Partial | Leagues, offline services, mercenary, audio done; lava biome, rain, daily gift not done |
| Phase 8 Testing and refinement | Partial | Suites run (see TESTING.md); soak and memory tests and device profiling not done |

## 18 Deliverables

| Requirement | Status | Evidence |
|---|---|---|
| Unity project | Done | Opens at 6000.3.22f1; 4 scenes |
| Source code | Done | 147 C# files under Assets/Scripts and Assets/Tests |
| Blender and audio generation scripts with outputs | Done | Tools/blender, Tools/audio |
| Design and technical documentation | Done | Docs/*.md |
| Automated tests | Done | 42 EditMode and 30 PlayMode tests (TESTING.md) |
| Screenshots and comparisons | Done | Docs/Screenshots, Docs/Comparisons |
| README with build and run instructions | Done | README.md (written this pass) |
| Performance report | Partial | Docs/PERFORMANCE_REPORT.md covers the editor only |
| Visual fidelity report | Done | Docs/VISUAL_FIDELITY_REPORT.md |
| Android build | Not done | Not built |
| iOS build | Not done | Not built |

## 19 Definition of done

| Requirement | Status | Evidence |
|---|---|---|
| Full loop playable: menu, battle, result, base editor, defense | Done | PlayMode MetaFlowTests.FullFlow_BootMenuBattleVictoryRewardsReturn, DefenseBattleTests |
| All automated tests pass | Done | EditMode 42 of 42 and PlayMode 30 of 30 passed (Logs/final_editmode.xml, Logs/final_playmode.xml, 2026-11-17) |
| Zero compile errors | Done | 0 occurrences of 'error CS' in Logs/final_editmode.log, final_playmode.log, final_validate.log (2026-11-17) |
| Visual result closely matches the reference | Partial | VISUAL_FIDELITY_REPORT.md; known differences listed |
| Every feature in the brief implemented | Partial | See the Not done or partial list above |
| Runs on a mobile device | Not done | No device build exists |
| Documentation accurate | Done | Written and cross-checked against disk and logs in this pass |
| No assets from the original game | Done | MD5 check and generation scripts |
