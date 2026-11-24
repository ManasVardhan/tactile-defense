# Tactile Wars Recreation: Gameplay Mechanics Spec

Engineer-facing spec. Every numeric value has: value, status, and the ScriptableObject field. Status is either **VERIFIED (source)** or **ESTIMATE**. "Source" abbreviations: RM = `Docs/References/web/RESEARCH_MECHANICS.md`, VA = `Docs/References/VISUAL_ANALYSIS.md`. Units: 1 Unity unit = 1 meter, soldier height about 1.0, soldier width about 0.8 (ESTIMATE). ScriptableObjects live in `Assets/Data`.

ScriptableObject naming used below: `CameraConfig`, `FormationConfig`, `SquadConfig`, `SoldierConfig`, `WeaponConfig`, `StructureConfig` (one asset per defense type), `BattleConfig`, `EconomyConfig`, `BaseEditorConfig`.

Nothing in the references gives stats. Most numbers are ESTIMATE and chosen to reproduce the observed read; tune against `ps_12.png` and `f_0056.jpg`.

## 1. Input (legacy: used only by the AI attacker in defense battles)

> Legacy. Attack battles no longer use strokes: the player deploys and moves units on a hex grid (section 16). Sections 1 to 3 describe the stroke-driven squad, which the AI attacker still drives in defense battles.

Input is wrapped in `Core/PointerInput.cs` so mouse and touch share one path.

| Rule | Value | Status | SO field |
|---|---|---|---|
| Tap moves the squad (formation preserved, anchor moves to tap point) | n/a | VERIFIED (RM section 2: "tap to move units") | n/a |
| Drag draws a formation stroke | n/a | VERIFIED (RM section 2) | n/a |
| Tap vs drag threshold, screen distance | 12 px at 1080p-equivalent scaled by DPI | ESTIMATE | `FormationConfig.tapMaxDragPixels` |
| Tap vs drag threshold, time | 0.25 s | ESTIMATE | `FormationConfig.tapMaxSeconds` |
| Minimum stroke length to count as a formation | 2.0 world units | ESTIMATE | `FormationConfig.minStrokeLength` |
| Tap on a flag-bearer bubble pops it (takes priority over move) | n/a | VERIFIED mechanic (RM section 1), priority is decision D20 | `BattleConfig.bubbleTapRadius` = 1.6 (ESTIMATE) |
| Tap on POW badge calls reinforcements when ready | n/a | ESTIMATE (D7) | n/a |

Behavior notes:
- Pointer positions are raycast to the ground plane (y = 0) to produce world points.
- A stroke in progress is rendered by `StrokeRenderer` as an inner solid line plus a dashed outer line in team color (VERIFIED look, VA section 3: ps_08; the video shows a thin dashed line from drag start, f_0056).
- Interpretation of the line mapping: VA f_0056 shows soldiers marching onto the drawn line as a single-file column, and levelwinner says "draw a straight line parallel to the mine" (RM section 2). Exact line-to-slot mapping is UNVERIFIED; the algorithm in section 2 is our design.

## 2. From stroke to soldier slots (legacy: AI attacker only)

Pipeline in `Formation/`: `StrokeRecorder` then `PathResampler` then `ShapeRecognizer` then `FormationSolver`.

### 2.1 Record
Sample pointer world positions each frame; drop a sample if closer than `FormationConfig.sampleMinDistance` to the last.

### 2.2 Smooth
Chaikin corner cutting or a 5-point moving average, preserving endpoints. Iterations: 2. Purpose: remove hand jitter. (ESTIMATE, `FormationConfig.smoothIterations`.)

### 2.3 Arc-length resample
Compute cumulative arc length L of the smoothed polyline. Resample into N points at equal arc-length spacing, where N is derived below. Closed shapes (circle, square) use perimeter and N points with no duplicated endpoint.

### 2.4 Count and spacing
Let S = living soldier count, w = soldier width, spacing d = `FormationConfig.spacingWidths` * w.
- Observed spacing along a drawn stroke: about 1.0 to 1.3 widths center to center (VERIFIED visually, VA section 2: ps_08 heart). Idle blob about 0.4 to 0.7 widths (VA).
- Capacity along stroke: Ncap = floor(L / d) + 1 (open) or floor(L / d) (closed).
- If Ncap >= S: place all S soldiers evenly over the full length, so actual spacing is L / (S - 1) (open), clamped to at most `maxSpacingWidths`. If clamped, the line is centered on the stroke and shorter than the stroke.
- If Ncap < S: place Ncap soldiers on the stroke, and distribute the remaining soldiers in a second rank (offset behind the stroke by `rankOffsetWidths`) and further ranks if needed. Alternative for closed shapes: fill the interior (see 2.6).
- Strokes are never rejected for being too short; below `minStrokeLength` the input is treated as a tap.

| Field | Value | Status | SO field |
|---|---|---|---|
| Spacing along stroke | 1.15 widths (range 1.0 to 1.3) | VERIFIED range (VA ps_08), midpoint ESTIMATE | `FormationConfig.spacingWidths` |
| Max spacing when stroke is long | 1.6 widths | ESTIMATE | `FormationConfig.maxSpacingWidths` |
| Second rank offset | 1.0 widths | ESTIMATE | `FormationConfig.rankOffsetWidths` |
| Idle blob spacing (no stroke, after tap) | 0.55 widths | VERIFIED range 0.4 to 0.7 (VA f_0056, burst_A_040), midpoint ESTIMATE | `FormationConfig.blobSpacingWidths` |
| Smoothing iterations | 2 | ESTIMATE | `FormationConfig.smoothIterations` |
| Sample min distance | 0.15 units | ESTIMATE | `FormationConfig.sampleMinDistance` |

### 2.5 Shape recognition
`ShapeRecognizer` classifies the smoothed stroke. Sources confirm lines, circles, squares are supported (VERIFIED, RM section 2: App Store text) and that an arrow shape exists as a gesture (MMOs mention); arrow handling is not specified beyond "arrow makes an arrow" and is treated as free-form here.

Classification (all thresholds ESTIMATE, `FormationConfig`):
1. **Closed?** gap between first and last point < `closeGapRatio` (0.25) times total length.
2. **Line**: fit a straight line; max perpendicular deviation < `lineMaxDeviationRatio` (0.08) times length. Result: slots along the straight fit (cleans up wobble).
3. **Circle**: closed and the radius standard deviation about the centroid < `circleRadiusStdRatio` (0.12) times mean radius. Result: slots on a true circle of the mean radius around the centroid.
4. **Square**: closed and polygon simplification (Douglas-Peucker, epsilon 6 percent of perimeter) yields 4 corners with interior angles 90 degrees plus or minus 25 degrees. Result: slots along a rectangle fitted to the corners, perimeter-resampled.
5. **Free**: everything else. Slots along the smoothed stroke as drawn (hearts, spirals, arcs, C shapes; VERIFIED shape variety in VA section 3).

Recognition snaps only when confidence is high; otherwise falls through to Free. Snapping can be disabled (`FormationConfig.enableShapeSnap`).

### 2.6 Filled closed shapes
For closed Circle and Square, if S exceeds perimeter capacity, add concentric inner rings (spacing d between rings) until all soldiers are placed. Contract wording "slots along or inside the shape".

### 2.7 Assignment
Assign soldiers to slots by minimal total travel. For S up to 60, a greedy nearest pair with a two-pass swap improvement is sufficient (full Hungarian is optional). Soldiers march onto slots as a chain: the VA evidence shows a single-file column or crescent forming over about 1 to 2 seconds (VA section 3), so arrival is staggered by slot index (stagger 0.02 s per slot, ESTIMATE, `FormationConfig.slotArrivalStagger`).

### 2.8 Facing
Squad facing equals the formation direction: for a line, the perpendicular pointing toward the nearest enemy side (or +Z if none); for a ring or free shape, outward from the centroid toward the nearest enemy. This is decision D1 (auto-fire facing).

## 3. Squad anchor and the ground ring (legacy: AI attacker only)

| Rule | Value | Status | SO field |
|---|---|---|---|
| Ring visual is a large team-colored ellipse with a spiral blot at center | n/a | VERIFIED (VA section 3: f_0056, ps_12, ps_06) | n/a |
| Ring radius | 6.0 units (diameter about 12 soldier-heights; ps_12 ring is about 310 px wide against soldiers about 22 px tall, so about 14, rounded down) | ESTIMATE derived from VA measurement | `SquadConfig.ringRadius` |
| Ring lags behind squad when a new formation is drawn | squad trails the ring center by a few widths and reaches it in 1 to 2 s | VERIFIED visually (VA section 3 burst_A) | `SquadConfig.settleSeconds` = 1.5 (ESTIMATE) |
| Ring splits color when contested | n/a | VERIFIED look (VA f_0023, f_0025) | n/a |

Behavior: tap sets the anchor; the ring is drawn at the anchor. The squad's formation is relative to the anchor. Drawing a stroke re-centers the anchor at the stroke centroid.

## 4. Movement feel

VA evidence: soldiers trail and reform smoothly, hold station in an arc while firing (VA section 3). Speeds are not measurable from the references.

| Field | Value | Status | SO field |
|---|---|---|---|
| Soldier run speed | 4.5 units/s | ESTIMATE | `SoldierConfig.moveSpeed` |
| Acceleration | 18 units/s^2 | ESTIMATE | `SoldierConfig.acceleration` |
| Deceleration | 24 units/s^2 | ESTIMATE | `SoldierConfig.deceleration` |
| Turn rate | 720 deg/s | ESTIMATE | `SoldierConfig.turnRate` |
| Separation radius | 0.5 units | ESTIMATE | `SoldierConfig.separationRadius` |
| Separation weight | 1.2 | ESTIMATE | `SoldierConfig.separationWeight` |
| Slot arrival tolerance | 0.15 units | ESTIMATE | `SoldierConfig.arriveTolerance` |
| Catch-up speed multiplier when far from slot (> 4 units) | 1.5 | ESTIMATE | `SoldierConfig.catchUpMultiplier` |
| Soldiers push through slower friends only via separation, no hard collision | n/a | ESTIMATE | n/a |

Obstacle avoidance: simple steering around colliders on layers Obstacle (10) and Structure (9); no NavMesh in milestone one (per ARCHITECTURE_CONTRACT). Soldiers are not blocked by projectiles.

## 5. Auto-targeting and firing

Decision D1: auto-fire. Sources conflict: Ankama's 2016 press release says players "aim with your finger, firing paintballs" and "finger-controlled aiming and firing" (VERIFIED as a source statement, RM section 2, likely loose marketing), while multiplayer.it says units attack automatically once targets are in range (VERIFIED, RM section 2). The recreation uses auto-fire with facing set by formation direction (section 2.8). Manual aiming is not implemented and is UNVERIFIED as a feature.

| Field | Value | Status | SO field |
|---|---|---|---|
| Target selection | nearest hostile within range | ESTIMATE (contract: nearest in range) | `WeaponConfig.targetMode` = Nearest |
| Retarget interval | 0.25 s | ESTIMATE | `WeaponConfig.retargetInterval` |
| Soldier weapon range | 7.0 units | ESTIMATE (squad engages across a few widths, VA ps_12 shots travel about 10 soldier widths) | `WeaponConfig.range` |
| Soldier fire interval | 0.6 s per shot, jittered 0.0 to 0.2 s per soldier | ESTIMATE | `WeaponConfig.fireInterval`, `fireJitter` |
| Shots per burst | 1 | ESTIMATE | `WeaponConfig.burstCount` |
| Accuracy cone | 6 degrees | ESTIMATE | `WeaponConfig.spreadDegrees` |
| Soldiers fire while moving | yes | ESTIMATE (VA burst_A shows streams during repositioning) | `WeaponConfig.canFireWhileMoving` = true |
| Visual: dense dotted streams of 5 to 20 dots visible at once | n/a | VERIFIED (VA section 4) | n/a (emerges from fireInterval and squad size) |

Stream read: with 40 soldiers at 0.6 s interval, about 67 projectiles per second; with projectile lifetime of about 0.7 s, about 45 dots are in flight, matching the "5 to 20 dots per target stream" read after distribution across targets.

## 6. Projectiles

| Field | Value | Status | SO field |
|---|---|---|---|
| Visual: round dot, white core, team-colored edge, no trail | n/a | VERIFIED (VA section 4) | n/a |
| Dot diameter | about 0.15 units (3 to 4 px at 20 to 25 px soldiers) | ESTIMATE derived from VA | `WeaponConfig.projectileRadius` = 0.08 |
| Speed | 14 units/s | ESTIMATE | `WeaponConfig.projectileSpeed` |
| Lifetime | 1.0 s or until range plus 1 unit | ESTIMATE | `WeaponConfig.projectileLifetime` |
| Collision | layer Projectile (11) against opposing team layers and Obstacle | contract | n/a |
| Pooling | `Core/Pool<T>`, capacity 400 | ESTIMATE | `BattleConfig.projectilePoolSize` |
| On hit | small team-colored splash, chance to spawn a ground splat | VERIFIED look of splats (VA section 4), chance ESTIMATE | `WeaponConfig.splatOnHitChance` = 0.15 |
| Friendly fire | none | ESTIMATE | n/a |

## 7. Damage, health, death

No health bars and no damage numbers in the original (VERIFIED, VA section 4). Health is internal only. Units simply disappear.

| Field | Value | Status | SO field |
|---|---|---|---|
| Soldier max HP | 10 | ESTIMATE | `SoldierConfig.maxHealth` |
| Soldier shot damage | 2.5 (4 shots to kill) | ESTIMATE | `WeaponConfig.damage` |
| Enemy grunt max HP | 10 | ESTIMATE | `SoldierConfig.maxHealth` (enemy variant) |
| Flag-bearer max HP | 30 | ESTIMATE | `SoldierConfig.maxHealth` (flag variant) |
| Damage numbers shown | none | VERIFIED (VA) | n/a |
| Health bars shown | none | VERIFIED (VA) | n/a |
| Hit feedback | 0.2 s flinch animation, brief white tint flash | ESTIMATE (animation list in contract) | `SoldierConfig.hitFlashSeconds` = 0.08 |

Death sequence (VERIFIED look, VA section 4: ghost, white flash, puff; killed own soldiers leave a red splat and red ghost):
1. `Die` animation (0.6 s per contract).
2. Spawn a translucent team-colored ghost wisp that rises and fades (1.0 s, ESTIMATE, `SoldierConfig.ghostSeconds`).
3. White puff particle burst.
4. Ground splat in the color of the killer's team? VA shows a red splat for dead red soldiers and purple blotches around purple casualties; we follow the victim's team color (decision). `SoldierConfig.splatOnDeath` = true.
5. Count decremented; `BattleEvents.OnUnitKilled` fired.

Defeat check runs when player soldier count reaches 0 (VERIFIED lose condition, RM section 1).

## 8. Flag-bearer bubble

| Field | Value | Status | SO field |
|---|---|---|---|
| Most enemy squads include one flag-bearer | n/a | VERIFIED (RM section 1) | `EnemySquadConfig.hasFlagBearer` |
| On bearer death, a tappable bubble spawns | n/a | VERIFIED | n/a |
| Tap bubble: whole squad destroyed instantly | n/a | VERIFIED | n/a |
| Bubble lifetime | 8 s | ESTIMATE (D6) | `BattleConfig.bubbleLifetime` |
| Bubble radius | 1.5 units (about 3 soldier sizes wide) | VERIFIED size ratio (VA section 5), absolute value ESTIMATE | `BattleConfig.bubbleRadius` |
| Expiry result | squad continues fighting without leader | ESTIMATE (D6) | `BattleConfig.bubbleExpiryBehavior` |
| Reinforcement credit when squad popped | counts as 1 squad kill | ESTIMATE | `BattleConfig.reinforcePerSquad` |
| Casualty-free popping rewards survivors bonus on result card | n/a | INFERRED from advice "no casualties" (RM section 1) | `EconomyConfig.noCasualtyBonus` = 0.1 (ESTIMATE) |

## 9. Reinforcement meter

| Field | Value | Status | SO field |
|---|---|---|---|
| Meter exists, top-right, fills as squads die | n/a | VERIFIED (RM section 1) | n/a |
| Meter max | 100 | ESTIMATE | `BattleConfig.reinforceMax` |
| Fill per squad killed | 25 | ESTIMATE | `BattleConfig.reinforcePerSquad` |
| Fill per defense destroyed | 15 | ESTIMATE | `BattleConfig.reinforcePerStructure` |
| Summon batch | 12 soldiers appear next to the squad, respecting squad cap | ESTIMATE | `BattleConfig.reinforceBatch` |
| Meter resets to 0 on summon | n/a | ESTIMATE | n/a |
| POW button lights when full, tap to summon | n/a | ESTIMATE (D7) | n/a |

## 10. Capture rules and win/lose

VERIFIED (RM section 1): two circular capture points per base on a linear map; capture, defend briefly, capture the next, win; lose when all units die. A white flag plants at a captured point (VERIFIED look, VA section 4).

| Field | Value | Status | SO field |
|---|---|---|---|
| Capture points per map | 2, sequential | VERIFIED | `BattleConfig.capturePointCount` = 2 |
| Capture radius | 2.0 units (pad radius per CapturePoint.fbx contract) | contract value, ESTIMATE in game terms | `BattleConfig.captureRadius` |
| Capture progress rule | progresses while at least `captureMinSoldiers` player soldiers inside and zero enemy units inside | ESTIMATE | `BattleConfig.captureMinSoldiers` = 5 |
| Capture time | 4 s at full rate; faster with more soldiers up to 2x | ESTIMATE (D4) | `BattleConfig.captureSeconds`, `captureSpeedupMax` |
| Progress decay when empty | 1.0 progress per 6 s | ESTIMATE | `BattleConfig.captureDecaySeconds` |
| Hold phase after capture ("defend briefly") | 6 s, during which defenders may respawn in reinforcement waves | ESTIMATE (D4; RM says "briefly", multiplayer.it mentions waves) | `BattleConfig.holdSeconds`, `holdWaveCount` = 1 |
| Second point unlocks only after first is held | n/a | VERIFIED (sequential) | n/a |
| Win | both points captured and holds completed | VERIFIED | n/a |
| Lose | all player soldiers dead | VERIFIED | n/a |
| Time limit | none | ESTIMATE (no timer verified, RM section 1) | `BattleConfig.timeLimitSeconds` = 0 (disabled) |

## 11. Defense type behaviors

All behaviors are ESTIMATE (D8). Names are VERIFIED (RM section 4) but no behavior is described except for mines and geysers. Fields in `StructureConfig` (one asset per type): `maxHealth`, `range`, `fireInterval`, `damage`, `projectileSpeed`, `turnRate`, `budgetCost`, `spaceCost`, `goldCost`, `unlockLevel`.

| Type | Behavior | maxHealth | range | fireInterval | damage | budgetCost | spaceCost | Status |
|---|---|---|---|---|---|---|---|---|
| Turret | Head yaws toward nearest player unit, single paint dot stream | 60 | 9 | 0.4 s | 2.0 | 10 | 1 | ESTIMATE |
| Tank | Hull moves slowly along a short patrol path (max 6 units), turret fires 3-shot bursts | 100 | 8 | 1.2 s burst | 3.0 | 20 | 2 | ESTIMATE |
| Cannon | Slow lobbed shot to the target's position (0.8 s flight), area radius 2.0, warning marker on the ground so players can dodge | 70 | 12 | 3.0 s | 8.0 area | 15 | 2 | ESTIMATE; "positioning avoids mortar fire" VERIFIED (multiplayer.it) |
| Mine | Disc on the ground, triggers when a player soldier is within 0.6 units; area burst radius 1.8; one use | 1 (indestructible by fire) | trigger 0.6 | single use | 6.0 area | 4 | 1 | ESTIMATE; "damages units on detonation, run through fast" VERIFIED (levelwinner) |
| Tesla antenna | Zaps up to 3 nearest soldiers within range each pulse, short range | 50 | 5 | 1.5 s | 4.0 per target | 12 | 1 | ESTIMATE |
| Bunker | Static block, fires from a slit, high HP | 150 | 8 | 0.8 s | 2.0 | 18 | 3 | ESTIMATE; look VERIFIED (VA) |
| Sandbags and walls | Block movement and projectiles, destructible optional | 80 | n/a | n/a | n/a | 2 | 1 | ESTIMATE |
| Enemy infantry squad | 6 to 20 grunts around a flag-bearer, hold position, fire at nearest in 7 units | 10 each | 7 | 0.7 s | 2.0 | 8 per squad | 2 | ESTIMATE |

Destruction: structure death plays a white starburst plus dust (VERIFIED look, VA section 4: burst_C_015, f_0083) and may drop star-coin pickups (VERIFIED look, VA f_0037, f_0077; count ESTIMATE, 1 to 3).

Not implemented in milestone one (names VERIFIED, behavior UNVERIFIED): sniper, heli-tower, tracker, geyser, trapped slab, vannon, robotank, bosses.

## 12. Rewards

| Field | Value | Status | SO field |
|---|---|---|---|
| Victory card shows gold, EXP, rank, MEDALS, SURVIVORS, COMBOS, TIME and grade letters | n/a | VERIFIED layout (VA section 7: f_0086) | n/a |
| Example victory: +23 gold, +298 EXP | n/a | VERIFIED single sample (VA f_0086, f_0090, f_0091), a level 20 vs level 21 PvP match | n/a |
| Base gold per win | 20 | ESTIMATE (D12), anchored to the sample | `EconomyConfig.baseGoldWin` |
| Gold per star-coin pickup | 1, with combo multiplier x1, x2 | VERIFIED look "x1" "x2" (VA section 4); amount ESTIMATE | `EconomyConfig.goldPerCoin`, `comboMax` |
| Base XP per win | 250 | ESTIMATE anchored to 298 sample | `EconomyConfig.baseXpWin` |
| Loss reward | 25 percent of win rewards | ESTIMATE | `EconomyConfig.lossFraction` |
| Opponent rank scaling | multiplier 0.6 to 1.5 by rank difference | VERIFIED concept (higher rank gives better rewards, RM section 6), numbers ESTIMATE | `EconomyConfig.rankRewardMin/Max` |
| Survivor bonus | +10 percent at 100 percent survivors | ESTIMATE | `EconomyConfig.noCasualtyBonus` |
| Soldier rebuy cost between battles | 1 gold per soldier lost | ESTIMATE (rebuy concept VERIFIED, RM section 3 and 5) | `SquadConfig.rebuyGoldPerSoldier` |
| Defense rebuy | equals `StructureConfig.goldCost` | rebuy concept VERIFIED, cost ESTIMATE | `StructureConfig.goldCost` |
| No energy, no timers gating attacks | n/a | VERIFIED (RM section 1 and 6) | n/a |
| Grade letters | S, A, B, C by survivors, combos, time | letters VERIFIED visible, formula ESTIMATE (D16) | `EconomyConfig.gradeThresholds[]` |

## 13. Base editing numbers

| Field | Value | Status | SO field |
|---|---|---|---|
| Space cap at level 1 | 8 | ESTIMATE (concept VERIFIED, RM section 5) | `BaseEditorConfig.spaceCapByLevel[]` |
| Budget cap at level 1 | 60 | ESTIMATE | `BaseEditorConfig.budgetCapByLevel[]` |
| Cap growth per level | +2 space every 3 levels, +10 budget per level | ESTIMATE | same arrays |
| Placement grid | 1.0 unit cells on the lane | ESTIMATE (UNVERIFIED in sources) | `BaseEditorConfig.gridCell` |
| Rejection rule | refuse placement if budget or space would exceed cap, show a "too heavy" message | VERIFIED concept via user report (RM section 5, INFERRED) | n/a |

## 14. Camera behavior

Pans with the squad; constant zoom; loose follow (VERIFIED, VA section 1). Off-screen groups are shown as helmet icons clamped to screen edges (VERIFIED).

| Field | Value | Status | SO field |
|---|---|---|---|
| Projection | orthographic | VERIFIED as "likely orthographic" (VA section 1) | `CameraConfig.orthographic` = true |
| Elevation | 35 degrees (range 30 to 40) | ESTIMATE; VA ellipse ratio gives about 25 to 30 | `CameraConfig.elevationDegrees` |
| Visible height | 28 soldier-heights (range 25 to 30) | VERIFIED range (VA section 1), midpoint ESTIMATE | `CameraConfig.orthoSize` = 14 |
| Follow smoothing | 0.35 s smooth damp on squad centroid | ESTIMATE ("loose follow" VERIFIED) | `CameraConfig.followSmoothTime` |
| Map bounds clamp | yes | contract | n/a |
| Shake on explosion | 0.1 units, 0.15 s | ESTIMATE | `CameraConfig.shakeAmplitude/Duration` |
| Off-screen icons | clamped helmet badge, team-colored | VERIFIED look | n/a |

## 15. Base defense as implemented (phase 5)

VERIFIED concepts (RM sections 1, 4, 5): attacks are asynchronous against AI run defenses laid out by a player; a base has
a space limit and a budget cap that rises with level; placed defenses are consumed and must be rebought with gold; named
defenses include mines, geysers, trapped slabs, tesla antennas, turrets, tanks, cannons, robotanks, trackers, snipers,
bunkers, heli-towers; geysers explode. EVERY NUMBER AND BEHAVIOUR BELOW IS AN ESTIMATE (our design).

### 15.1 Defense economy (`Assets/Data/Meta/DefenseCatalog.asset`, `DefenseCatalog.Entry`)

| Id | Gold | Budget | Space | Max | Footprint | Research | Notes |
|---|---|---|---|---|---|---|---|
| mine | 15 | 4 | 1 | 10 | 0.6 | def_mine (start) | single use trap |
| geyser | 30 | 6 | 1 | 6 | 0.9 | def_geyser (lv 3) | 2 eruptions, then spent (consumed) |
| turret | 60 | 10 | 1 | 4 | 1.1 | def_turret (lv 2) | |
| squad | 40 | 8 | 2 | 3 | 2.4 | none | 8 defender soldiers + flag bearer |
| tesla | 90 | 12 | 1 | 3 | 0.8 | def_tesla (lv 8) | |
| sniper | 100 | 14 | 1 | 2 | 0.9 | def_sniper (lv 6) | |
| bunker | 110 | 18 | 3 | 2 | 1.7 | def_bunker (lv 4) | |
| cannon | 120 | 15 | 2 | 2 | 1.0 | def_cannon (lv 7) | |
| tank | 150 | 20 | 2 | 2 | 1.4 | def_tank (lv 5) | patrols 3 units to each side |

Budget and space points for turret, tank, cannon, mine, tesla and bunker are those of section 11.

### 15.2 Base editing (`Assets/Data/Meta/BaseEditorConfig.asset`)

| Rule | Value | SO field |
|---|---|---|
| Budget cap | 60 at level 1, +10 per level | `budgetCapByLevel[]` |
| Space cap | 10 at level 1, +2 every 2 levels (section 13 said 8, +2 every 3; raised because traps cost space) | `spaceCapByLevel[]` |
| Grid | 1.0 unit squares, footprints snap to cell centres | `gridCell` |
| Zones | placeable rectangle x -15.5 to 15.5, z 18 to 101; attacker start zone z < 18; 3.6 clearance around capture pads; no overlap with blocking props or other defenses | `MapLayoutData.placeable`, `attackerZoneMaxZ`, `padClearance` |
| Purchase | placing uses a stocked copy, else buys at gold cost; paid on Save | n/a |
| Remove | back to stock, no gold refund, re-placing is free | n/a |
| Consumption | destroyed structures, fired mines, spent geysers and wiped defender squads become wrecks; rebuy at gold cost in place | n/a |
| Research level bonus | +15 percent health and damage per level above 1 in battle | `levelBonus` |

### 15.3 New defense behaviours (`Assets/Data/Structure_Tesla/Sniper/Geyser.asset`)

| Type | Behaviour | Values |
|---|---|---|
| Tesla antenna | charges, then a paint lightning arc hits the nearest soldier in range and chains to the nearest unhit soldier within the jump range | charge 2.2 s, range 5.5, up to 4 targets, jump 2.6, 26 damage, x0.8 per jump, 160 HP |
| Sniper tower | prefers flag bearers, yaws, shows a growing aiming line, then one fast heavy shot | range 16, aim 1.3 s, 50 damage, 45 u/s, cooldown 2.4 s, 140 HP |
| Geyser | hidden (only a rock ring shows); an attacker within the trigger radius wakes it: rumble warning, then a paint eruption in an area; optional timer | trigger 1.8, warning 0.9 s, radius 2.8, 34 damage, 2 charges, cooldown 4.5 s, not targetable |

Not implemented: trapped slab, robotank, tracker, heli-tower, vannon (names verified, no behaviour).

### 15.4 Defense battles (`Assets/Data/DefenseBattleConfig.asset`)

| Rule | Value |
|---|---|
| Attacker squad | 30 + 2 per player level soldiers (max 56), +4 percent health per level above 1, 8 per reinforcement call |
| Outcome | base falls when both points are captured; defense holds when the attackers are wiped or after 240 s |
| AI decisions every 0.4 s | engage (line at 5.2 from a defender squad centre or structure within 11, pushes closer after 9 s without kills); spread (ring of radius 5.2 when a visible mine or an already erupted geyser lies within 5 of the next step); capture (ring of radius 1.6 on the active pad); advance (7 unit steps along the grid route); pops bubbles after 0.8 s; calls reinforcements when ready |
| Defender paint strike | 3 charges, radius 3, 45 damage, 0.7 s fall, 4 s cooldown |
| Rewards (DEFEND only) | held: 60 gold + 1 per attacker popped, 40 XP; fell: 10 XP; TEST battles pay nothing and consume nothing |
| Watch speed | 1x, 2x, 4x |

### 15.5 Simulated offline raids (labeled SIMULATED in the UI; not real players)

On returning to the menu after at least 30 minutes (at most 2 per session, never on an empty base):
strength = sum of placed defenses' budget points x level bonus; attack = (40 + 12 x level) x U(0.7, 1.3);
P(hold) = strength / (strength + attack). Traps fire (consumed) with 0.55 probability; other defenses are destroyed with
0.12 (held) or 0.45 (fell). Held: +45 gold x U(0.8, 1.2). Fell: -min(8 percent of gold, 120). Last 6 raids are logged.

### 15.6 Counterattack and routing

After the first capture, 6 soldiers leave the spawn hatch nearest the captured point after 3 s and march on it
(`BattleConfig.counterattack*`). Taps whose straight path is blocked by walls route the squad through gates on a 1 unit
obstacle grid (`SquadConfig.useRouting`, `navCell`, `navClearance`, `routeBlobSpacing`, `routeAdvanceDistance`).

## 16. Unit squads on a hex grid (attack battles)

Attack battles field a squad of units instead of one stroke-driven squad. Defense battles keep the legacy squad (sections 1 to 3), driven by the AI attacker.

### 16.1 Controls

| Action | Rule |
|---|---|
| Deploy | Click a unit button in the bar at the bottom, then a free hexagon. A hexagon is free when nothing blocks it, no unit holds it, and no enemy soldier stands within 0.9 of its outer radius. Escape or clicking the button again disarms. |
| Select and move | Click a unit, then a hexagon. The unit walks there by the grid route; the destination is reserved at once. Refused orders flash the hexagon red and play a sound. |
| Pan | Drag the ground. A drag that starts on the HUD does nothing. |
| Backup | When the meter is full and a unit was lost, click the Backup badge (or press R): the lost unit is back in the bar, ready to deploy. |

### 16.2 Units

| Rule | Value | Status | Field |
|---|---|---|---|
| Hex size (outer radius) | 1.4 | ESTIMATE | `BattleConfig.hexOuterRadius` |
| Default squad | Attacker, Attacker, Tank (9 soldiers) | ESTIMATE | `PlayerArmy.DefaultSquad` |
| Soldiers per unit | 3 | ESTIMATE | `UnitTypeConfig.soldierCount` |
| Attacker | the base soldier (`Unit_PlayerSoldier`: 45 health, range 6.5, 3 x 3 damage every 1.15 s) | ESTIMATE | `Assets/Data/Units/UnitType_Attacker.asset` |
| Tank | health x3, range x0.6, speed x0.7, draws fire | ESTIMATE | `Assets/Data/Units/UnitType_Tank.asset` |
| Draws fire | enemy soldiers and direct-fire defenses (turret, bunker, tank, sniper tower) shoot a Tank soldier in range before a nearer one; the cannon's lobbed shells, tesla, mines and geysers do not choose | ESTIMATE | `UnitTypeConfig.drawsFire`, `StructureBase.PreferDrawsFire` |

### 16.3 Capture with units

| Rule | Value | Status | Field |
|---|---|---|---|
| Base capture time | 4 s | ESTIMATE | `BattleConfig.captureTime` |
| Speed bonus per soldier on the pad | +1/3, counted up to 9 soldiers: 3 soldiers capture 1.5 times as fast as 1, 9 twice as fast as 3 | ESTIMATE | `BattleConfig.armyPerSoldierBonus`, `armyMaxSoldierBonus` |
| Who counts | soldiers inside the pad radius, plus every living soldier of a resting unit whose hexagon overlaps the pad | ESTIMATE | `PlayerArmy.PadExtraCount` |

Defense battles keep the legacy bonus (+3 percent per soldier, up to 10).

### 16.4 Defeat and backup

The battle is lost when no soldier is alive, no unit is ready to deploy and the backup meter is not full. A full meter postpones defeat until the player calls backup.

### 16.5 Level 1 (tutorial level, base `b01`)

The handcrafted deployment of `Battle.unity`, also used when no loadout is pending. The straight lane with no traps: a squad of 4 grunts without a flag bearer stands on point 1, one turret and a squad of 6 (5 grunts and a flag bearer) hold point 2. No mines, geysers, cannons, tesla antennas or bunkers. Enemy multipliers: health x1.0, damage x1.2.

| Per-base rule | Level 1 | Field |
|---|---|---|
| Passive enemies: squads wake only when a player soldier is within their weapon range plus 2 of a member | on | `BaseDefinition.passiveEnemies`, `BattleLoadout.passiveEnemies`, `EnemySquad.passive` |
| Counterattack after the first capture | off | `BaseDefinition.counterattack`, `BattleLoadout.counterattack`, `BattleSetup.sceneCounterattack` (no loadout) |

The guided tutorial (spec 6.2) runs on Level 1 only. The squad on point 1 makes its "Tanks" step come before its "capture" step.


### 16.6 Level ladder, unlocks and the squad

Rules live in `Assets/Scripts/Runtime/Meta/Progression.cs` (pure, tested by `ProgressionTests`).

| Rule | Value | Status | Field |
|---|---|---|---|
| Levels | the five roster bases in order: Level N is `MetaDatabase.roster.bases[N - 1]` (b01 to b05), each on its own map (`Map_levelN`) | ESTIMATE | `Progression.LevelCount` |
| Cleared | consecutive levels from Level 1 whose base id is in `PlayerProfile.defeatedBases` | | `Progression.LevelsCleared` |
| Open | Level N is open when N is at most cleared + 1 (cleared levels stay open for replay); the player XP level no longer gates bases | | `Progression.IsLevelOpen`, `MetaGame.IsBaseUnlocked` |
| Squad slots | 3, plus 1 per level cleared up to 8 (at Level 5) | ESTIMATE | `Progression.SquadSlots` |
| Unit unlocks | Attacker and Tank from the start; Healer at Level 1, Sniper at 2, Vanguard at 3, Demolisher at 4 | ESTIMATE | `Progression.UnlockLevel` |
| Squad | one unit type per slot, chosen on the Army screen; emptied slots are skipped, at least one unit stays; a new slot arrives filled with an Attacker | | `PlayerProfile.squad`, `MetaGame.Squad`, `BattleLoadout.squad` |
| Mercenary | a hired mercenary joins as one extra one soldier unit after the squad (health and damage from its multipliers times Mercenary Training); it can be healed, is never revived and backup never restores it | ESTIMATE | `PlayerArmy.SetMercenary`, `BattleSetup.MercenaryType` |
| Retired research | Squad Size and Bigger Backup were removed; save version 3 refunds the gold paid for them | | `SaveMigrations.RetiredResearchRefund` |

| Demolisher | Value | Status | Field |
|---|---|---|---|
| Fire interval | x2.5 the base soldier's, one lobbed shell per volley (3 x 3 damage) | ESTIMATE | `UnitType_Demolisher.fireIntervalMult` |
| Damage to structures / soldiers | x8 / x0.4, splash 1.8 | ESTIMATE | `structureDamageMult`, `soldierDamageMult` |
| Measured | one unit destroys a turret in 1.97 s (an Attacker unit 10.04 s); four soldiers in 35.8 s (an Attacker unit 12.9 s) | | `UnitAbilityTests` |

### 16.7 Main menu and the Army screen

The menu has two buttons: ARMY (bottom left, with the squad's glyphs above it) and ATTACK! (the level ladder), plus settings in the top bar, which shows the player plate and gold only. Research, Base and Profile have no menu entry; their screens and code are kept for tests.

| Rule | Value | Field |
|---|---|---|
| Base features | off: no simulated raids are generated (no gold lost, no raid report), no Research, Base or Profile entry; existing research multipliers stay in effect, none can be bought | `MetaGame.BaseFeaturesEnabled` |
| Units | unlocked by clearing levels only (16.6); no gold is spent on units | `Progression.IsUnlocked` |
| Unit cards | six, in unlock order: glyph on the accent color, name, description, four bars; ADD puts the unit in the first empty slot; a locked card reads "Clear Level N to unlock" | `UnitCard`, `SquadBuilderPanel` |
| Stat bars | ratio to the Attacker: Health `healthMult`, Damage `damageMult x soldierDamageMult / fireIntervalMult` (0 and "Heals" without a weapon), Range `rangeMult`, Speed `speedMult`; fill = ratio / (1 + ratio), so the Attacker sits at half (the tick on each track) | `UnitStatBars` |
| Full squad | ADD with no empty slot shows "Squad is full. Click a slot to remove a unit first." | `SquadBuilderPanel.FullMessage` |
| NEW tag and Army dot | a type unlocked by a clear is NEW until the Army screen has been opened once; the Army button shows a red dot while any type is NEW | `Progression.NewUnits`, seen-list ids `army_seen_<type>` |
| One-time callouts | home: "You unlocked a new unit. Open Army." (`menu_army`); inside Army: "Add the Healer to your squad." (`menu_add_unit`); both in the tutorial seen-list | `MenuGuide`, `MetaGame.ShowArmyGuide`, `ShowAddUnitGuide` |
| Heroes | the mercenary roster on the Army screen's second tab (unlock and hire with gold) | `ArmyScreen` |
