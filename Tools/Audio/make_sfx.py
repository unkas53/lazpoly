"""
Synthesises Lazpoly's sound effects into Assets/_Lazpoly/Audio/Resources/Lazpoly/Sfx as 16-bit mono WAV.

Every sound is generated here from sine waves and filtered noise, so the files belong to the project and
need no licence. Run again after changing a recipe:  python Tools/Audio/make_sfx.py
Needs numpy only. A fixed seed keeps the output identical from run to run.
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Lazpoly", "Audio", "Resources", "Lazpoly", "Sfx")
rng = np.random.default_rng(1977)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(length_s, attack=0.002, decay=0.1):
    x = t(length_s)
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-x / decay)


def one_pole(signal, cutoff, high=False):
    """Simple one-pole low-pass (or high-pass) filter."""
    k = np.exp(-2 * np.pi * cutoff / RATE)
    out = np.empty_like(signal)
    y = 0.0
    for i, s in enumerate(signal):
        y = (1 - k) * s + k * y
        out[i] = y
    return signal - out if high else out


def band_noise(length_s, low, high):
    n = rng.standard_normal(int(RATE * length_s))
    return one_pole(one_pole(n, high), low, high=True)


def place(total_s, *parts):
    """Mixes (start_seconds, signal) parts into one buffer."""
    out = np.zeros(int(RATE * total_s))
    for start, sig in parts:
        i = int(start * RATE)
        j = min(len(out), i + len(sig))
        out[i:j] += sig[: j - i]
    return out


def mix(*signals):
    """Sums signals of different lengths, padding the shorter ones with silence."""
    out = np.zeros(max(len(s) for s in signals))
    for s in signals:
        out[: len(s)] += s
    return out


def tone(freq, length_s, decay, partials=((1, 1.0),), attack=0.003, glide=0.0):
    x = t(length_s)
    f = freq * (1 + glide * np.exp(-x / 0.03))
    phase = 2 * np.pi * np.cumsum(f) / RATE
    sig = sum(amp * np.sin(phase * ratio) for ratio, amp in partials)
    return sig * env(length_s, attack, decay)


BELL = ((1, 1.0), (2.76, 0.45), (5.40, 0.25), (8.93, 0.12))
COIN = ((1, 1.0), (2.32, 0.6), (4.25, 0.35), (6.8, 0.2))


def wood_tok(freq=520):
    body = tone(freq, 0.12, 0.025, ((1, 1.0), (2.1, 0.35)), glide=0.4)
    click = band_noise(0.12, 1500, 5000) * env(0.12, 0.0005, 0.006) * 0.6
    return body + click


def clack():
    sig = band_noise(0.06, 1800, 7000) * env(0.06, 0.0003, 0.008)
    sig += tone(rng.uniform(2300, 3200), 0.06, 0.012) * 0.5
    return sig


def coin(freq, decay=0.25):
    return tone(freq, decay * 4, decay, COIN, attack=0.001)


def thud(freq=90, decay=0.12):
    body = tone(freq, decay * 4, decay, ((1, 1.0), (1.5, 0.3)), glide=1.2)
    return body + band_noise(decay * 4, 60, 900) * env(decay * 4, 0.001, decay * 0.4) * 0.5


def chord(freqs, start_step, length_s, decay, partials=BELL):
    return [(i * start_step, tone(f, length_s, decay, partials) * 0.6) for i, f in enumerate(freqs)]


def recipes():
    yield "dice_shake", place(0.45, *[(s, clack() * rng.uniform(0.25, 0.5)) for s in np.sort(rng.uniform(0, 0.38, 14))])
    land = [(0.0, clack()), (0.09, clack() * 0.8), (0.16, clack() * 0.6), (0.26, clack() * 0.45),
            (0.33, clack() * 0.3), (0.42, clack() * 0.2), (0.47, clack() * 0.12)]
    yield "dice_land", place(0.7, *land)
    yield "pawn_step", wood_tok()
    yield "buy", place(0.9, (0.0, coin(1318)), (0.07, coin(1760)), (0.12, band_noise(0.25, 3000, 9000) * env(0.25, 0.001, 0.05) * 0.25))
    yield "money_in", place(0.8, (0.0, coin(1568, 0.18) * 0.7), (0.06, coin(2093, 0.2) * 0.6), (0.12, coin(2637, 0.22) * 0.5))
    yield "money_out", place(0.7, (0.0, coin(1046, 0.16) * 0.8), (0.08, coin(784, 0.18) * 0.7))
    yield "build", place(0.7, (0.0, mix(thud(140, 0.05), wood_tok(700) * 0.6)), (0.18, mix(thud(120, 0.06), wood_tok(640) * 0.7)))
    yield "sell", place(0.5, (0.0, wood_tok(420) * 0.8), (0.1, coin(988, 0.12) * 0.5))
    yield "mortgage", place(0.6, (0.0, wood_tok(360)), (0.12, tone(330, 0.4, 0.12, BELL) * 0.4))
    yield "bid", wood_tok(900) * 0.6
    yield "gavel", place(1.0, (0.0, mix(thud(110, 0.08), wood_tok(480))), (0.0, tone(587, 0.9, 0.25, BELL) * 0.25))
    yield "ui_click", wood_tok(1200) * 0.35
    yield "ui_open", band_noise(0.22, 400, 3000) * np.hanning(int(RATE * 0.22)) * 0.25
    yield "your_turn", place(0.9, *chord([784, 1175], 0.12, 0.7, 0.25))
    yield "achievement", place(1.3, *chord([523, 659, 784, 1046], 0.09, 1.0, 0.35))
    yield "win", place(2.0, *chord([523, 659, 784, 1046, 1318], 0.12, 1.4, 0.45))
    yield "bankrupt", place(1.6, *chord([392, 330, 262, 196], 0.18, 1.0, 0.3, ((1, 1.0), (2, 0.3), (3, 0.15))))
    yield "trade", place(0.8, (0.0, coin(1318, 0.15) * 0.6), (0.1, wood_tok(800) * 0.5), (0.18, coin(1760, 0.18) * 0.6))


def write(name, signal):
    peak = np.max(np.abs(signal)) or 1.0
    data = (signal / peak * 0.85 * 32767).astype("<i2")
    fade = min(len(data), int(RATE * 0.01))
    data[-fade:] = (data[-fade:] * np.linspace(1, 0, fade)).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(data.tobytes())


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, signal in recipes():
        write(name, signal)
        print(f"{name}.wav {len(signal) / RATE:.2f}s")
