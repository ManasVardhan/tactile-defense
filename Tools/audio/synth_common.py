"""Shared procedural synthesis helpers for the Tactile Wars audio (numpy only)."""
import os
import wave
import numpy as np

SR = 44100
TWO_PI = 2.0 * np.pi

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SFX_DIR = os.path.join(ROOT, "Assets", "Audio", "SFX")
MUSIC_DIR = os.path.join(ROOT, "Assets", "Audio", "Music")


def ns(d):
    return int(round(d * SR))


def tarr(d):
    return np.arange(ns(d)) / SR


def rng(seed):
    return np.random.default_rng(seed)


def white(d, seed):
    return rng(seed).standard_normal(ns(d))


def hz(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


_NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def midi(name):
    """'E5' -> 76, 'Bb4' -> 70, 'F#3' -> 54."""
    p = _NOTE[name[0]]
    i = 1
    while name[i] in "#b":
        p += 1 if name[i] == "#" else -1
        i += 1
    return 12 * (int(name[i:]) + 1) + p


# ---------------------------------------------------------------- filters
def fft_filter(x, hp=None, lp=None, order=2):
    """Zero phase smooth high/low pass done in the frequency domain.
    Circular, so filtering a periodic buffer keeps it periodic (seamless loops)."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    g = np.ones_like(f)
    if hp:
        g = g / np.sqrt(1.0 + (hp / np.maximum(f, 1e-3)) ** (2 * order))
    if lp:
        g = g / np.sqrt(1.0 + (f / lp) ** (2 * order))
    return np.fft.irfft(X * g, len(x))


def band(x, lo, hi, order=2):
    return fft_filter(x, hp=lo, lp=hi, order=order)


def sat(x, drive=1.5):
    return np.tanh(x * drive) / np.tanh(drive)


# ---------------------------------------------------------------- envelopes
def env_exp(d, decay, attack=0.002):
    t = tarr(d)
    e = np.exp(-t / decay)
    if attack > 0:
        e = e * np.minimum(1.0, t / attack)
    return e


def env_gate(d, att=0.01, rel=0.05):
    t = tarr(d)
    return np.clip(np.minimum(t / max(att, 1e-4), (d - t) / max(rel, 1e-4)), 0.0, 1.0)


def env_gauss(d, center, width):
    t = tarr(d)
    return np.exp(-(((t - center) / width) ** 2))


# ---------------------------------------------------------------- oscillators
def sine(f, d, phase=0.0):
    return np.sin(TWO_PI * f * tarr(d) + phase)


def sweep(f0, f1, d):
    """Exponential pitch sweep sine from f0 to f1 over d seconds."""
    n = ns(d)
    t = np.arange(n) / SR
    f = f0 * (f1 / f0) ** (t / d)
    return np.sin(TWO_PI * np.cumsum(f) / SR)


def additive(freq, amps, env):
    """freq: scalar or per-sample array; amps: list of harmonic amplitudes."""
    n = len(env)
    f = np.full(n, float(freq)) if np.isscalar(freq) else np.asarray(freq, float)
    ph = TWO_PI * np.cumsum(f) / SR
    out = np.zeros(n)
    fmax = f.max()
    for k, a in enumerate(amps, start=1):
        if a == 0 or k * fmax > 18000:
            continue
        out += a * np.sin(k * ph)
    return out * env


# ---------------------------------------------------------------- instruments
def bell(f, d=0.6, decay=0.35):
    t = tarr(d)
    s = (np.sin(TWO_PI * f * t) * np.exp(-t / decay)
         + 0.45 * np.sin(TWO_PI * 2.0 * f * t) * np.exp(-t / (decay * 0.6))
         + 0.22 * np.sin(TWO_PI * 3.005 * f * t) * np.exp(-t / (decay * 0.4))
         + 0.10 * np.sin(TWO_PI * 4.2 * f * t) * np.exp(-t / (decay * 0.25)))
    return s * np.minimum(1.0, t / 0.0015)


def marimba(f, d=0.5, decay=0.22):
    t = tarr(d)
    s = (np.sin(TWO_PI * f * t) * np.exp(-t / decay)
         + 0.35 * np.sin(TWO_PI * 4.0 * f * t) * np.exp(-t / (decay * 0.3))
         + 0.08 * np.sin(TWO_PI * 9.9 * f * t) * np.exp(-t / (decay * 0.1)))
    # tiny mallet knock
    knock = fft_filter(white(0.012, int(f) % 997 + 3), hp=800, lp=3500)
    s[:len(knock)] += 0.12 * knock * np.exp(-np.arange(len(knock)) / SR / 0.003)
    return s * np.minimum(1.0, t / 0.001)


def pluck(f, d=0.4, decay=0.18, nh=6, bright=1.0):
    t = tarr(d)
    s = np.zeros(len(t))
    for k in range(1, nh + 1):
        if k * f > 16000:
            break
        s += (1.0 / k) * np.sin(TWO_PI * k * f * t) * np.exp(-t / (decay / (1.0 + 0.7 * (k - 1) * bright)))
    return s * np.minimum(1.0, t / 0.002) * np.minimum(1.0, np.maximum((d - t) / 0.02, 0))


def brass(f, d=0.4, f_env=None, vib=0.004, lp=None):
    n = ns(d)
    t = np.arange(n) / SR
    fr = np.full(n, float(f)) if f_env is None else f * f_env
    vdepth = vib * np.clip((t - 0.08) / 0.15, 0, 1)
    fr = fr * (1.0 + vdepth * np.sin(TWO_PI * 5.3 * t))
    ph = TWO_PI * np.cumsum(fr) / SR
    out = np.zeros(n)
    for k in range(1, 9):
        if k * fr.max() > 16000:
            break
        come_in = np.clip(t / (0.015 + 0.012 * k), 0, 1)   # high harmonics bloom later: "blat"
        out += (1.0 / k ** 0.9) * come_in * np.sin(k * ph)
    env = env_gate(d, 0.018, 0.05)
    out = out * env
    if lp:
        out = fft_filter(out, lp=lp)
    return out


def pad_tone(f, d, att=0.4, rel=0.5):
    t = tarr(d)
    s = np.zeros(len(t))
    for det in (-0.004, 0.0, 0.004):
        ph = TWO_PI * f * (1 + det) * t
        s += np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.1 * np.sin(3 * ph)
    return s * env_gate(d, att, rel) / 3.0


# ---------------------------------------------------------------- drums
def kick(gain=1.0):
    d = 0.22
    s = sweep(120, 45, d) * env_exp(d, 0.08, 0.001)
    click = fft_filter(white(0.01, 11), hp=1500, lp=6000) * env_exp(0.01, 0.003, 0.0005)
    s[:len(click)] += 0.25 * click
    return gain * s


def snare(gain=1.0, seed=21, decay=0.07):
    d = 0.2
    nz = fft_filter(white(d, seed), hp=1800, lp=9000) * env_exp(d, decay, 0.001)
    body = sweep(210, 150, d) * env_exp(d, 0.04, 0.001)
    return gain * (0.55 * nz + 0.4 * body)


def hat(gain=1.0, seed=31):
    d = 0.06
    return gain * fft_filter(white(d, seed), hp=7000) * env_exp(d, 0.018, 0.0008) * 0.7


# ---------------------------------------------------------------- space
def make_ir(rt, seed, lp=5000, hp=250):
    n = ns(rt * 1.4)
    t = np.arange(n) / SR
    ir = rng(seed).standard_normal(n) * np.exp(-6.9 * t / rt)
    ir *= np.minimum(1.0, t / 0.004)
    ir = fft_filter(ir, hp=hp, lp=lp)
    return ir / np.sqrt(np.sum(ir ** 2))


def reverb(x, wet=0.2, rt=0.3, seed=7, lp=5000, circular=False):
    """Convolve with decaying noise impulse; output keeps len(x)."""
    n = len(x)
    ir = make_ir(rt, seed, lp)
    if circular:
        ir = ir[:n]
        y = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(ir, n), n)
    else:
        m = n + len(ir)
        y = np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(ir, m), m)[:n]
    return x + wet * y


def mix_at(buf, snd, t, gain=1.0):
    """Add snd into buf starting at time t (seconds); clipped to buffer."""
    i = int(round(t * SR))
    if i >= len(buf):
        return
    j = min(len(buf), i + len(snd))
    buf[i:j] += gain * snd[: j - i]


def fold_wrap(buf, n):
    """Fold the tail beyond n back onto the start (seamless loops)."""
    out = buf[:n].copy()
    rest = buf[n:]
    k = 0
    while k < len(rest):
        seg = rest[k:k + n]
        out[:len(seg)] += seg
        k += n
    return out


# ---------------------------------------------------------------- output
def fade(x, fin=0.003, fout=0.012):
    x = x.copy()
    a, b = ns(fin), ns(fout)
    if a:
        x[:a] *= np.linspace(0, 1, a) ** 1.5
    if b:
        x[-b:] *= np.linspace(1, 0, b) ** 1.5
    return x


def finalize(x, peak_db=-3.0, fin=0.003, fout=0.012, loop=False):
    x = np.asarray(x, float)
    x = x - np.mean(x)
    if not loop:
        x = fade(x, fin, fout)
    p = np.max(np.abs(x))
    return x / p * 10 ** (peak_db / 20.0)


def trim(x, d):
    n = ns(d)
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def write_wav(path, x, stereo=False):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    a = np.clip(np.asarray(x) * 32767.0, -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2 if stereo else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(a.tobytes())
