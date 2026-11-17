# Reference versus build: comparison notes (phase 4 and 8 pass)

Images in this folder are side by side pairs built with ImageMagick: reference on the left, our build on the right,
same height, labeled. Our captures come from the PlayMode capture tests (1280x720 render target, editor, M2 Mac).
These are eyeball notes, not measurements; no similarity score is claimed. The reference is a 2D sprite game; ours is
3D under an orthographic camera (owner requirement), so pixel identity is not the goal.

## 01_gameplay_overview.png (ps_12.png vs battle_u_formation.png)
- Matches: U shaped single file squad open toward the camera with the ring and spiral blot inside it; enemy squads with
  skull pennants and bubbles above; pause tab top left, round squad badge top right; dotted paint streams; team colors
  are the most saturated things on screen; orthographic read, similar soldier size (about 22 to 26 px at 720p).
- Differs: biome. ps_12 is the grey lava biome with orange cracks; our default map is the sand/olive biome from the
  contract, so ground hue and contrast differ strongly. Our framing shows dense forest borders on both sides; ps_12 has
  open ground with tents and barricades. The reference has many more enemies spread in loose arcs around the squad; ours
  cluster tightly around their bearers. Reference bubbles sit inside a thick red C shaped arc; ours have a thin dashed
  track that fills only as the bearer takes damage. Our HUD has extra elements (capture pill, objective marker, POW
  badge always shown, soldier count) that ps_12 does not show.
- Fixed this pass: soldiers now lean back toward the camera and never turn fully away, so the face, body and gun read
  instead of helmet tops; ring texture about 50 percent thicker; spiral blot larger; bubble shows a pale milky interior
  behind the bearer; darker teal trees; stronger hex seams; softer dirt path band.
- Remains: lava biome and crack glow (not built), enemy spacing and count, thick bubble arc, HUD extras.

## 02_gameplay_ring.png (ps_06.png vs battle_formation_circle.png)
- Matches: red ring with spiral blot, squad in a circle formation, enemy squads with pennant flags around, mine discs.
- Differs: ps_06 ring is much larger and thicker relative to the squad, and soldiers stand loosely inside it; ours puts
  the soldiers on the ring line itself in a tight circle. ps_06 enemies are green on a grey-green biome with rain; ours
  are teal on sand. Our capture also shows a "SQUAD POPPED!" toast and more splats.
- Fixed this pass: same soldier, ring, splat and bubble changes as above; splats are now clumpy blobs with droplets.
- Remains: ring scale relative to formation, rain, biome.

## 03_soldier_closeup.png (ps_12 crop vs our crop at the same screen relative scale, 4x nearest)
- Matches: helmet dome with a white rim, team colored body, a gun stub to the side, roughly the same pixel height, dark
  boots, soft ground shadow.
- Differs: the reference sprite is darker and more compact (dark body and legs, dark outline), ours has a white sticker
  edge around the whole helmet and body and a brighter, busier read; our faces are visible as white patches but the eyes
  rarely resolve at this size; reference soldiers on the top row show their backs, ours show faces or 3/4 profiles
  (intended by the brief).
- Fixed this pass: visual lean (30 degrees) and visual yaw clamp (80 degrees) in `SoldierView`; dark 1 px outline on
  face, gun and boots; white edge kept on team colored parts. Before this pass the same crop showed only red domes.
- Remains: eyes and face plate are not enlarged (the soldier was not re-exported from Blender); white edge is heavier
  than the reference.

## 04_formation_drawing.png (f_0056 vs battle_stroke_drawing.png)
- Matches: dashed red stroke on the ground, soldiers streaming into a single file chain, ring and blot nearby, HUD
  corners.
- Differs: f_0056 shows a thin dashed line well ahead of the chain on an empty lava field; our capture happens mid fight
  (after the new U capture) so the stroke is partly hidden by soldiers and splats. Our stroke has a soft glow ribbon the
  reference does not have.
- Fixed this pass: live streaming (soldiers queue in a column back toward the squad instead of clumping while the
  stroke is short), solid inner line through the dashes, narrower glow.
- Remains: the glow ribbon, a cleaner standalone stroke capture.

## 05a_hud_pause_tab.png and 05b_hud_squad_badge.png
- Matches: dark slate pause tab with "II" and a rounded corner top left; round badge top right with a white ring and
  three soldier busts.
- Differs: reference badge has white soldier busts on a grey disc with a partial red fill arc; ours uses red helmeted
  busts on a red disc, plus a separate POW hex badge and a soldier count pill. Post processing (vignette, grading) still
  applies to our HUD: an overlay UI camera was tried and reverted because the HUD disappeared from render texture
  captures.
- Remains: badge art closer to the reference, UI excluded from post on device builds.

## 06_victory_screen.png (f_0086 vs ui_victory.png)
- Matches: white rounded card, currency row with star coin and EXP style counters, grade letters in red circles, gold
  medal with a grade letter, bold rounded display font.
- Differs: reference has a red banner strip with a soldier walking to a target, four stat icons (medals, survivors,
  combos, time), a grey VICTORY plate on a red ribbon and a home and next button pair; ours has a red ribbon title, three
  stats, a level up bar and REPLAY and CONTINUE pills. Our HUD fades behind the card.
- Fixed this pass: nothing on this screen (out of scope); a world space gold coin burst now plays on victory.
- Remains: banner strip, medal ribbon layout, button set.
