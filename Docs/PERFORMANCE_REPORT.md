# Performance report (phase 8)

All numbers are EDITOR play mode measurements on an Apple M2 Mac (8 GB RAM), Unity 6000.3.22f1 in batchmode, rendering
into a 1280x720 render target with MSAA 4x. No device profiling has been done; phone numbers will differ (and the editor
adds its own overhead to CPU time and GC). Measured on 2026-11-17 by the PlayMode tests in category `Perf`.

## How it was measured
- `Assets/Tests/PlayMode/PerfSampler.cs`, used by `CaptureAndPerfTests.PerfCapture.FrameTime_FullBattle` (attack) and
  `DefensePerfCapture.FrameTime_HeavyDefenseBattle` (defense). Raw output: `Logs/perf_battle.txt`, `Logs/perf_defense.txt`.
- The earlier routine reported exactly 16.67 ms per frame for the attack battle because the project caps the frame rate
  at 60 (`Application.targetFrameRate = 60`); that number only showed the cap. The sampler now sets
  `Application.targetFrameRate = -1` and `QualitySettings.vSyncCount = 0` for the window, waits 10 frames, then records
  300 frames. Per frame it records wall time (including one explicit `Camera.Render()`, because batchmode has no game
  view), the Profiler "Main Thread" recorder (CPU main thread), "GC Allocated In Frame", and the render counters
  "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", plus "Texture Memory" and
  "Total Used Memory".
- Attack: Battle.unity, handcrafted base one, 40 player soldiers, 70 enemy soldiers in 7 squads, defenses active, a tap
  sends the squad into the first firefight 2.5 s before the window.
- Defense: heavy player base (every defense type, 8 mines, 3 defender squads), home map with 714 runtime props, AI
  attackers engaged.

## Results
| Metric | Attack battle | Defense battle (heaviest scene) |
|---|---|---|
| CPU main thread (Profiler) avg / median / p95 / max | 9.81 / 9.61 / 11.31 / 12.94 ms | 7.18 / 7.18 / 7.64 / 10.42 ms |
| Wall time per frame incl. extra render, avg / p95 | 9.81 / 11.18 ms | 7.21 / 7.62 ms |
| Batches (median) | 4942 | 4022 |
| SetPass calls (median) | 1901 | 1150 |
| Draw calls (median) | 4990 | 4081 |
| Triangles (median) | about 1.30 M | about 1.01 M |
| GC alloc per frame avg / median | 523 / 418 bytes | 567 / 480 bytes |
| Texture memory (editor process) | 310.5 MB | 309.4 MB |
| Total used memory (editor process) | 1037 MB | 1028 MB |
| Peak live projectiles | 47 | 44 |

Notes on reading these:
- The attack battle is the heavier CPU case here (more soldiers in combat); the defense scene has more props but fewer
  units firing in the window.
- Batches and draw calls are very high for a mobile target. Every soldier is a skinned mesh with 5 to 7 material slots
  and an outline pass, splats and props are individual renderers, and the forest border is ~700 instances without
  static batching or GPU instancing of the trees in this measurement. The SRP batcher keeps SetPass calls lower than the
  draw count, but on a phone this would be the first thing to cut (merge soldier materials into one atlas material,
  combine or instance the forest, cap splats lower).
- Texture memory includes editor-loaded assets (UI art renders, fonts, editor resources) and is not the player build
  footprint.
- GC per frame includes editor and test harness overhead; gameplay code paths were written to be allocation free
  (pools, preallocated buffers) but this was not isolated with a player build profile.
- Frame cap: the shipping configuration keeps 60 fps capped; the uncapped numbers above are for headroom only.

## Not done
- No device (iOS or Android) profiling, no GPU timing, no thermal test, no player build memory snapshot.
