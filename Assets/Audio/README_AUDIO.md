# Tactile Wars audio

All audio in this folder is original, procedurally synthesized content created for this project
(license: project-owned). No third-party samples, recordings, or music were used or downloaded.

Regenerate everything with:

    python3 Tools/audio/synth_sfx.py     # SFX/*.wav
    python3 Tools/audio/synth_music.py   # Music/*.ogg (needs ffmpeg at /opt/homebrew/bin/ffmpeg)
    python3 Tools/audio/verify_audio.py  # ffprobe + volumedetect report

Scripts are deterministic (fixed RNG seeds). Requires python3 and numpy only.

Format: SFX are 44.1 kHz, 16-bit, mono WAV, peak-normalized to about -3 dBFS (so set per-category
volumes in Unity; frequent sounds such as paint shots should be played quietly with random pitch
0.9 to 1.1). Music is 44.1 kHz stereo OGG Vorbis, about -18 dBFS RMS.

## SFX (Assets/Audio/SFX)

| File | Intended use |
|---|---|
| paint_shot_01..04.wav | Soldier paint-gun shot (60 to 120 ms); pick a random variant per shot |
| paint_splat_01..04.wav | Paint hitting a unit/ground/wall (wet splat) |
| unit_pop_01..03.wav | Soldier eliminated ("poof") |
| turret_shot_01..02.wav | Turret fires |
| cannon_shot.wav | Cannon fires |
| tank_shot.wav | Tank fires |
| mine_explosion.wav | Mine detonates (boom plus paint burst) |
| building_hit_01..02.wav | Building takes paint damage |
| building_destroyed.wav | Building destroyed |
| footsteps_loop.wav | Seamless 1 s loop, squad moving |
| stroke_draw_loop.wav | Seamless 1 s loop while the player drags a formation stroke |
| formation_set.wav | Formation confirmed (finger released) |
| capture_tick.wav | Capture progress tick |
| capture_complete.wav | Point/building captured jingle (about 1 s) |
| reinforcement_ready.wav | Reinforcements available (chime) |
| reinforcement_call.wav | Reinforcements called (bugle fanfare, 0.8 s) |
| bubble_pop.wav | Bubble/shield pop |
| coin_pickup.wav | Coin collected |
| ui_click.wav | Generic button press |
| ui_back.wav | Back/cancel |
| ui_open.wav | Panel/menu opens |
| ui_purchase.wav | Purchase confirmed |
| ui_error.wav | Invalid action |
| level_up.wav | Level up / upgrade |
| victory.wav | Level won (2.9 s fanfare) |
| defeat.wav | Level lost (2 s descending sting) |

## Music (Assets/Audio/Music)

| File | Intended use |
|---|---|
| menu_theme.ogg | Menu/map loop, 32 s, 120 bpm, 16 bars, light playful march |
| battle_theme.ogg | In-battle loop, about 54.9 s, 140 bpm, 32 bars, driving version of the same family |

Both loops are whole bars with tails wrapped to the start. Import with Load Type "Streaming" or
"Compressed In Memory", and enable Loop on the AudioSource. Do not enable "Preload Audio Data" trimming
or any start/end offset, so the loop point stays sample exact.

Note: the OGG files were encoded with ffmpeg's native Vorbis encoder at 160 kbps, because the ffmpeg
build on the authoring machine has no libvorbis. synth_music.py uses libvorbis `-q:a 4` automatically
when available.
