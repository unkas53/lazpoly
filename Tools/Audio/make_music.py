"""Lazpoly'nin sakin arka plan müziklerini üretir.

İki parça, her biri yaklaşık 64 saniye, kendi içinde döner: "kiyi" (pentatonik marimba arpejleri ve
yumuşak bir pad) ve "aksam" (daha yavaş, daha koyu akorlar). Dışarıdan ses kullanılmaz; numpy ile
sentezlenir. 22,05 kHz mono, böylece depo küçük kalır.

Kullanım:  python Tools/Audio/make_music.py
Çıktı:     Assets/_Lazpoly/Audio/Resources/Lazpoly/Music/*.wav
"""
import os
import wave

import numpy as np

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Lazpoly", "Audio", "Resources", "Lazpoly", "Music")
RATE = 22050
rng = np.random.default_rng(4471)


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def one_pole(signal, cutoff):
    a = np.exp(-2 * np.pi * cutoff / RATE)
    out = np.empty_like(signal)
    y = 0.0
    for i, x in enumerate(signal):
        y = (1 - a) * x + a * y
        out[i] = y
    return out


def lowpass_fft(signal, cutoff):
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(len(signal), 1 / RATE)
    spectrum *= 1 / (1 + (freqs / cutoff) ** 4)
    return np.fft.irfft(spectrum, len(signal))


def marimba(freq, length_s, decay):
    """A soft mallet note: fundamental plus faint high partials, fast attack, exponential decay."""
    time = t(length_s)
    env = (1 - np.exp(-time / 0.004)) * np.exp(-time / decay)
    sig = np.sin(2 * np.pi * freq * time) + 0.1 * np.sin(2 * np.pi * freq * 3.98 * time) * np.exp(-time / (decay * 0.3))
    return sig * env


def pad(freqs, length_s, attack=2.5, release=3.0):
    """A slow, detuned pad: several sines per note, breathing in and out."""
    time = t(length_s)
    sig = np.zeros_like(time)
    for f in freqs:
        for detune in (-0.25, 0.0, 0.25):
            phase = rng.uniform(0, 2 * np.pi)
            sig += np.sin(2 * np.pi * f * (1 + detune / 100) * time + phase) / 3
    env = np.minimum(1, time / attack) * np.minimum(1, (length_s - time) / release)
    tremolo = 1 + 0.06 * np.sin(2 * np.pi * 0.17 * time)
    return lowpass_fft(sig * env * tremolo, 900) / len(freqs)


def place(canvas, start_s, signal, gain=1.0):
    start = max(0, int(start_s * RATE))
    end = min(len(canvas), start + len(signal))
    if end > start:
        canvas[start:end] += signal[: end - start] * gain


def midi(n):
    return 440 * 2 ** ((n - 69) / 12)


def kiyi(length_s=64.0):
    """D major pentatonic over four slow chords; arpeggios drift up and down like small waves."""
    out = np.zeros(int(RATE * length_s))
    beat = 0.5  # seconds
    chords = [(50, 57, 62, 66), (47, 54, 62, 66), (43, 50, 59, 66), (45, 52, 61, 64)]  # D, Bm, G, A
    scale = [62, 64, 66, 69, 71, 74, 76, 78, 81, 83]
    bar = 16 * beat
    for b, chord in enumerate(chords * int(length_s / bar / len(chords))):
        start = b * bar
        place(out, start, pad([midi(n) for n in chord], bar + 3.0), 0.16)
        # Bass note on the one, softly.
        place(out, start, marimba(midi(chord[0]), 3.0, 1.2), 0.22)
        for step in range(16):
            if rng.random() < 0.3:
                continue
            tone = scale[(b * 3 + step * 2 + int(rng.integers(0, 2))) % len(scale)]
            if step % 4 == 0:
                # Land on a chord tone at the start of each beat group, so the line follows the harmony.
                chord_tones = [n for n in scale if n % 12 in {c % 12 for c in chord}]
                tone = chord_tones[(step // 4 + b) % len(chord_tones)]
            place(out, start + step * beat + rng.uniform(-0.01, 0.01), marimba(midi(tone), 1.6, 0.6), rng.uniform(0.1, 0.18))
    return out


def aksam(length_s=64.0):
    """Slower and darker: E minor chords held long, sparse marimba answers."""
    out = np.zeros(int(RATE * length_s))
    chords = [(40, 47, 52, 55), (36, 43, 50, 55), (38, 45, 50, 57), (35, 42, 50, 54)]  # Em, C, D, Bm
    scale = [64, 66, 67, 69, 71, 74, 76, 78, 79, 81]
    bar = 8.0
    for b, chord in enumerate(chords * int(length_s / bar / len(chords))):
        start = b * bar
        place(out, start, pad([midi(n) for n in chord], bar + 4.0, attack=3.5, release=4.0), 0.2)
        notes = rng.choice(scale, size=3, replace=False)
        for i, n in enumerate(sorted(notes)):
            place(out, start + 1.0 + i * rng.uniform(1.4, 2.2), marimba(midi(n), 2.5, 1.0), 0.14)
    return out


def write(name, signal):
    # Make the end meet the start: fade the last three seconds into a copy of the first three.
    fade = int(RATE * 3.0)
    ramp = np.linspace(0, 1, fade)
    signal[-fade:] = signal[-fade:] * (1 - ramp) + signal[:fade] * ramp
    signal = lowpass_fft(signal, 5000)
    peak = np.max(np.abs(signal)) or 1.0
    data = (signal / peak * 0.6 * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(data.tobytes())
    print(f"{name}.wav {len(data) / RATE:.1f}s")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    write("kiyi", kiyi())
    write("aksam", aksam())
