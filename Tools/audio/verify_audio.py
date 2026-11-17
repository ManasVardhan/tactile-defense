#!/usr/bin/env python3
"""Verify generated audio with ffprobe + ffmpeg volumedetect. Prints a table and flags problems."""
import glob
import json
import os
import re
import subprocess
import sys
from synth_common import SFX_DIR, MUSIC_DIR

FF = "/opt/homebrew/bin/"


def probe(path):
    o = subprocess.run([FF + "ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries",
                        "stream=codec_name,sample_rate,channels:format=duration,size", "-of", "json", path],
                       capture_output=True, text=True).stdout
    j = json.loads(o)
    s = j["streams"][0]
    return s["codec_name"], int(s["sample_rate"]), int(s["channels"]), float(j["format"]["duration"]), int(j["format"]["size"])


def vol(path):
    e = subprocess.run([FF + "ffmpeg", "-hide_banner", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
                       capture_output=True, text=True).stderr
    mx = float(re.search(r"max_volume: (-?[\d.]+) dB", e).group(1))
    mean = float(re.search(r"mean_volume: (-?[\d.]+) dB", e).group(1))
    return mx, mean


def main():
    bad = 0
    total = 0
    print("%-28s %-6s %6s %2s %8s %8s %8s %8s" % ("file", "codec", "sr", "ch", "dur(s)", "peak dB", "mean dB", "KB"))
    for p in sorted(glob.glob(SFX_DIR + "/*.wav")) + sorted(glob.glob(MUSIC_DIR + "/*.ogg")):
        c, sr, ch, d, sz = probe(p)
        mx, mean = vol(p)
        total += sz
        flag = ""
        if mx > -1.0:
            flag += " CLIP?"
        if mx < -12.0:
            flag += " QUIET"
        if sr != 44100:
            flag += " SR"
        if p.endswith(".wav") and ch != 1:
            flag += " CH"
        if p.endswith(".ogg") and ch != 2:
            flag += " CH"
        if flag:
            bad += 1
        print("%-28s %-6s %6d %2d %8.3f %8.1f %8.1f %8.1f%s" % (os.path.basename(p), c, sr, ch, d, mx, mean, sz / 1024, flag))
    print("total size: %.2f MB" % (total / 1048576.0))
    print("flagged:", bad)
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
