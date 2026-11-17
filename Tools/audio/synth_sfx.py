#!/usr/bin/env python3
"""Generates all Tactile Wars SFX (44.1 kHz, 16 bit, mono WAV) procedurally.
Run:  python3 synth_sfx.py        Output: Assets/Audio/SFX/
Everything is deterministic (fixed RNG seeds), so re-running reproduces the same files."""
import os
import numpy as np
from synth_common import *


def buf(d):
    return np.zeros(ns(d))


def gurgle(d, seed, rate=60.0):
    """Wet amplitude modulation made from low passed noise."""
    m = fft_filter(white(d, seed), lp=rate)
    m = m / (np.std(m) + 1e-9)
    return np.clip(0.65 + 0.5 * m, 0.0, 1.3)


def bubbles(d, seed, count, fmin=450, fmax=1200, gain=0.35, tmin=0.01, tmax=None):
    r = rng(seed)
    out = buf(d)
    tmax = tmax if tmax else d - 0.06
    for _ in range(count):
        f = r.uniform(fmin, fmax)
        t0 = r.uniform(tmin, max(tmin + 0.01, tmax))
        b = sweep(f, f * r.uniform(1.6, 2.2), 0.04) * env_exp(0.04, 0.014, 0.002)
        mix_at(out, b, t0, gain * r.uniform(0.6, 1.0))
    return out


# ------------------------------------------------------------------ combat
def paint_shot(i):
    d = [0.090, 0.100, 0.085, 0.110][i]
    f0 = [720, 640, 820, 570][i]
    pop = sweep(f0, f0 * 0.42, d) * env_exp(d, 0.022, 0.0015)
    body = sweep(f0 * 0.5, f0 * 0.25, d) * env_exp(d, 0.03, 0.002) * 0.5
    nz = band(white(d, 100 + i), 1200, 4500) * env_exp(d, 0.012, 0.001) * 0.3
    x = sat(0.8 * pop + body + nz, 1.3)
    x = fft_filter(x, lp=5500)
    return trim(x, d)


def paint_splat(i):
    d = [0.15, 0.18, 0.20, 0.22][i]
    sp = band(white(d, 200 + i), 500, 3200 + 300 * i) * env_exp(d, 0.05, 0.002) * gurgle(d, 210 + i, 70)
    thump = sweep(210, 70, d) * env_exp(d, 0.04, 0.002) * 0.8
    bub = bubbles(d, 220 + i, 2 + i % 2, gain=0.35, tmin=0.02)
    x = sat(0.9 * sp + thump + bub, 1.2)
    x = fft_filter(x, lp=7000)
    return trim(x, d)


def unit_pop(i):
    d = [0.28, 0.30, 0.26][i]
    f = [520, 600, 460][i]
    work = buf(d + 0.1)
    puff = band(white(d, 300 + i), 150, 2200) * env_exp(d, 0.09, 0.006) * 0.45
    bwip = sweep(f, f * 0.4, 0.14) * env_exp(0.14, 0.05, 0.002) * 0.7
    work[:len(puff)] += puff
    work[:len(bwip)] += bwip
    # little sparkle
    for k, fs in enumerate([1500, 2000, 2600]):
        sp = sweep(fs, fs * 1.4, 0.05) * env_exp(0.05, 0.02, 0.002) * 0.18
        mix_at(work, sp, 0.02 + 0.03 * k)
    x = reverb(work, 0.18, 0.18, 310 + i, lp=4500)
    return trim(fft_filter(x, lp=6500), d)


def turret_shot(i):
    d = [0.24, 0.26][i]
    f0 = [260, 215][i]
    th = sweep(f0, 55, d) * env_exp(d, 0.07, 0.002)
    pop = sweep(900, 300, 0.05) * env_exp(0.05, 0.02, 0.001)
    nz = band(white(d, 400 + i), 300, 3500) * env_exp(d, 0.03, 0.001) * 0.5
    x = np.zeros(ns(d))
    x += th
    x[:len(pop)] += 0.5 * pop
    x += nz
    return trim(fft_filter(sat(x, 2.0), lp=6000), d)


def cannon_shot():
    d = 0.5
    th = sweep(150, 38, d) * env_exp(d, 0.14, 0.002)
    nz = band(white(d + 0.2, 410), 80, 2500)[:ns(d)] * env_exp(d, 0.10, 0.001) * 0.8
    rum = fft_filter(white(d, 411), lp=300) * env_exp(d, 0.25, 0.005) * 3.0
    pop = sweep(700, 200, 0.06) * env_exp(0.06, 0.02, 0.001) * 0.4
    x = th + nz + rum
    x[:len(pop)] += pop
    w = np.concatenate([sat(x, 2.2), np.zeros(ns(0.2))])
    w = reverb(w, 0.25, 0.3, 412, lp=3500)
    return trim(w, d)


def tank_shot():
    d = 0.55
    th = sweep(120, 32, d) * env_exp(d, 0.16, 0.002)
    th2 = np.zeros(ns(d))
    s2 = sweep(95, 35, 0.3) * env_exp(0.3, 0.1, 0.002) * 0.6
    mix_at(th2, s2, 0.11)
    t = tarr(d)
    tonk = (np.sin(TWO_PI * 210 * t) + 0.6 * np.sin(TWO_PI * 342 * t) + 0.3 * np.sin(TWO_PI * 530 * t)) * np.exp(-t / 0.07) * 0.28
    nz = band(white(d, 420), 80, 2800) * env_exp(d, 0.09, 0.001) * 0.8
    rum = fft_filter(white(d, 421), lp=250) * env_exp(d, 0.3, 0.005) * 3.0
    x = th + th2 + tonk + nz + rum
    w = np.concatenate([sat(x, 2.4), np.zeros(ns(0.2))])
    w = reverb(w, 0.3, 0.35, 422, lp=3000)
    return trim(w, d)


def mine_explosion():
    d = 0.70
    th = sweep(140, 35, d) * env_exp(d, 0.2, 0.002)
    blast = band(white(d, 430), 100, 3000) * env_exp(d, 0.12, 0.001) * 0.9
    rum = fft_filter(white(d, 431), lp=400) * env_exp(d, 0.3, 0.004) * 3.0
    wet = band(white(d, 432), 500, 3500) * env_exp(d, 0.18, 0.02) * gurgle(d, 433, 50) * 0.6
    bub = bubbles(d, 434, 7, 400, 1100, 0.3, tmin=0.08, tmax=0.5)
    pop = sweep(900, 250, 0.08) * env_exp(0.08, 0.025, 0.001) * 0.4
    x = th + blast + rum + wet + bub
    x[:len(pop)] += pop
    w = np.concatenate([sat(x, 2.0), np.zeros(ns(0.3))])
    w = reverb(w, 0.25, 0.35, 435, lp=4000)
    return trim(w, d)


def building_hit(i):
    d = 0.22
    f0 = [260, 215][i]
    th = sweep(f0, 105, d) * env_exp(d, 0.05, 0.001)
    bonk = np.sin(TWO_PI * [520, 450][i] * tarr(d)) * env_exp(d, 0.03, 0.001) * 0.35
    nz = band(white(d, 500 + i), 200, 2500) * env_exp(d, 0.04, 0.001) * 0.6
    sp = band(white(d, 510 + i), 1500, 6000) * env_exp(d, 0.06, 0.002) * gurgle(d, 520 + i, 80) * 0.25
    return trim(fft_filter(sat(th + bonk + nz + sp, 1.5), lp=6500), d)


def building_destroyed():
    d = 1.0
    r = rng(540)
    th = sweep(130, 33, d) * env_exp(d, 0.3, 0.002)
    blast = band(white(d, 541), 100, 2800) * env_exp(d, 0.2, 0.001) * 0.7
    crumb = buf(d)
    for k in range(26):
        t0 = 0.04 + k * 0.032 + r.uniform(-0.01, 0.01)
        f = r.uniform(300, 1200)
        c = band(white(0.05, 600 + k), f, f * 3.5) * env_exp(0.05, 0.015, 0.001)
        mix_at(crumb, c, t0, 0.9 * (1.0 - k / 34.0))
    wet = band(white(d, 542), 500, 3500) * env_exp(d, 0.3, 0.02) * gurgle(d, 543, 40) * 0.45
    bub = bubbles(d, 544, 8, 400, 1000, 0.25, tmin=0.1, tmax=0.7)
    hits = buf(d)
    for t0, f in [(0.0, 240), (0.12, 200), (0.26, 170)]:
        mix_at(hits, sweep(f, f * 0.45, 0.14) * env_exp(0.14, 0.04, 0.001), t0, 0.5)
    x = th + blast + 0.9 * crumb + wet + bub + hits
    w = np.concatenate([sat(x, 1.8), np.zeros(ns(0.3))])
    w = reverb(w, 0.25, 0.4, 545, lp=3500)
    return trim(w, d)


# ------------------------------------------------------------------ movement loops
def footsteps_loop():
    n = SR
    work = np.zeros(n + SR // 2)
    r = rng(700)
    for layer in range(3):
        base = r.uniform(0, 0.125)
        for s in range(8):
            t0 = base + s * 0.125 + r.uniform(-0.012, 0.012)
            lo = r.uniform(200, 450)
            st = band(white(0.07, 710 + layer * 20 + s), lo, lo * 5) * env_exp(0.07, 0.016, 0.002)
            th = sweep(r.uniform(110, 150), 70, 0.05) * env_exp(0.05, 0.015, 0.002) * 0.5
            st[:len(th)] += th
            mix_at(work, st, t0 % 1.0, r.uniform(0.4, 1.0))
    x = fold_wrap(work, n)
    x = fft_filter(x, lp=4000)
    return x


def stroke_draw_loop():
    n = SR
    t = np.arange(n) / SR
    nz = band(white(1.0, 800), 2500, 9000) + 0.5 * band(white(1.0, 801), 900, 2200)
    wob = fft_filter(white(1.0, 802), lp=40)
    wob = wob / (np.std(wob) + 1e-9)
    mod = 0.55 + 0.22 * np.cos(TWO_PI * 5 * t) + 0.13 * np.cos(TWO_PI * 11 * t + 1.0) + 0.12 * wob
    mod = np.clip(mod, 0.05, 1.5)
    return nz * mod


def formation_set():
    d = 0.38
    sw = buf(d)
    for c, w, lo, hi in [(0.06, 0.04, 400, 1500), (0.10, 0.04, 1000, 3500), (0.14, 0.04, 2500, 8000)]:
        sw += band(white(d, 900 + lo), lo, hi) * env_gauss(d, c, w)
    sw *= 0.8
    pop = sweep(500, 950, 0.12) * env_exp(0.12, 0.04, 0.002) * 0.8
    out = sw.copy()
    mix_at(out, pop, 0.15)
    mix_at(out, bell(1318.5, 0.25, 0.1), 0.17, 0.35)
    out = reverb(out, 0.2, 0.2, 910, lp=5000)
    return trim(out, d)


# ------------------------------------------------------------------ objectives
def capture_tick():
    d = 0.09
    t = tarr(d)
    x = (np.sin(TWO_PI * 1100 * t) + 0.4 * np.sin(TWO_PI * 2640 * t)) * env_exp(d, 0.02, 0.001)
    return x


def capture_complete():
    d = 1.1
    out = buf(d + 0.4)
    for k, m in enumerate([72, 76, 79, 84]):
        mix_at(out, bell(hz(m), 0.6, 0.22), k * 0.09, 0.7)
    for m in (84, 88, 91):
        mix_at(out, bell(hz(m), 0.9, 0.4), 0.40, 0.45)
    out = reverb(out, 0.25, 0.5, 1000, lp=6000)
    return trim(out, d)


def reinforcement_ready():
    d = 0.5
    out = buf(d + 0.3)
    mix_at(out, bell(hz(79), 0.4, 0.15), 0.0, 0.7)
    mix_at(out, bell(hz(84), 0.5, 0.2), 0.12, 0.8)
    out = reverb(out, 0.2, 0.3, 1010)
    return trim(out, d)


def reinforcement_call():
    d = 0.8
    out = buf(d + 0.4)
    for t0, m, ln in [(0.0, 67, 0.10), (0.11, 72, 0.10), (0.22, 76, 0.10)]:
        mix_at(out, brass(hz(m), ln + 0.02, lp=4500), t0, 0.7)
    mix_at(out, brass(hz(79), 0.50, lp=4500), 0.34, 0.85)
    out = reverb(out, 0.2, 0.35, 1020, lp=4500)
    return trim(out, d)


def bubble_pop():
    d = 0.12
    b = sweep(320, 1000, 0.07) * env_exp(0.07, 0.03, 0.002)
    w = buf(d)
    w[:len(b)] += b
    mix_at(w, band(white(0.02, 1030), 2000, 7000) * env_exp(0.02, 0.005, 0.0005), 0.065, 0.3)
    return w


def coin_pickup():
    d = 0.4
    out = buf(d)
    mix_at(out, bell(987.8, 0.2, 0.07), 0.0, 0.7)
    mix_at(out, bell(1318.5, 0.35, 0.14), 0.07, 0.9)
    return out


# ------------------------------------------------------------------ UI
def ui_click():
    d = 0.07
    x = sweep(1400, 900, d) * env_exp(d, 0.012, 0.0008)
    x += band(white(d, 1100), 3000, 9000) * env_exp(d, 0.004, 0.0005) * 0.2
    return x


def ui_back():
    d = 0.09
    return sweep(900, 500, d) * env_exp(d, 0.02, 0.001)


def ui_open():
    d = 0.3
    sw = band(white(d, 1110), 500, 4000) * env_gauss(d, 0.10, 0.06) * 0.4
    r = sweep(400, 800, 0.15) * env_exp(0.15, 0.05, 0.003) * 0.6
    sw[:len(r)] += r
    mix_at(sw, bell(1046.5, 0.2, 0.08), 0.13, 0.3)
    return sw


def ui_purchase():
    d = 0.55
    out = buf(d + 0.2)
    mix_at(out, band(white(0.03, 1120), 6000, 12000) * env_exp(0.03, 0.01, 0.0005), 0.0, 0.3)
    mix_at(out, bell(1318.5, 0.3, 0.12), 0.0, 0.7)
    mix_at(out, bell(1760.0, 0.45, 0.2), 0.09, 0.8)
    out = reverb(out, 0.15, 0.25, 1121)
    return trim(out, d)


def ui_error():
    d = 0.3
    out = buf(d)
    for t0, f in [(0.0, 220), (0.14, 165)]:
        ln = 0.14
        tone = additive(f, [1, 0, 0.3, 0, 0.12], env_gate(ln, 0.005, 0.04))
        mix_at(out, tone, t0, 0.8)
    return fft_filter(sat(out * 0.5, 1.5), lp=2200)


def level_up():
    d = 1.2
    out = buf(d + 0.4)
    for k, m in enumerate([72, 76, 79, 84, 88]):
        mix_at(out, bell(hz(m), 0.5, 0.2), k * 0.1, 0.65)
    for m in (79, 84, 88):
        mix_at(out, bell(hz(m), 0.8, 0.4), 0.52, 0.45)
    r = rng(1130)
    for k in range(10):
        f = r.uniform(3000, 6000)
        mix_at(out, sweep(f, f * 1.2, 0.04) * env_exp(0.04, 0.012, 0.001), 0.3 + k * 0.05, 0.1)
    out = reverb(out, 0.25, 0.5, 1131, lp=6000)
    return trim(out, d)


# ------------------------------------------------------------------ stingers
def victory():
    d = 2.9
    out = buf(d + 0.8)
    mel = [(0.00, "G4", 0.14), (0.18, "G4", 0.14), (0.36, "G4", 0.14), (0.54, "C5", 0.50),
           (1.10, "E5", 0.14), (1.28, "D5", 0.14), (1.46, "E5", 0.14), (1.70, "G5", 0.85)]
    for t0, nm, ln in mel:
        mix_at(out, brass(hz(midi(nm)), ln + 0.04, lp=5000), t0, 0.8)
    # harmony brass
    for nm in ("C4", "E4", "G4"):
        mix_at(out, brass(hz(midi(nm)), 0.55, lp=3500), 0.54, 0.35)
    for nm in ("C5", "E5", "C4"):
        mix_at(out, brass(hz(midi(nm)), 0.9, lp=3500), 1.70, 0.35)
    # bass plucks
    for t0, nm in [(0.0, "C3"), (0.54, "C3"), (1.10, "G2"), (1.70, "C3")]:
        mix_at(out, pluck(hz(midi(nm)), 0.5, 0.25), t0, 0.7)
    # small snare roll into the final chord
    for k in range(8):
        mix_at(out, snare(0.3 + 0.05 * k, 1200 + k), 1.30 + k * 0.045)
    # crash and sparkle on the final chord
    crash = band(white(1.0, 1210), 4000, 12000) * env_exp(1.0, 0.35, 0.002) * 0.3
    mix_at(out, crash, 1.70)
    for k, nm in enumerate(["C6", "E6", "G6", "C7", "G6", "E6"]):
        mix_at(out, bell(hz(midi(nm)), 0.7, 0.3), 1.72 + k * 0.07, 0.35)
    out = reverb(out, 0.25, 0.6, 1211, lp=5500)
    out = trim(out, d)
    return out


def defeat():
    d = 2.0
    out = buf(d + 0.6)
    notes = [(0.00, "Bb4", 0.34), (0.38, "A4", 0.34), (0.76, "Ab4", 0.34)]
    for t0, nm, ln in notes:
        mix_at(out, brass(hz(midi(nm)), ln + 0.05, vib=0.002, lp=1800), t0, 0.7)
    # last note held, then slides down
    ln = 0.95
    t = np.arange(ns(ln)) / SR
    glide = np.where(t < 0.5, 1.0, 2.0 ** (-(t - 0.5) / 0.45 * 0.75))
    mix_at(out, brass(hz(midi("G4")), ln, f_env=glide, vib=0.004, lp=1800), 1.14, 0.75)
    for t0, nm in [(0.0, "G3"), (0.76, "Eb3"), (1.14, "G2")]:
        mix_at(out, pluck(hz(midi(nm)), 0.8, 0.4, nh=4), t0, 0.35)
    out = reverb(out, 0.3, 0.8, 1300, lp=3500)
    return trim(out, d)


# ------------------------------------------------------------------ phase 5 defenses
def tesla_charge():
    d = 0.55
    t = tarr(d)
    hum = sweep(180, 720, d) * (0.4 + 0.6 * t / d)
    buzz = np.sign(np.sin(TWO_PI * sweep_phase_safe(90, 360, d))) * 0.18
    crackle = band(white(d, 700), 2500, 7000) * env_gauss(d, d * 0.8, 0.15) * 0.35
    x = fft_filter(0.5 * hum + buzz + crackle, lp=6000) * env_gate(d, 0.05, 0.04)
    return trim(sat(x, 1.4), d)


def sweep_phase_safe(f0, f1, d):
    t = tarr(d)
    return f0 * t + (f1 - f0) * t * t / (2 * d)


def tesla_zap():
    d = 0.32
    r = rng(710)
    out = buf(d)
    for k in range(9):
        t0 = k * 0.026 + r.uniform(0, 0.01)
        c = band(white(0.03, 720 + k), 1800, 9000) * env_exp(0.03, 0.008, 0.0005)
        mix_at(out, c, t0, 0.9)
    snap = sweep(2400, 300, 0.08) * env_exp(0.08, 0.02, 0.0005) * 0.6
    hum = np.sin(TWO_PI * 120 * tarr(d)) * env_exp(d, 0.12, 0.002) * 0.3
    x = out + hum
    x[:len(snap)] += snap
    return trim(fft_filter(sat(x, 1.8), lp=10000), d)


def sniper_aim():
    d = 0.4
    beep = sine(1650, d) * env_gate(d, 0.01, 0.05) * (0.5 + 0.5 * (np.sin(TWO_PI * 14 * tarr(d)) > 0))
    return trim(beep * 0.6, d)


def sniper_shot():
    d = 0.55
    crack = band(white(d, 740), 800, 7000) * env_exp(d, 0.03, 0.0005)
    boom = sweep(320, 60, d) * env_exp(d, 0.12, 0.001) * 0.8
    tail = band(white(d, 741), 300, 2500) * env_exp(d, 0.2, 0.02) * 0.25
    x = sat(crack + boom + tail, 1.7)
    w = np.concatenate([x, np.zeros(ns(0.2))])
    w = reverb(w, 0.25, 0.45, 742, lp=4500)
    return trim(w, d)


def geyser_rumble():
    d = 0.9
    rum = fft_filter(white(d, 750), lp=220) * 3.0 * env_gate(d, 0.3, 0.1)
    bub = bubbles(d, 751, 14, 250, 700, 0.35, tmin=0.05, tmax=0.8)
    x = rum + bub * gurgle(d, 752, 40)
    return trim(sat(x, 1.4), d)


def geyser_erupt():
    d = 1.0
    whoosh = band(white(d, 760), 300, 4000) * env_exp(d, 0.35, 0.03) * gurgle(d, 761, 60)
    thump = sweep(150, 40, d) * env_exp(d, 0.18, 0.002)
    bub = bubbles(d, 762, 12, 350, 1100, 0.3, tmin=0.1, tmax=0.8)
    x = sat(whoosh + thump + bub, 1.6)
    w = np.concatenate([x, np.zeros(ns(0.3))])
    w = reverb(w, 0.25, 0.4, 763, lp=4000)
    return trim(w, d)


# ------------------------------------------------------------------ table
def build():
    items = {}
    for i in range(4):
        items["paint_shot_%02d" % (i + 1)] = paint_shot(i)
        items["paint_splat_%02d" % (i + 1)] = paint_splat(i)
    for i in range(3):
        items["unit_pop_%02d" % (i + 1)] = unit_pop(i)
    for i in range(2):
        items["turret_shot_%02d" % (i + 1)] = turret_shot(i)
        items["building_hit_%02d" % (i + 1)] = building_hit(i)
    items["cannon_shot"] = cannon_shot()
    items["tank_shot"] = tank_shot()
    items["mine_explosion"] = mine_explosion()
    items["building_destroyed"] = building_destroyed()
    items["formation_set"] = formation_set()
    items["capture_tick"] = capture_tick()
    items["capture_complete"] = capture_complete()
    items["reinforcement_ready"] = reinforcement_ready()
    items["reinforcement_call"] = reinforcement_call()
    items["bubble_pop"] = bubble_pop()
    items["coin_pickup"] = coin_pickup()
    for name, fn in [("ui_click", ui_click), ("ui_back", ui_back), ("ui_open", ui_open),
                     ("ui_purchase", ui_purchase), ("ui_error", ui_error), ("level_up", level_up),
                     ("victory", victory), ("defeat", defeat)]:
        items[name] = fn()
    for name, fn in [("tesla_charge", tesla_charge), ("tesla_zap", tesla_zap), ("sniper_aim", sniper_aim),
                     ("sniper_shot", sniper_shot), ("geyser_rumble", geyser_rumble), ("geyser_erupt", geyser_erupt)]:
        items[name] = fn()
    return items


LOOPS = {"footsteps_loop": footsteps_loop, "stroke_draw_loop": stroke_draw_loop}
# longer tails need a longer final fade so reverb/decay ends smoothly
FADE_OUT = {"victory": 0.15, "defeat": 0.15, "capture_complete": 0.05, "level_up": 0.05,
            "cannon_shot": 0.05, "tank_shot": 0.05, "mine_explosion": 0.06, "building_destroyed": 0.08,
            "sniper_shot": 0.06, "geyser_erupt": 0.08, "geyser_rumble": 0.08}


def main():
    os.makedirs(SFX_DIR, exist_ok=True)
    for name, x in build().items():
        y = finalize(x, -3.0, 0.002, FADE_OUT.get(name, 0.012))
        write_wav(os.path.join(SFX_DIR, name + ".wav"), y)
        print("wrote", name, "%.3fs" % (len(y) / SR))
    for name, fn in LOOPS.items():
        y = finalize(fn(), -3.0, loop=True)
        write_wav(os.path.join(SFX_DIR, name + ".wav"), y)
        print("wrote", name, "%.3fs (loop)" % (len(y) / SR))


if __name__ == "__main__":
    main()
