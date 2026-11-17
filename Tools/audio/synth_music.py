#!/usr/bin/env python3
"""Generates menu_theme.ogg and battle_theme.ogg (stereo 44.1 kHz, OGG Vorbis q4).
Run:  python3 synth_music.py      Output: Assets/Audio/Music/
Both loops are composed in whole bars; note/reverb tails are folded back onto the start and
reverb is circular, so the loop point is seamless."""
import os
import subprocess
import tempfile
import numpy as np
from synth_common import *

FFMPEG = "/opt/homebrew/bin/ffmpeg"


def _has_libvorbis():
    o = subprocess.run([FFMPEG, "-hide_banner", "-encoders"], capture_output=True, text=True).stdout
    return "libvorbis" in o


# libvorbis is preferred (-q:a 4). This machine's ffmpeg only ships the native (experimental)
# vorbis encoder, which needs -strict -2 and a target bitrate instead of a quality level.
ENC_ARGS = (["-c:a", "libvorbis", "-q:a", "4"] if _has_libvorbis()
            else ["-c:a", "vorbis", "-strict", "-2", "-b:a", "160k"])

# ------------------------------------------------------------------ composition (C major, 4/4)
# one token per eighth note: note name = new note, '-' = hold, 'r' = rest
MEL_A1 = [
    "E5 - G5 E5 C5 - E5 G5",     # C
    "A5 - G5 E5 C5 - D5 E5",     # Am
    "F5 - A5 F5 C5 - F5 A5",     # F
    "G5 - F5 D5 G4 - B4 D5",     # G
]
MEL_A2 = [
    "E5 - G5 E5 C6 - G5 E5",     # C
    "A5 - G5 E5 A5 - C6 -",      # Am
    "A5 - G5 F5 E5 - D5 C5",     # F
    "D5 - E5 D5 B4 - G4 -",      # G
]
MEL_B = [
    "C6 - A5 F5 A5 - C6 -",      # F
    "B5 - G5 D5 G5 - B5 -",      # G
    "G5 G5 E5 - G5 B5 - -",      # Em
    "A5 - E5 C5 E5 - A5 -",      # Am
    "A5 - F5 D5 F5 - A5 G5",     # Dm
    "B5 - A5 G5 F5 - D5 -",      # G
    "E5 - G5 E5 C5 - E5 G5",     # C
    "C6 - G5 E5 C5 - r r",       # C (last beat left for the snare fill that wraps into bar 1)
]
CH_A = ["C", "Am", "F", "G", "C", "Am", "F", "G"]
CH_B = ["F", "G", "Em", "Am", "Dm", "G", "C", "C"]

BASS_ROOT = {"C": 48, "F": 41, "G": 43, "Am": 45, "Em": 40, "Dm": 50}
TRIAD = {"C": [60, 64, 67], "F": [60, 65, 69], "G": [59, 62, 67], "Am": [60, 64, 69],
         "Em": [59, 64, 67], "Dm": [62, 65, 69]}


def parse_bar(s):
    toks = s.split()
    assert len(toks) == 8, s
    notes, cur = [], None
    for i, tk in enumerate(toks):
        if tk == "-":
            cur[2] += 1
        elif tk == "r":
            cur = None
        else:
            cur = [i, midi(tk), 1]
            notes.append(cur)
    return notes


class Song:
    def __init__(self, bpm, bars):
        self.bpm, self.bars = bpm, bars
        self.e = 60.0 / bpm / 2.0                 # eighth note seconds
        self.bar_s = self.e * 8
        self.N = ns(self.bar_s * bars)
        self.pad = ns(4.0)
        self.tracks = {}

    def track(self, name):
        if name not in self.tracks:
            self.tracks[name] = np.zeros(self.N + self.pad)
        return self.tracks[name]

    def add(self, name, snd, bar, eighth, gain=1.0):
        tr = self.track(name)
        i = int(round((bar * self.bar_s + eighth * self.e) * SR))
        j = min(len(tr), i + len(snd))
        if i < j:
            tr[i:j] += gain * snd[: j - i]

    def done(self, name):
        return fold_wrap(self.tracks[name], self.N)


def compose(song, melodies, chords, mode):
    """mode: 'menu' or 'battle'. melodies/chords: list of bar strings / chord names for every bar."""
    e = song.e
    nb = song.bars
    half = nb // 2
    kicks = [kick(1.0)]
    snares = [snare(1.0, 21 + k) for k in range(4)]
    hats = [hat(1.0, 31 + k) for k in range(4)]
    for b in range(nb):
        ch = chords[b]
        root = BASS_ROOT[ch]
        second_half = b >= half
        # ---- lead
        for (i, m, ln) in parse_bar(melodies[b]):
            d = max(0.25, ln * e + 0.15)
            snd = marimba(hz(m), d, decay=0.22 if mode == "menu" else 0.18)
            song.add("lead", snd, b, i, 0.55)
            if mode == "battle" and second_half:
                song.add("lead2", pluck(hz(m + 12), 0.3, 0.12, nh=4), b, i, 0.12)
            if mode == "menu" and b >= 4:
                song.add("lead2", pluck(hz(m - 12), 0.35, 0.16, nh=4), b, i, 0.07)
        # ---- bass
        if mode == "menu":
            pat = [(0, root, 0.55), (4, root + 7, 0.45)]
        else:
            pat = [(0, root, .6), (1, root, .35), (2, root + 12, .4), (3, root, .35),
                   (4, root + 7, .5), (5, root, .35), (6, root + 12, .4), (7, root, .35)]
        for (i, m, g) in pat:
            song.add("bass", pluck(hz(m), 0.34 if mode == "menu" else 0.22, 0.2 if mode == "menu" else 0.12, nh=5, bright=0.6), b, i, g)
        # ---- chord stabs (pizzicato-like)
        stab_slots = [2, 6] if mode == "menu" else [1, 3, 5, 7]
        for i in stab_slots:
            for m in TRIAD[ch]:
                song.add("stab", pluck(hz(m), 0.18, 0.07, nh=4), b, i, 0.17 if mode == "menu" else 0.12)
        # ---- arpeggio texture
        if (mode == "menu" and b >= 4) or (mode == "battle" and second_half):
            arp = TRIAD[ch] + [TRIAD[ch][1]]
            for k, i in enumerate([1, 3, 5, 7]):
                song.add("arp", pluck(hz(arp[k] + 12), 0.2, 0.08, nh=3), b, i, 0.10)
        # ---- pad on the second half of the battle theme
        if mode == "battle" and second_half:
            for m in TRIAD[ch]:
                song.add("pad", pad_tone(hz(m), song.bar_s + 0.3, 0.3, 0.4), b, 0, 0.05)
        # ---- drums
        if mode == "menu":
            for i in (0, 4):
                song.add("kick", kicks[0], b, i, 0.45)
            for i in (2, 6):
                song.add("snare", snares[(b + i) % 4], b, i, 0.28)
            for i in (1, 3, 5, 7):
                song.add("hat", hats[(b + i) % 4], b, i, 0.10)
            roll_bar = (b % 4 == 3)
            if roll_bar:                              # 16th snare roll, last two beats, crescendo
                for k in range(8):
                    song.add("snare", snares[k % 4], b, 4 + k * 0.5, 0.10 + 0.05 * k)
        else:
            for i in (0, 2, 4, 6):
                song.add("kick", kicks[0], b, i, 0.6)
            for i in (2, 6):
                song.add("snare", snares[(b + i) % 4], b, i, 0.42)
            for k in range(16):
                song.add("hat", hats[k % 4], b, k * 0.5, 0.14 if k % 2 == 0 else 0.08)
            if b % 4 == 3:
                for k in range(8):
                    song.add("snare", snares[k % 4], b, 4 + k * 0.5, 0.15 + 0.07 * k)
            if b == nb - 1:                           # extra full-bar build into the loop point
                for k in range(8):
                    song.add("snare", snares[k % 4], b, k * 0.5, 0.10 + 0.04 * k)


def mixdown(song, mode):
    pans = {"lead": -0.15, "lead2": 0.3, "bass": 0.0, "stab": 0.35, "arp": 0.5, "pad": 0.0,
            "kick": 0.0, "snare": -0.2, "hat": 0.4}
    levels = {"lead": 1.0, "lead2": 1.0, "bass": 0.9, "stab": 1.0, "arp": 1.0, "pad": 1.0,
              "kick": 1.0, "snare": 1.0, "hat": 1.0}
    L = np.zeros(song.N)
    R = np.zeros(song.N)
    send = np.zeros(song.N)
    sends = {"lead": 0.5, "lead2": 0.6, "arp": 0.6, "stab": 0.4, "snare": 0.25, "pad": 0.3}
    for name in song.tracks:
        x = song.done(name) * levels[name]
        th = (pans[name] + 1) * np.pi / 4
        L += x * np.cos(th)
        R += x * np.sin(th)
        if name in sends:
            send += x * sends[name]
    irL = make_ir(0.7, 5001, lp=6000)[:song.N]
    irR = make_ir(0.7, 5002, lp=6000)[:song.N]
    S = np.fft.rfft(send)
    wl = np.fft.irfft(S * np.fft.rfft(irL, song.N), song.N)
    wr = np.fft.irfft(S * np.fft.rfft(irR, song.N), song.N)
    L += 0.22 * wl
    R += 0.22 * wr
    st = np.stack([L, R], axis=1)
    st -= st.mean(axis=0)
    # target roughly -18 dBFS RMS (about -14 LUFS-ish), then gently limit stray peaks
    rms = np.sqrt(np.mean(st ** 2))
    st *= 10 ** (-18.0 / 20.0) / rms
    pk = np.max(np.abs(st))
    if pk > 0.89:
        st = np.tanh(st / 0.89) * 0.89
    return st


def encode(st, name):
    os.makedirs(MUSIC_DIR, exist_ok=True)
    with tempfile.TemporaryDirectory() as td:
        wav = os.path.join(td, name + ".wav")
        a = np.clip(st * 32767.0, -32768, 32767).astype("<i2")
        import wave
        with wave.open(wav, "wb") as w:
            w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
            w.writeframes(a.tobytes())
        out = os.path.join(MUSIC_DIR, name + ".ogg")
        subprocess.run([FFMPEG, "-y", "-v", "error", "-i", wav, ] + ENC_ARGS + ["-ar", "44100", "-ac", "2", out], check=True)
    return out


def main():
    # menu: 120 bpm, 16 bars = 32.0 s
    s = Song(120, 16)
    mel = MEL_A1 + MEL_A2 + MEL_B
    ch = CH_A + CH_B
    compose(s, mel, ch, "menu")
    st = mixdown(s, "menu")
    print("menu_theme", encode(st, "menu_theme"), "%.2fs" % (len(st) / SR))

    # battle: 140 bpm, 32 bars = ~54.9 s  (A, B, A', B' with extra layers in the second half)
    s = Song(140, 32)
    mel = (MEL_A1 + MEL_A2 + MEL_B) * 2
    ch = (CH_A + CH_B) * 2
    compose(s, mel, ch, "battle")
    st = mixdown(s, "battle")
    print("battle_theme", encode(st, "battle_theme"), "%.2fs" % (len(st) / SR))


if __name__ == "__main__":
    main()
