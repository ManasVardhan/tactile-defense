# Tactile Wars Recreation: Visual Style Guide

Evidence labels: **VERIFIED** (seen in a named reference image, see `Docs/References/VISUAL_ANALYSIS.md`, called VA), **ESTIMATE** (our target, tune by eye), **UNVERIFIED** (no evidence). Reference frames are marketing composites and one upscaled 360p let's play, so all pixel measurements are approximate.

## 0. The 2D versus 3D decision (read first)

The original renders 2D sprites on a hex-tiled ground: soldiers and buildings are upright billboards facing the camera, ground decals are flat ellipses (VERIFIED by inspection of ps_12, ps_06, f_0056; VA section 1). It is not stated by any text source (RM section 8 conflicts: one outlet says 2D, another 3D).

This recreation uses **3D models under an orthographic camera**. This is an owner requirement, not an attempt to match the original's technique. The goal is to reproduce the same visual read.

Consequences of the choice:
1. **Lighting exists.** 3D models shade; the original looks flat-lit with soft blob shadows. We use a mostly ambient, low-contrast setup and unlit-style toon shading with a faint rim, to avoid hard directional shading that the original does not have.
2. **Orientation matters.** Sprites always face the camera. 3D soldiers face their movement or fire direction, so from some angles you see backs. Mitigation: helmet dome dominates the silhouette from every angle; the face (white with two dots) is on a front-facing mask. Accept that backs are visible, which the original never shows.
3. **Billboard trees vs real trees.** Trees and props are modeled with low poly cones and rotate nothing, so their perspective under the oblique camera differs slightly from flat art. Acceptable.
4. **Ground decals must be real decals or quads.** Rings, splats and shadows are flat quads or URP decal projectors on the ground plane so they foreshorten like the ellipses in the originals (ring about 2:1 width to height is VERIFIED, VA section 1).
5. **Cost.** 40 to 60 skinned soldiers plus projectiles on an M2 with 8 GB RAM. Budget: soldier under 2.5k tris (contract), GPU instancing, no real-time shadows on soldiers (blob shadows only), animation via simple Animator or GPU instanced vertex animation if needed.
6. **Production effort.** Every asset is modeled and rigged in Blender and animated, versus sprite sheets. Assets stay small and flat-colored with no textures.
7. **Reference comparison caveat.** Pixel-matching screenshots is not possible; we compare silhouette, color, size ratios and layout, not texture.

## 1. Camera

| Property | Target | Status | Source |
|---|---|---|---|
| Projection | Orthographic | VERIFIED as "likely orthographic" (constant hex size top to bottom, ps_12; wall width constant f_0011) | VA section 1 |
| Elevation | 35 degrees above horizon (allowed 30 to 40) | ESTIMATE; ellipse ratio of rings gives 25 to 30, contract allows 30 to 40 | VA section 1: ring 450x175 px in f_0056 |
| Yaw | looks down +Z | contract | ARCHITECTURE_CONTRACT |
| Visible height | about 28 soldier-heights (25 to 30) | VERIFIED range | VA section 1: soldiers about 3.5 percent of screen height |
| Visible width at 16:9 | about 50 soldier-widths | VERIFIED approx | VA section 1 |
| Zoom | constant during battle | VERIFIED | VA section 1 (f_0010 vs f_0083) |
| Follow | loose pan on squad centroid, squad in middle third | VERIFIED | VA section 1 |
| Off-screen indicators | round helmet badge clamped to screen edge, team colored | VERIFIED | f_0056, f_0080 |

With soldier height 1.0 unit, orthographic size = 14 gives 28 units visible. Tune against ps_12.

## 2. Soldier proportions

VERIFIED (VA section 2): chibi, helmet-dominant. Helmet dome about half the total height; below it a white round face with two black dot eyes; a very small body and legs. Overall about as wide as tall (about 1:1.1). Thin white rim around the helmet in game (f_0056, ps_12). Chunky short grey gun almost as long as the body (promo ps_01, ps_05; small dark stub in game). Soft dark ellipse shadow beneath (ps_12). Leader/mercenary has a pointed spike on the helmet (f_0080, ps_04).

| Part | Spec | Status |
|---|---|---|
| Total height | 1.0 unit | contract |
| Helmet | rounded dome, 0.5 unit tall, 0.8 wide, white rim, team-color material `TeamColor` | VERIFIED look, sizes ESTIMATE |
| Face | white disc on the front, 2 black dot eyes, material `Skin` for white | VERIFIED |
| Body | tiny, `TeamColorDark` or `Dark` | VERIFIED |
| Legs | two small stubs, run cycle | ESTIMATE (walk animation UNVERIFIED, VA section 9) |
| Gun | grey `Metal` with `Dark` body, length about 0.7 | VERIFIED look |
| Skull emblem on helmet front | optional, tiny, may be dropped at in-game scale | VERIFIED in promo only (ps_01, ps_02) |
| Triangles | under 2.5k | contract |

## 3. Palette

Hex values are samples from the analysis and are approximate (JPEG and upscale error). Purple is NOT trusted (VA section 8). Teal and green values come from references; others are our picks.

| Use | Hex | Status | Source |
|---|---|---|---|
| Ground, lava biome (default) | #7C6F66 | VERIFIED sample | f_0056 |
| Ground, lava biome alt | #7F7867 | VERIFIED sample | ps_12 |
| Ground, green biome | #998D75 | VERIFIED sample | ps_06 |
| Ground, sand biome | #C7B78D | VERIFIED sample | ps_10 |
| Ground, sand light | #E5DCBF | VERIFIED sample | ps_08 |
| Contract ground default | #B9AD8A to #C7B78D | contract | ARCHITECTURE_CONTRACT |
| Lava crack glow | #D05E53 | VERIFIED sample | f_0056 |
| Map paper beige | #CFC6A9 | VERIFIED sample | ps_09 |
| Red team ring | #DA4029 | VERIFIED sample | ps_12 |
| Red ring blot (video) | #B51021 | VERIFIED sample | f_0056 |
| Red HUD badge rim | #D51932 | VERIFIED sample | f_0056 |
| Red tank body | #A81A2E | VERIFIED sample | burst_B_030 |
| Teal helmet | #229A8A | VERIFIED sample | ps_12 |
| Teal pine dark | #12443E | VERIFIED sample | ps_12 |
| Teal tree light | #39826D | VERIFIED sample | ps_10 |
| Pink stroke and blot | #DE368A | VERIFIED sample | ps_08 |
| Pink logo | #DB4793 | VERIFIED sample | ps_11 |
| Orange splat | #FFA300 | VERIFIED sample | ps_08 |
| Green faction | #8DB52B | contract value from visual review, ps_06 enemy | ARCHITECTURE_CONTRACT |
| Dark spike tree | #272423 | VERIFIED sample | burst_B_030 |
| Purple faction | #8F63B0 | ESTIMATE (eye match, VA says do not trust samples) | VA section 8 |
| Blue faction | #3F7FD0 | ESTIMATE (no reference sample; teal is the observed cool team) | none |

Team palette asset (`TeamPalette`): primary, dark (primary at about 65 percent value), paint (primary, slightly lighter and more saturated).

| Faction | Primary | Dark | Paint | Status |
|---|---|---|---|---|
| Red | #DA4029 | #9A2A1A | #E85A42 | primary VERIFIED, others ESTIMATE |
| Blue (teal reference) | #229A8A or #3F7FD0 | #166D61 | #3DB8A6 | ESTIMATE, see GDD D19 |
| Green | #8DB52B | #5F7F1B | #A3CC3A | primary from contract, others ESTIMATE |
| Orange | #F59A1A | #B36F0E | #FFB13A | ESTIMATE (splat #FFA300 VERIFIED) |
| Pink | #DB4793 | #9C2F69 | #EC66AA | primary VERIFIED |
| Purple | #8F63B0 | #64447D | #A982C6 | ESTIMATE |

Rule (VERIFIED, VA section 8): ground is low-saturation mid-value so team colors are the only strongly saturated objects. Keep props desaturated: walls beige, trees dark teal or olive or near-black spikes, tents canvas cream.

## 4. Ground treatment

- Muted sand/olive or warm grey-brown base with a subtle hex tile pattern at low contrast (VERIFIED, VA section 6: f_0056, f_0061). Hex cells are about 5 to 6 soldier-heights across (VERIFIED, VA section 1: f_0056).
- Optional biome accent: orange-red lava cracks in the hex seams (VERIFIED, VA section 6). Default biome for the first build: the sand/olive one from the brief; lava biome as a second map.
- Beige paved hex patches and plus or Z-shaped hex clusters as ground decoration (VERIFIED, VA section 5: f_0017, f_0056, f_0061).
- Implementation: a tiling hex pattern shader (`Art/Shaders`) with 2 to 3 value steps per tile and thin darker seams; no texture needed. Tile ID drives a small random value shift. ESTIMATE: tile jitter about 3 percent.
- Out of bounds: darker fog and long crenellated beige walls at map edges (VERIFIED, VA section 6).
- Ground decals sit just above y = 0 (z-fight avoided via small offsets or decal projectors).

## 5. Props

VERIFIED looks (VA section 5), all low-poly flat color:
- Pine trees: layered faceted cones, teal, olive or grey-green, 3 to 5 soldier-heights tall (ps_04, ps_06, ps_12).
- Spike trees: dark grey or purple cone clusters, 2 to 3 soldier-heights tall (f_0010, f_0056).
- Canvas tents, cream (ps_12).
- Wooden X-frame barricades (ps_12, f_0011).
- Beige cardboard boxes, open (f_0011, f_0017).
- Long beige crenellated block walls with rivets, about 3 soldiers tall (f_0011).
- Stone bunker with arched door, 8 to 10 soldier-widths (f_0041).
- Spawn pads: dark round hatch with skull, 2 to 3 soldiers wide (ps_06, ps_12).
- Dark disc skull marks scattered on the ground (ps_12). The original does not show them detonating (VA section 9). We use them as mines.
- Small pennant flags with skull emblem on a pole (ps_12, f_0080).
- Mushrooms and white flowers, tiny (ps_06).
- Not distinctly seen: HQ building, mines in motion, geysers, tesla antennas (UNVERIFIED looks; our designs).

## 6. VFX language

| Effect | Spec | Status |
|---|---|---|
| Projectiles | tiny round dots, white core, team-colored edge, dotted stream, no trail | VERIFIED (VA section 4: burst_B, ps_12) |
| Muzzle | small white flash at the gun; larger orange flash for artillery | VERIFIED look in promo (ps_04, ps_05, ps_09), in-game small white flash (ps_06) |
| Paint splats | persistent team-colored irregular blobs with a few satellite drops on the ground, soft edges | VERIFIED (f_0021 to f_0028, ps_12) |
| Splat cap | 300 decals live, oldest fade over 2 s | ESTIMATE (`Combat/PaintSplatManager`) |
| Splat on units | team-colored splash on helmet in promo | VERIFIED promo (ps_05), optional in game |
| Death | translucent team-colored ghost wisp rises and fades, white flash and puff | VERIFIED (f_0016, f_0032, f_0014) |
| Structure destruction | white starburst plus dust, star coins fall and spin | VERIFIED (burst_C_015, f_0083, f_0037, f_0077) |
| Capture | white flag plants at the capture point, ring changes color, ring split in team colors while contested | VERIFIED (f_0023, f_0025, f_0028) |
| Squad ring | big flat team-colored ellipse outline (thick, slightly hand-drawn edge) with an irregular spiral paint blot at center | VERIFIED (f_0056, ps_12, ps_06) |
| Drawn stroke | solid inner line plus dashed white outer line, team-colored | VERIFIED (ps_08); video shows thin dashed red line (f_0056) |
| Bubble | glass sphere about 3 soldier-widths with team-colored arc ring on the ground and a pennant | VERIFIED (ps_12, f_0080) |
| Rain | light diagonal streaks in the green biome only | VERIFIED once (ps_06), optional |
| Combo numbers | "x1", "x2" near star coin pickups | VERIFIED (VA section 4) |
| Damage numbers, health bars | none | VERIFIED absent |

## 7. UI language

VERIFIED (VA section 7: f_0056, ps_12, ps_06, f_0091, f_0086):
- Pause tab: dark slate with rounded bottom-right corner, top-left, about 60x40 px at 1280 wide, shows "II".
- Squad badge: round, top-right, about 100 px across at 1280 wide; white ring on a grey disc with three soldier busts; the ring partly fills in team color. In our build this is the reinforcement meter (GDD D7).
- POW hex badge: grey round plate with a dark hex icon reading "POW", to the left of the squad badge when a power-up state is active.
- Off-screen group indicator: dark round plate with a helmet bust in the group's team color.
- Typography: chunky, slightly rounded, condensed bold sans in white with a dark outline for headings and button labels; regular bold sans for names and levels; bold black on white for numbers on cards.
- Cards: white rounded panels with a top bar of currency chips.
- Buttons: rounded rectangle pills (slate or white); big mascot-helmet icon as Fight and Attack button; circular icon buttons; 4 small tab icons on the map screen.
- Victory card: star coin count, EXP, rank, icon row (MEDALS, SURVIVORS, COMBOS, TIME), grade letters in red circles, gold ribbon medal, home button and next button (f_0086).
- Defeat screen: UNVERIFIED, our design.
- Tip bubble: white speech bubble with tip text at bottom-left during loading (f_0012).
- Nothing else in battle HUD: no timer, no health, no minimap (VERIFIED, VA section 7).

Fonts: SIL OFL fonts only. Candidates: Lilita One (display heading, chunky rounded), Fredoka or Baloo 2 (rounded body). Chosen fonts are ESTIMATE; they must be downloaded as OFL files from the open font sources, with the license file kept in `Assets/Art/Fonts`.

## 8. Lighting and post-processing targets

All ESTIMATE; the original shows soft ambient light, no hard cast shadows, only soft contact shadows (VERIFIED, VA section 6).

- Renderer: URP, forward, MSAA 4x, mobile-class quality.
- One directional light, intensity 0.8, color warm white (#FFF3DC), euler (50, -30, 0), no real-time shadows on soldiers. Ambient: gradient, sky #D8D2C0, equator #B7AE98, ground #8E8672 (ESTIMATE).
- Shading: simple toon or lightly banded diffuse (2 to 3 steps), faint fresnel rim to keep silhouettes bold. Thin white rim on helmets (VERIFIED look).
- Blob shadows: soft dark ellipse quad under every unit, alpha about 0.35, scale about 1.1 soldier widths.
- Post: very gentle. Bloom intensity 0.15 threshold 1.0 (only for lava cracks and flashes); vignette 0.15; color grading with +5 saturation, no tonemapping extremes. No depth of field, no SSAO. Disabled on low quality.
- Color check: sample the rendered ground and compare to #C7B78D and #7C6F66 within a small tolerance; team color rings should be the highest-saturation pixels on screen.

## 9. Framing checklist for screenshot QA

A battle screenshot passes if: (1) orthographic and no perspective convergence; (2) 25 to 30 soldier-heights visible; (3) squad plus 2 to 3 enemy groups on screen; (4) ring ellipse ratio within 0.35 to 0.55; (5) no health bars or damage numbers; (6) HUD only has pause tab, badge, POW; (7) splats and dotted streams visible; (8) ground low saturation, teams saturated.
