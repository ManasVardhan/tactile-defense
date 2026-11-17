# Tactile Wars Recreation: Reference Comparison

Each row: what the references show, what the recreation targets, and the known deviation. Evidence from `Docs/References/VISUAL_ANALYSIS.md` (VA) and `Docs/References/web/RESEARCH_MECHANICS.md` (RM). Image filenames are relative to `Docs/References/playstore/` (ps_*) and `Docs/References/video_frames/` (f_*, burst_*). Playstore images ps_01 to ps_05 and ps_07 to ps_11 are marketing composites (lower-corner art is promo, not in-game scale). Video frames are 640x360 upscaled to 1280x720 with a streamer facecam bottom right (not UI). Status in the first column: V = verified in references, I = inferred, E = our estimate.

## 1. Camera

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Orthographic or very low perspective: constant hex size top to bottom (ps_12), constant wall width (f_0011) [I, "likely orthographic"] | Orthographic camera | Original is 2D sprites; ours is 3D (owner requirement) |
| Pitch about 25 to 30 degrees from ring ellipse ratio, ring 450x175 in f_0056, 310x155 in ps_12 [V measurement, I conclusion] | 35 degrees (range 30 to 40) | We chose the higher end so 3D soldiers keep a readable face and top; ring ratio will be about 0.57 at 35 degrees versus 0.39 to 0.5 observed. Tunable. |
| Soldiers about 3.5 percent of screen height, screen about 25 to 30 soldier-heights tall (ps_12, burst_A_040) [V approx] | orthoSize 14 for 28 units | Exact device resolution unknown |
| Constant zoom, loose follow, squad stays in middle third (f_0010 vs f_0083, f_0033 to f_0048) [V] | Smooth damp follow on centroid | Follow smoothing time is E |
| Off-screen allies and enemies shown as edge-clamped helmet badges (f_0056, f_0080) [V] | Same, colored per team | None known |
| Pre-battle map is a zoomed-out hex territory view (f_0004, f_0005, ps_09) [V] | Simple hex map screen in MainMenu | Our map is a stub list of AI bases, not a Colors War map |

## 2. Characters

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Chibi, helmet about half the height, white face, two dot eyes, tiny body (ps_01, ps_05, ps_06, ps_12) [V from promo, small in game] | Soldier.fbx with the same proportions | In-game sprite detail is too small to verify legs, walk cycle or outline width (VA section 9); ours is invented |
| Thin white rim on helmet in game (f_0056, ps_12) [V] | Rim light or outline in shader | Rim is a shader effect rather than a sprite outline |
| Chunky grey gun, almost body length (ps_01, ps_05) [V promo] | Soldier gun mesh | In game it is a dark stub (ps_06); our gun is slightly larger for 3D readability |
| Teams are recolors of one soldier (ps_12 teal, f_0056 red, ps_06 green) [V] | `TeamColor` material swap | Purple hex not trusted (VA section 8); ours is E |
| Leader/mercenary with pointed helmet spike (ps_04, f_0080) [V] | Optional mercenary with a spiked helmet | Names and abilities UNVERIFIED |
| Soft blob shadow under units (ps_12) [V] | Blob shadow quad | None |
| Squad 40 to 60 on screen (ps_08 about 50, ps_12 about 50 to 60, f_0056) [V approx] | Start 40, cap 60 | Real cap and rules unknown |

## 3. Formation drawing

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Tap to move, draw to change formation (RM section 2) [V] | Same, via `PointerInput` | Tap/drag thresholds E |
| Heart stroke with inner solid pink line and dashed white outer line (ps_08) [V, promo composite] | `StrokeRenderer` draws solid plus dashed | The in-game video shows only a thin dashed red line (f_0056); we use the promo style, E |
| Soldiers line up along the stroke, about 1.0 to 1.3 widths apart (ps_08) [V approx] | Spacing 1.15 widths | Heart-spacing measured on a promo image |
| Idle blob 0.4 to 0.7 widths apart (f_0056, burst_A_040) [V approx] | 0.55 widths | None known |
| Chain formation: soldiers march onto the line single file, forming C, spiral, line, arc, ring (f_0056, f_0061, f_0063, f_0077, ps_06) [V] | Staggered slot arrival along the stroke | Timing "1 to 2 s" from 10 fps burst of a 30 fps 360p source, so sub-second behavior is E |
| Ring with spiral blot at the squad anchor; ring lags the squad (f_0056, burst_A_001 to 040) [V] | `SquadRing` prefab, `settleSeconds` 1.5 | Exact ring size and lag E |
| Shape recognition (lines, circles, squares) (RM section 2, App Store text) [V as feature, algorithm UNVERIFIED] | Line, circle, square, free recognizer | Algorithm and snapping rules are ours; the original may not snap at all |
| Arrow formation exists (mmos) [V text only] | Treated as a free shape | No special arrow behavior |

## 4. Combat

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Small round dots, white core, team-colored edge, dotted streams of 5 to 20, no trail (burst_B, ps_12, f_0056) [V] | Pooled dot projectile | Dot size, speed, rate are E |
| White muzzle flash small in game (ps_06), orange flames in promos (ps_05, ps_09) [V] | White flash for soldiers, orange for artillery | None known |
| Persistent team-colored paint splats on the ground (f_0021 to f_0028, ps_12) [V] | Decal splat manager with cap | Cap E; original may persist for the whole battle |
| No health bars or damage numbers; only "x1" "x2" combo numbers near coins (VA section 4) [V] | Same | None |
| Death: translucent ghost, white flash, puff (f_0016, f_0032, burst_B) [V] | Ghost and puff VFX | Durations E |
| Auto-fire versus manual aiming (RM section 2: multiplayer.it auto, press release "aim with finger") [conflict] | Auto-fire, facing from formation direction | Decision D1: manual aiming not implemented; UNVERIFIED whether the original had it |
| Flag-bearer in bubble, tap to wipe squad (RM section 1; ps_01, ps_12) [V] | Bubble with tap, lifetime E | Lifetime and expiry behavior E |
| Reinforcement bar fills from kills (RM section 1) [V]; top-right badge fill changes (f_0033 to f_0048) [V, meaning I] | Badge acts as meter, POW as summon | POW function UNVERIFIED |
| Capture and white flag plant (f_0028, burst_B_015) [V] | Capture ring and flag plant | Capture time E |
| Lose when all units die (RM section 1) [V] | Same | None |

## 5. Structures

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Small tanks with exhaust puffs (f_0045 to f_0052, burst_B) [V] | Tank.fbx | Behavior E |
| Stone bunker with arched door (f_0041) [V] | Bunker.fbx | Behavior E |
| Turrets with tank treads, octopus emblem, large barrel (ps_03, ps_09 promo and map icons only) [V look, in-game behavior UNVERIFIED] | Turret.fbx without the emblem | Our own design |
| Fenced enclosures with soldiers in a grid and flags (f_0038 to f_0040, burst_C_001) [V] | Enemy squad formations around a flag | Not a separate structure type in our build |
| Long beige crenellated walls, wooden X barricades, cardboard boxes (f_0011, f_0017, ps_12) [V] | Wall, Fence, Crate, Sandbags | Wall HP and function UNVERIFIED |
| Spawn pad holes with skull (ps_06, ps_12) [V] | Decal prop | Function unknown, decorative or spawn use |
| Dark disc skull marks on the ground (ps_12) [V look] | Mine.fbx | Mines "in motion" and detonation never seen (VA section 9); behavior E |
| Cannon, tesla antenna (names in RM section 4) [V names] | Our own designs | Appearance UNVERIFIED |
| HQ building not distinctly seen (VA section 5) | Optional HQ.fbx | UNVERIFIED that it exists |
| Pickups: gold star coins fall from destruction, parachute crates (f_0037, ps_05) [V] | Star coin pickup | Parachute crates skipped |

## 6. Environment

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Warm grey-brown hex ground with orange lava cracks (f_0056 #7C6F66, ps_12 #7F7867) [V] | Hex ground shader, secondary biome | Lava is a second map |
| Sand/desert biome #C7B78D to #E5DCBF (ps_02, ps_08, ps_10) [V] | Primary biome, sand/olive per brief | None |
| Green biome #998D75 with rain (ps_06) [V] | Optional third biome | Rain optional |
| Pine and spike trees, tents, barricades (ps_12, f_0010) [V] | Tree_A/B/C, Tent, Fence | Modeled in 3D, flat color |
| Map boundary walls and dark fog (f_0009, ps_09) [V] | Walls and a dark edge fade | Fog implemented as a vignette plane, E |
| Soft ambient light, no hard shadows (VA section 6) [V] | Ambient plus faint directional, blob shadows | 3D shading inevitably adds some form shading |

## 7. UI

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Pause tab top-left (f_0056, ps_12) [V] | Same | None |
| Round squad badge top-right with partial fill (f_0056, f_0080, ps_12) [V] | Reinforcement meter badge | Meaning inferred |
| POW hex badge (ps_06, f_0030, f_0080) [V look] | POW summon button | Function E |
| Chunky rounded outlined display font, white with dark outline (f_0005, ps_01 to ps_03) [V] | OFL font such as Lilita One | Original font is unidentified; ours is an open substitute |
| Victory card: gold x23, EXP x298, rank, MEDALS, SURVIVORS, COMBOS, TIME, grade letters, ribbon "A" (f_0086) [V] | Same layout with our assets | Grade formula E |
| World map with Attack button, locks, 75/140 fractions, tab icons, v1.2.2 (f_0090 to f_0093) [V] | Simple hex map and opponent list | No Colors War map |
| Colors War menu with six faction hexes (f_0094) [V] | Faction picker | Text sources list Blue and Pink where the menu shows teal and magenta (conflict) |
| Defeat screen [not seen] | Our design | UNVERIFIED |
| Pre-battle VS splash with tip bar (f_0008, f_0012) [V] | VS card and tip line | Tip text ours |

## 8. Audio

| Evidence | Recreation target | Known deviation |
|---|---|---|
| None. No source describes audio or music (RM section 8; VA section 9) | Synthesized soft pops, splats, stingers, two loops | Entire audio direction is E; the video had no usable audio analysis |

## 9. Meta

| Evidence | Recreation target | Known deviation |
|---|---|---|
| Gold, Prisms, XP, research locked to level and paid in gold; no energy (RM section 6) [V] | Same | Prisms earned free, no IAP (D13) |
| Four leagues, rank by wins (RM section 7) [V] | Four placeholder leagues | Names and thresholds E |
| Six color factions, cannot attack own color (RM section 7) [V] | `TeamColorId` six values | No faction perks |
| Alliances (App Store, press release) [V advertised], MMOs saw none [conflict] | Stub with synthetic roster | No real alliances |
| Async PvP against real bases (RM section 1) [V] | Seeded AI bases | No real players |
| Base editing with space and budget caps, consumed defenses rebought with gold (RM section 5) [V] | Same rules | Numbers E, simulated raids to consume defenses |
| Keys and chests (gamebrain, snippets) [V partial, may be post-launch] | Not included | Out of scope |
| Release date Aug 13 or 14, 2015, last version 1.7.8 in 2018; shutdown status UNVERIFIED (RM section 9) | n/a | n/a |

## 10. Cannot be verified from available references

- Exact squad size, cap, and loss or replenishment rules (RM section 3; VA section 2 gives only visual counts of about 40 to 60).
- Unit stats for any soldier, enemy, or defense (health, damage, range, fire rate, speed).
- Tech tree names, categories, costs, and effects (RM section 6).
- Mercenary names, abilities, costs (RM section 4; only promo cut-outs in ps_04, ps_10).
- Defeat screen appearance and its rewards (VA section 7).
- Mines in motion or detonating, geysers, tesla antennas, snipers, heli-towers, trackers in actual gameplay.
- The HQ building, if one exists (VA section 5).
- Timers: any battle timer, hold duration, bubble lifetime, capture time (RM section 1).
- Audio, music and haptics.
- Exact camera pitch, projection, native resolution, outline width, walk animation.
- Meaning of the top-right badge fill and the POW badge (VA section 9).
- Grade letter scale and star ratings (RM section 1).
- Exact line-to-slot mapping when drawing and whether strokes snap to shapes (RM section 2).
- Whether manual aiming exists.
- Budget and space numbers, grid size (RM section 5).
- Boss mechanics, key and chest economy, Color War map rules.
- Whether the Play Store screenshot gameplay matches current game versions; the let's play is v1.2.2.

## 11. Reference material that would help

Captures the project owner could supply (any device, screen recording preferred):
1. A native-resolution screen recording of a full battle in v1.7.x (clean, no facecam), at 60 fps, including the defeat screen.
2. A close-up recording of drawing a stroke and the squad reforming, ideally with touch dots visible, to fix spacing, timing and snapping.
3. Gameplay of attacking a base with mines, turrets, cannons, tesla antennas and a sniper, to see actual defense behavior.
4. Base editor screens showing grid, space and budget readouts, and the shop and research menus with costs.
5. The mercenary roster screen and a mercenary in action.
6. Audio from a battle and the menu (even a phone recording), to guide sound design.
7. The Colors War map, league screens, and alliance screens.
8. A recording of the reinforcement bar filling and being used, and what POW does.
