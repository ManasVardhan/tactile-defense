# Tactile Wars (2015, Ankama, com.ankama.tactilwar) visual analysis

Scope: ORIGINAL Tactile Wars only, not "All Stars". Every observation below cites the image files it came from. Anything not visible in the images is listed under "Could not verify".

Evidence caveats (read first):
- Play Store images are marketing composites. ps_01 to ps_05 (800x480) and ps_07 to ps_11 (1024x600) are the same five images at two sizes. Only ps_06 and ps_12 are plain gameplay screenshots (same image at 800x480 and 1024x600). The upper part of each composite is real gameplay, the lower-corner characters, logo text and splats are promo art and are NOT in-game scale.
- The video source is 640x360 (YouTube 360p was the only format that fit the size budget; the 720p60 stream was about 100 MB). Frames were upscaled to 1280 wide, so they are soft. Each f_NNNN.jpg is 1280x720 and includes a streamer facecam in the bottom-right corner, which is NOT game UI.
- The video is a player-versus-player ("Chi66992" level 20 vs "Mafia70077" level 21) match, game version v1.2.2 on the map screen (f_0091). Red is the player, purple is the opponent.
- Frame timing: f_NNNN are roughly 5 s apart (f_0001 near the start, f_0098 near 8:11). burst_A starts near 4:32, burst_B near 6:23, burst_C near 3:08, each 6 s at 10 fps (60 frames).
- Colors were sampled with magick 3x3 averages at single points, so they are approximate and affected by JPEG and upscaling.

## 1. Camera

- Projection: reads as orthographic or very low perspective. Evidence: in ps_12 the hex ground tiles are the same size at the top and bottom of the screen, rows of pine trees at the bottom edge (ps_12 bottom-left) are about the same size as trees at the top-left, and the lava-crack hex grid keeps constant cell size across f_0056 and f_0061. Walls (the long beige wall in f_0011) stay parallel and the same width from top to bottom of the frame. No obvious size change with depth in any frame. Caveat: stylised flat art could hide a mild perspective, so I rate this "likely orthographic", not proven.
- Pitch: estimated from flat ground decals drawn as ellipses. The red selection ring in f_0056 is about 450 px wide by 175 px tall (ratio about 0.39); in ps_12 the ring is about 310 by 155 (ratio about 0.5); hexes in ps_12 are about 2:1 wide:tall. Ratio equals sin(view elevation), so the camera looks down about 25 to 30 degrees below horizontal in these ellipses, i.e. a shallow oblique 3/4 view, about 60 to 65 degrees off straight-down. Buildings and soldiers are drawn as upright billboards facing the camera (ps_06, f_0056), not as top-down sprites. Treat 30 degrees as a starting value to tune by side-by-side with ps_12 and f_0056.
- Visible battlefield: soldiers are about 20 to 25 px tall in ps_12 (600 px high) and about 25 px in burst_A_040 (720 px high). That makes the screen roughly 25 to 30 soldier-heights tall and roughly 45 to 55 soldier-widths wide (aspect 16:9 at ~1.7 to 1.8). The whole squad plus about 2 to 3 enemy formations fit on screen at once (ps_12, ps_06). Hex ground cells are about 5 to 6 soldier-heights across in f_0056.
- Following: the camera pans with the squad. In m-montage frames f_0033 to f_0048 the terrain (spike trees, walls, lava cracks) scrolls while the squad stays in the middle third of the screen, and f_0053 to f_0056 show the squad at left of centre while the ring is to its right, so the follow is loose, not locked. Off-screen allies and enemies are shown as round helmet icons clamped to screen edges (purple helmet = enemy group, red helmet = own mercenary or group; f_0056, f_0061 to f_0064, burst_A_001, burst_B_001, burst_C_001). The camera is at constant zoom across all sampled frames (soldier size unchanged between f_0010 and f_0083).
- Pre-battle map screen is a zoomed-out hex territory overview with the same oblique tilt (f_0004, f_0005, ps_09).

## 2. Soldiers

- Proportions (from promo art ps_01, ps_05, ps_07, ps_11 and in-game ps_06, ps_12): chibi, helmet-dominant. The helmet dome is roughly half of total height, below it a white round face with two black dot eyes, then a very small body and legs. Head:body is about 1:1 or the helmet slightly larger; overall soldier is about as wide as tall (about 1:1.1). In-game sprites are tiny, so this proportion is verified mainly from the promo close-ups.
- Helmet: rounded dome (Brodie or "soup bowl" style) with a thick rim and a small skull emblem on the front in the promo art (ps_01, ps_02, ps_05). In-game the helmet reads as a solid saturated dome with a white edge around it (f_0056, burst_A_040, ps_12). The leader/mercenary has a pointed spike on the helmet (ps_04, ps_10; also a red unit with a pointed top in the middle of the squad in f_0056 and burst_B_030).
- Weapon: a short, chunky grey rifle or bazooka held at the front, almost as long as the body (ps_01, ps_02, ps_04, ps_05). In-game it appears as a small dark stub (ps_12, ps_06).
- Colors per team (see palette): red (player in video), purple (opponent in video, f_0013 to f_0040), teal (ps_12, ps_04 enemy), pink and yellow/orange (ps_02, ps_05 promos), green (ps_06 enemy, ps_09 base theme). Teams are recolors of one soldier shape; the color covers helmet and a matching ground ring or paint.
- Outline: in-game red soldiers carry a thin white outline or rim (f_0056, burst_A_040, ps_12 red soldiers). Promo art has a thin dark outline with flat cel shading (ps_04 bottom row shows white sticker-style outline on mercenary cut-outs). Enemy purple soldiers look darker with less visible rim (f_0013, burst_B_030).
- Shadow: promo and in-game units use a soft blurred dark ellipse under the unit (visible under the shielded flag-bearer bubbles in ps_01, ps_02, ps_04 and under the ring decal in ps_12). No hard cast shadows from the sun direction on individual tiny soldiers were visible.
- Size on screen: about 20 to 25 px tall at 600 to 720 px screen height (ps_12, burst_A_040); that is about 3.5% of screen height. Flag-bearer / leader "capture" units are drawn inside a larger glass bubble about 3x soldier size (ps_01, ps_02, ps_12, burst_B_030).
- Squad count: ps_08 heart formation has about 50 soldiers; ps_12 has about 50 to 60 red soldiers in an L shape; f_0056 and burst_A show 40 to 60 red soldiers. Enemy squads in grids of about 15 to 30 (f_0017, f_0038 to f_0040, burst_C). Squad looks like a few dozen up to about 60.
- Spacing in formations: soldiers are packed shoulder to shoulder with little or no gap along a drawn line. Heart stroke in ps_08: soldiers are about 1.0 to 1.3 widths apart centre to centre. Idle blob (f_0056, burst_A_040): heavily overlapped, about 0.4 to 0.7 widths apart. Enemy grids in f_0017 and f_0038: about 2 to 3 widths apart, regular rows and columns, with a flag in the grid.

## 3. Formation drawing

- Selection ring: a flat, thick, hand-drawn style ellipse in team color (red in f_0056, f_0061, burst_A_001; purple ring when the opponent holds it, f_0021, f_0022). The ring can be split between colors (red arc and purple arc, f_0023 and f_0025), and has a spiral "fingerprint" or swirl blot at its centre (f_0056, burst_B_030, ps_08, ps_12). The spiral is also in the Ankama logo glyph (ps_05, ps_11).
- Drawn stroke: in ps_08 the stroke is an inner solid pink line plus a dashed white outer line forming a heart. In the video the stroke appears as a thin dashed red line from the drag start (f_0056 shows a horizontal dashed red line from the ring blot to the left while soldiers stream down along a vertical line). A stroke is not shown as dots per soldier; soldiers themselves line up along it.
- Movement: soldiers march onto the line as a single-file column or chain (f_0056 vertical column about 20 soldiers long; f_0037, f_0060s montage frames f_0057 to f_0064 show the chain curving into a "C" or spiral). Chain shapes seen repeatedly: crescent or "C" (f_0041 to f_0048, f_0049 to f_0052), line (f_0061, f_0056), spiral or comma (f_0063), arc facing the enemy (f_0077, f_0078, f_0081), ring (ps_06), blob (f_0017), heart (ps_08).
- Timing from bursts (6 s at 10 fps): burst_A_001 to burst_A_060 show a single arc crescent splitting into two clumps within about 1 s (burst_A_001 versus burst_A_011), then reforming near a new ring about 3 s later (burst_A_040, ring drawn on the right). The squad lags the ring: soldiers trail the ring centre by a few soldier-widths and reach it over roughly 1 to 2 seconds. burst_B shows the squad staying in an arc while firing; the arc width is constant across 3 s, so soldiers hold station.
- Small bursts of red and white dots stream from the squad to the target (burst_B_001 to burst_B_030, f_0077 to f_0080); these are shots, not path markers.

## 4. Combat

- Projectiles: small round dots with a white core and a team-color edge (red-edged white dots from red soldiers in f_0056 area, burst_B, ps_12; purple/violet dots from enemies; cyan clusters in ps_01; pink in ps_08). Fired in dense streams of 5 to 20 dots (burst_B_001 to burst_B_030, burst_C_025). The size is about 3 to 4 px. Tracer trail is not visible; motion appears as a dotted line of discrete dots.
- Muzzle effects: white flash on front soldier (ps_04 mercenary with a white puff at the gun), yellow-orange muzzle flame in promo art (ps_05 cannon, ps_09 turret with a green flash), small white flash at the gun in ps_06.
- Paint splats on ground: team-colored blotches stay on the ground after combat (purple blotches in f_0021 to f_0028 and f_0049 to f_0052; red blotches and a trail across the ground in burst_C_001 to burst_C_060 and f_0037 to f_0040; teal blotches in ps_12; bright splashes on the ground in ps_01). Splats look like soft irregular paint with a few satellite drops. Soldiers also carry a splat of enemy color on the helmet in the promo (ps_05 yellow soldier with pink splat).
- Health bars: none visible on soldiers or structures in any sampled frame. Damage is conveyed by units disappearing.
- Damage numbers: no numeric floating damage. The only floating numbers are combo multipliers "x1" and "x2" next to star-coin pickups (f_0037, f_0077, burst_C_020, f_0083).
- Death effects: killed enemy units fade to a translucent ghost silhouette with a white flash and puff (f_0016, f_0032, burst_B_001 to burst_B_030, burst_C). Killed own soldiers leave a red splat and red ghost (f_0013 top; f_0014 red ghost near the squad). Destroying an enemy base or tank produces a white starburst plus dust (burst_C_015, f_0083). A white flag icon plants where a bubble leader/HQ is captured (f_0028, burst_B_015, burst_C_001). Star-coin tokens fall and spin out of a destruction (f_0037, f_0077).

## 5. Enemies and structures

| Type | Appearance | Relative size | Seen in |
|---|---|---|---|
| Enemy soldier | purple chibi helmet, tiny | 1.0 soldier | f_0013, f_0017, f_0021 |
| Flag-bearer in glass bubble | soldier with skull flag inside a bubble and a ring segment arc in team color | about 3x soldier, flag about 4x tall | ps_01, ps_02, ps_12, f_0022, f_0026, burst_B_001 |
| Fenced enclosure (purple) | rectangular grid of fence posts and soldiers with small skull flags; soldiers inside regular grid | about 15 to 20 soldier-widths long | f_0038 to f_0040, burst_B_001, burst_C_001 |
| Small tank (purple and red variants) | low boxy body, little turret, dark exhaust puffs; red variant has smoking pipes | about 6 to 8 soldier-widths long | f_0045 to f_0052, f_0073 to f_0076, burst_B_001 |
| Stone bunker / house | grey-beige stone block with arched door | about 8 to 10 soldier-widths | f_0041 to f_0044 (top-left), f_0045 |
| Walls | long beige crenellated block walls with rivets, plus wooden cross-barricades (X frames) | long, about 3 soldiers tall | f_0011, f_0017, f_0056 top, burst_A_001 |
| Cardboard boxes / crate debris | beige open cardboard boxes | 3 to 5 soldier-widths | f_0011, f_0017, f_0081 |
| Beige tile clusters | plus-shaped and Z-shaped groupings of hex tiles | 8 to 12 soldier-widths | f_0017, f_0056, f_0061 |
| Spike trees | dark grey or purple cone-leaf trees in spiky clusters | about 2 to 3 soldiers tall | f_0010, f_0041, f_0063, ps_01 |
| Pine trees (teal / olive / grey-green) | layered faceted cones | about 3 to 5 soldiers tall | ps_04, ps_06, ps_07 left, ps_12 |
| Spawn pads / holes | dark round hatch with a skull, ground decal | 2 to 3 soldiers wide | ps_06, ps_12 |
| Tents and wooden barricade | beige canvas tents plus wooden barrier at map edge | 6 to 8 soldiers wide | ps_12 (top and right edge) |
| Turrets (green faction, promo and map) | boxy, tank-treaded cannon with octopus emblem, large barrel | in promo ps_03, ps_09 | ps_03 and ps_09 (promo art and base-map icons only) |
| Mercenaries | spiked-helmet minigunner, cannon-backpack unit, ball robot with flag, white cube rocket, spinning-top disc | 1 to 1.5 soldiers | ps_04, ps_10, f_0004 (tray) |
| Pickups | gold star coin stacks (ps_03, ps_09), parachute crates (ps_05) | 2 soldiers | ps_03, ps_05, ps_09 |
| Volcano props | grey volcano cones with glowing crack tops | large | ps_03, ps_09 |

Not distinctly identified as such in any image: mines, a separate HQ building, capture-point flags other than the bubble-carried flags above. The pre-battle map shows a hexagonal territory board with orange hex tiles and base icons (f_0004, f_0005, ps_09), but a distinct "HQ" object was not unambiguous.

## 6. Environment

- Ground: warm grey-brown hex-tiled stone (f_0056 ground about #7C6F66, ps_12 ground about #7F7867), with glowing orange-red lava cracks in the hex seams (about #D05E53 where bright). Other biomes: sand/desert beige (ps_02, ps_10, about #C7B78D to #E5DCBF), teal-pine lowlands (ps_04, ps_10), green/olive (ps_06, ps_09), grey volcanic with fog (ps_03, ps_09). Biome changes by map.
- Ground texture: subtle hex outline pattern at low contrast (f_0056, f_0061), plus beige paved hex patches.
- Props: dark spiked trees, pine clusters, beige walls and cardboard boxes, tents (ps_12), parachute crates (ps_05), mushrooms/white flowers (ps_06 bottom-left).
- Map boundaries: long beige crenellated walls and a darker out-of-bounds fog (f_0009 hex transition, ps_09 shows fog and hex territory edges with dashed-brick borders between territories).
- Saturation and light: ground is low-saturation and mid-value so team colors (red, purple, teal, pink) pop as the only high-saturation things; soft ambient with no hard shadows. Lighting direction is not clear; there are no sun-cast shadows, only soft contact shadows.
- Rain: light rain streaks visible in ps_06 (diagonal streaks); not seen in video frames.

## 7. UI

- In-battle HUD (f_0056, burst_A_040, ps_06, ps_12): top-left is a dark slate pause tab with a rounded bottom-right corner (about 60x40 px at 1280 wide, ps_12 shows it as "II"). Top-right is a round badge about 100 px across: white ring on a grey/red disc with three soldier busts; the ring is partly filled red or white in different frames (f_0033 to f_0048) and switches to all-red with three dark soldiers in f_0056 and burst_A. In ps_06 a second round badge with a "POW" hex-icon appears to its left; it appears with a power-up state in f_0027 to f_0048 ("POW" in a grey hex badge). Edge icons: round dark helmet icons at left and right edges for off-screen groups (f_0056, burst_B). Bottom-right of every video frame is a facecam (not game UI). Tips: a white speech bubble "Tip: Keep your Mercenary alive." at bottom-left (f_0012).
- No visible health bars, ammo, or timers numerically in battle.
- Fonts: a chunky, slightly rounded, condensed bold sans in white with a dark outline for headings and "Fight!" and "Attack" (f_0005, ps_01, ps_02, ps_03 titles; f_0091 "Attack", "COLORS WAR" in f_0092). Player name and level in a more regular bold sans (f_0004, f_0091). Numbers on the victory panel are bold black on white (f_0086).
- Buttons: rounded rectangle slate or white pills (f_0005 header "VS" pill; f_0092 "COLORS WAR" white pill), red helmet-with-gun mascot icon as the "Fight!" and "Attack" button (f_0005, f_0091), circular icon buttons (settings gear f_0091), 4 small bottom tab icons on the map (f_0093).
- Pre-battle (f_0004, f_0005): hex map overview, header with player left (name, level, red crest), "VS" pill centre, opponent right (purple crest), bottom tray of locked mercenary cards (padlocks) with one active red mercenary.
- Loading/splash: "VS" splash with red crowd versus purple tank wall and halftone, with caption text bar at bottom (f_0008). Tip bar with a round hint icon.
- Victory screen (f_0085 empty, f_0086 filled): white card with a top bar (gold star coin x23, EXP x298, rank icon +21, a red flag tab), a red banner strip with a bullseye target and a red soldier moving toward it, icon row (MEDALS, SURVIVORS, COMBOS, TIME), grade letters in red circles (S, C, S, B), a gold ribbon medal with "A", a home button and a red-to-purple arrow button. Before values count up the card shows 0 and a purple variant appears in f_0084 (the first panel frame is purple-tinted, probably a transition; not verified). Then a reward animation: soldiers carry a stack of gold coins on black with caption "Triumphant return of Chi66992" (f_0087 to f_0089).
- Defeat screen: not seen.
- World map (f_0090, f_0091): beige hex territories with lock icons, fractions like 75/140, "Attack" button, pickup gold star coins that fly to the top-right counter after victory, bottom row of 4 icon tabs, version "v1.2.2".
- Currencies: gold star coin (116, then 139 after the win: the +23 in f_0086) and a hex gem (59, then 69) at top right (f_0090, f_0091). "Colors War" menu with six faction hexes: purple, teal, green, orange, red, magenta (f_0094).
- Video outro (f_0093 to f_0098) is the streamer's end card, not game.

## 8. Color palette (sampled)

| Use | Hex (approx) | Source |
|---|---|---|
| Battlefield ground, lava biome | #7C6F66 | f_0056 (300,600) |
| Battlefield ground, lava biome | #7F7867 | ps_12 (500,150) |
| Lava crack glow | #D05E53 | f_0056 (325,298) |
| Red team selection ring | #DA4029 | ps_12 (331,322) |
| Red ring centre blot (video) | #B51021 | f_0056 (880,410) |
| Red HUD badge rim | #D51932 | f_0056 (1178,20) |
| Red enemy tank body | #A81A2E | burst_B_030 (205,190) |
| Dark spike tree | #272423 | burst_B_030 (208,595) |
| Teal soldier helmet | #229A8A | ps_12 (180,105) |
| Teal pine | #12443E | ps_12 (12,520) |
| Teal tree (sand biome) | #39826D | ps_10 (40,100) |
| Sand ground | #C7B78D | ps_10 (560,30) |
| Sand ground, light | #E5DCBF | ps_08 (700,300) |
| Pink stroke / blot | #DE368A | ps_08 (495,300) |
| Pink logo | #DB4793 | ps_11 (350,160) |
| Orange splat | #FFA300 | ps_08 (400,590) |
| Map paper beige | #CFC6A9 | ps_09 (400,260) |
| Map paper beige | #C9BFA6 | ps_09 (700,400) |
| Green-biome ground | #998D75 | ps_06 (300,100) |
| Red ring on green map | #C73A25 | ps_06 (410,240) |
| Sky-blue end card background (not game) | n/a | f_0093 |

Purple team tones: the purple ring and splats in f_0021, f_0022 and burst_B_030 read as a mid violet (about #9B6FB0 by eye) but my sampled pixels landed on mixed pixels (some gave #96595A), so I do not give a trusted hex for purple. Resample before using.

## 9. Could not verify

- Whether projection is truly orthographic (stylised art can hide perspective; only indirect evidence).
- Exact camera pitch and FOV (only an ellipse-ratio estimate).
- Native device resolution and true on-device soldier pixel size (images are marketing art or an upscaled 360p capture).
- Soldier legs and walk animation, weapon recoil, and the exact outline width (too small and too blurry).
- Individual soldier health or hit reactions; there are no visible health bars.
- Damage numbers (none seen); star-coin combo "x1"/"x2" only.
- Mines, a distinct HQ building, wall HP, turret firing behaviour in real gameplay (turrets and green faction only appear in promo art and map icons: ps_03, ps_09).
- Defeat screen, settings menu, mercenary shop, upgrade screens (not shown).
- Exact ground purple hex, exact lighting direction.
- Audio and haptics (images only).
- Timing of drawing: sub-second behaviour of soldiers joining a drawn path (bursts only cover three 6 s windows at 10 fps from a 30 fps 360p source).
- Whether the white/red/grey fill of the top-right round badge means squad strength, cooldown, or timer (it changes but is never labelled).
- Archived Play Store or other store galleries were not needed and not fetched, so no App Store images are included.

## 10. Sources

| URL | Status | Files |
|---|---|---|
| https://play.google.com/store/apps/details?id=com.ankama.tactilwar&hl=en_US | ok (HTTP 200, listing live, title "Tactile Wars - Apps on Google Play") | playstore/ps_01.jpg to ps_12 (page HTML kept only in scratch) |
| play-lh.googleusercontent.com image links from that page, fetched with =w1920 (the server returned 800x480 or 1024x600 originals) | ok | playstore/ps_01 to ps_05 (800x480), ps_06 (png 800x480), ps_07 to ps_11 (1024x600), ps_12 (png 1024x600). ps_07 to ps_11 are bigger copies of ps_01 to ps_05, ps_12 is the bigger copy of ps_06 |
| https://www.youtube.com/watch?v=g-kw1a31fmM | ok with a standalone current yt-dlp binary (pip yt-dlp 2025.10.14 failed with "The page needs to be reloaded"); format 134 (640x360, video only, 19 MB) | video_frames/source.mp4 (kept, 19 MB, under the 60 MB deletion threshold), f_0001 to f_0098, burst_A_001 to burst_A_060, burst_B_001 to burst_B_060, burst_C_001 to burst_C_060 |
| https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos (tool, 37 MB, kept outside this folder, never executed against downloaded media files other than as a downloader) | ok | none |
| web.archive.org, App Store, press kits | not attempted (Play Store and the YouTube video both succeeded) | none |

The video is a Chi6692 let's play; the frame set is from that one video only.

## 11. Best reference frames (for side-by-side with the Unity build)

1. playstore/ps_12.png: clean full-screen gameplay, HUD corners, red L-formation, teal enemies, lava hex ground, tents and barricades.
2. playstore/ps_06.png: clean gameplay on a green biome with rain, spawn pads, enemy leaders and a red ring.
3. playstore/ps_11.jpg: logo and clean promo of soldier faces and weapon shapes (character model sheet substitute).
4. playstore/ps_10.jpg: promo of the mercenary roster (spiked minigunner, cannon unit, ball robot, rocket cube, disc), red ring, teal pines.
5. playstore/ps_08.jpg: formation drawing (heart stroke, dashed outer line, pink blot, bubbled flag-bearers).
6. playstore/ps_07.jpg: purple-vs-cyan attack volume, lava-hex purple biome with splats.
7. playstore/ps_09.jpg: green faction turrets, base map with coins and volcano fog, territory borders.
8. video_frames/f_0056.jpg: full-res style squad column mid-draw, dashed path, ring, HUD (pause tab, squad badge, edge helmet icon).
9. video_frames/burst_B_030.jpg: firefight with purple fenced enclosure, bubble flag-bearer, red tank, dot projectiles, paint splats.
10. video_frames/burst_A_040.jpg: squad in several clumps with ring and HUD, clean ground texture for color matching.
11. video_frames/f_0086.jpg: victory panel with currency row and grade icons.
12. video_frames/f_0091.jpg: world map with Attack button, currencies, tab icons and territory locks.
