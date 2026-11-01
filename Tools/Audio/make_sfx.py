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


# Warm, wooden timbres: marimba-like partials (strong fundamental, a soft fourth harmonic) instead of
# metallic bell or coin overtones, so nothing rings sharply in the ear.
MARIMBA = ((1, 1.0), (3.9, 0.12), (9.2, 0.03))
SOFT = ((1, 1.0), (2, 0.18), (3, 0.05))


def wood_tok(freq=320, decay=0.03):
    body = tone(freq, 0.16, decay, ((1, 1.0), (2.0, 0.22)), attack=0.004, glide=0.25)
    knock = band_noise(0.16, 200, 1600) * env(0.16, 0.002, 0.01) * 0.35
    return body + knock


def felt_clack():
    """A die landing on a felt-covered board: a dull knock, no click."""
    sig = band_noise(0.08, 200, 1200) * env(0.08, 0.002, 0.012)
    sig += tone(rng.uniform(520, 760), 0.08, 0.018, SOFT, attack=0.002) * 0.45
    return sig


def mallet(freq, decay=0.22, attack=0.006):
    return tone(freq, decay * 4, decay, MARIMBA, attack=attack)


def thud(freq=80, decay=0.12):
    body = tone(freq, decay * 4, decay, ((1, 1.0), (1.5, 0.25)), attack=0.004, glide=0.8)
    return body + band_noise(decay * 4, 50, 500) * env(decay * 4, 0.002, decay * 0.4) * 0.4


def chord(freqs, start_step, length_s, decay, partials=MARIMBA):
    return [(i * start_step, tone(f, length_s, decay, partials, attack=0.008) * 0.6) for i, f in enumerate(freqs)]


def room(signal, amount=0.14, length_s=0.35):
    """A small, soft room: the sound gets a body instead of stopping dead."""
    n = int(RATE * length_s)
    impulse = one_pole(one_pole(rng.standard_normal(n), 1800), 1800) * np.exp(-np.arange(n) / (RATE * 0.09))
    impulse /= np.sqrt(np.sum(impulse ** 2)) or 1.0
    size = len(signal) + n
    wet = np.fft.irfft(np.fft.rfft(signal, size) * np.fft.rfft(impulse, size), size)[: size]
    out = np.zeros(size)
    out[: len(signal)] += signal
    return out + wet * amount


def recipes():
    yield "dice_shake", place(0.45, *[(s, felt_clack() * rng.uniform(0.3, 0.55)) for s in np.sort(rng.uniform(0, 0.38, 12))])
    land = [(0.0, felt_clack()), (0.09, felt_clack() * 0.75), (0.17, felt_clack() * 0.5), (0.27, felt_clack() * 0.32),
            (0.35, felt_clack() * 0.2), (0.43, felt_clack() * 0.12)]
    yield "dice_land", place(0.7, *land)
    yield "pawn_step", wood_tok(260, 0.025)
    yield "buy", place(0.9, (0.0, mallet(523)), (0.08, mallet(784, 0.26)))
    yield "money_in", place(0.8, (0.0, mallet(587, 0.16) * 0.8), (0.07, mallet(740, 0.18) * 0.7), (0.14, mallet(880, 0.22) * 0.6))
    yield "money_out", place(0.7, (0.0, mallet(494, 0.16) * 0.8), (0.09, mallet(370, 0.2) * 0.7))
    yield "build", place(0.7, (0.0, mix(thud(110, 0.06), wood_tok(380) * 0.6)), (0.18, mix(thud(95, 0.07), wood_tok(340) * 0.7)))
    yield "sell", place(0.5, (0.0, wood_tok(300) * 0.8), (0.1, mallet(440, 0.12) * 0.5))
    yield "property_sale", place(0.9, (0.0, wood_tok(260)), (0.1, mallet(440, 0.14) * 0.5), (0.2, mallet(349, 0.16) * 0.5), (0.3, mallet(262, 0.22) * 0.5))
    yield "bid", wood_tok(440) * 0.6
    yield "gavel", place(1.0, (0.0, mix(thud(90, 0.09), wood_tok(300))), (0.02, mallet(392, 0.25) * 0.3))
    yield "ui_click", wood_tok(420, 0.018) * 0.5
    yield "ui_open", band_noise(0.2, 120, 900) * np.hanning(int(RATE * 0.2)) * 0.3
    yield "your_turn", place(0.9, *chord([392, 587], 0.12, 0.7, 0.25))
    yield "achievement", place(1.3, *chord([392, 494, 587, 784], 0.09, 1.0, 0.32))
    yield "win", place(2.0, *chord([392, 494, 587, 784, 988], 0.12, 1.4, 0.42))
    yield "bankrupt", place(1.6, *chord([294, 247, 196, 147], 0.18, 1.0, 0.3, SOFT))
    yield "trade", place(0.8, (0.0, mallet(523, 0.15) * 0.6), (0.1, wood_tok(360) * 0.5), (0.18, mallet(659, 0.18) * 0.6))
    # Passing start: brighter and longer than an ordinary gain, a small fanfare for the lap bonus.
    yield "pass_start", place(1.1, (0.0, mallet(523, 0.18) * 0.7), (0.09, mallet(659, 0.2) * 0.7), (0.18, mallet(784, 0.22) * 0.7), (0.3, mallet(1047, 0.4) * 0.6))
    # A hotel going up: the building knocks and then a warm chord, bigger than a house.
    yield "hotel", place(1.2, (0.0, mix(thud(100, 0.07), wood_tok(360) * 0.6)), (0.16, mix(thud(90, 0.08), wood_tok(320) * 0.7)), *[(0.3 + s, sig) for s, sig in chord([392, 494, 587], 0.08, 0.8, 0.3)])
    # The turn passes to someone else at the table: one soft note, under the "your turn" chord.
    yield "turn_pass", mallet(440, 0.2) * 0.6


# Small, frequent sounds sit well under the big moments already in the files, not only in the game's mix.
QUIET = {"pawn_step": 0.35, "ui_click": 0.3, "ui_open": 0.3, "bid": 0.45, "dice_shake": 0.5, "dice_land": 0.55, "turn_pass": 0.4}
PEAK = 0.7


def write(name, signal):
    # Everything passes a gentle low-pass and a small room, so no sound has a sharp, thin top.
    signal = room(one_pole(one_pole(signal, 4000), 4000))
    peak = np.max(np.abs(signal)) or 1.0
    data = (signal / peak * QUIET.get(name, PEAK) * 32767).astype("<i2")
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
