"""
Synthesises Lazpoly's sound effects into Assets/_Lazpoly/Audio/Resources/Lazpoly/Sfx as 16-bit mono WAV.

Every sound is generated here, so the files belong to the project and need no licence. Run again after
changing a recipe:  python3 Tools/Audio/make_sfx.py   (numpy only; a fixed seed keeps the output identical).

How the sounds are made (Faz 12e, "richer, more real, more in tune, fuller"):
  * Real objects are modal: a struck die, a pawn, a gavel or a coin rings at a handful of its own resonant
    frequencies, each dying away at its own rate. `modal` sums those damped sines under a short excitation,
    so wood sounds woody (low, inharmonic, quick) and brass coins sound like coins (high, sparse, longer).
  * Everything has a body: tonal sounds carry a soft sub-octave and a felt-mallet attack, knocks carry a low
    thump, so nothing is thin. A gentle tape-like saturation glues the layers.
  * Everything is in one key: G major (G A B C D E F#), the notes the music uses, so a purchase over the
    music never clashes. Gains go up the scale, losses come down it, the big moments are full chords.
  * A small wooden room (`room`) gives each sound the same space, the table's.
  * Frequent sounds come in several takes (name.wav, name_2.wav, ...): steps, dice, clicks, coins, knocks.
    The game picks one at random each time (SoundCues.Variants), so a long game does not repeat itself.
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Lazpoly", "Audio", "Resources", "Lazpoly", "Sfx")
rng = np.random.default_rng(1977)

# G major, by name, in Hz (equal temperament, A4 = 440).
NOTE = {
    "G2": 98.00, "D3": 146.83, "G3": 196.00, "A3": 220.00, "B3": 246.94, "C4": 261.63, "D4": 293.66, "E4": 329.63,
    "F#4": 369.99, "G4": 392.00, "A4": 440.00, "B4": 493.88, "C5": 523.25, "D5": 587.33, "E5": 659.25,
    "F#5": 739.99, "G5": 783.99, "B5": 987.77, "D6": 1174.66,
}


# ---------------------------------------------------------------- building blocks

def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def spectral(signal, low=None, high=None, order=2):
    """Zero-phase filter in the frequency domain: high-pass at `low`, low-pass at `high` (Butterworth-shaped)."""
    n = len(signal)
    if n == 0:
        return signal
    spectrum = np.fft.rfft(signal)
    f = np.fft.rfftfreq(n, 1 / RATE)
    gain = np.ones_like(f)
    if high is not None:
        gain /= np.sqrt(1 + (f / high) ** (2 * order))
    if low is not None:
        with np.errstate(divide="ignore"):
            gain /= np.sqrt(1 + (low / np.maximum(f, 1e-6)) ** (2 * order))
    return np.fft.irfft(spectrum * gain, n)


def noise(seconds, low=None, high=None):
    return spectral(rng.standard_normal(int(RATE * seconds)), low, high)


def env(seconds, attack=0.002, decay=0.1):
    x = t(seconds)
    return np.clip(x / max(attack, 1e-4), 0, 1) * np.exp(-x / decay)


def modal(modes, seconds, strike=0.0015, jitter=0.0):
    """A struck object: (frequency, decay seconds, amplitude) modes, each a damped sine, under a short noise strike."""
    x = t(seconds)
    out = np.zeros_like(x)
    for freq, decay, amp in modes:
        f = freq * (1 + rng.uniform(-jitter, jitter))
        out += amp * np.sin(2 * np.pi * f * x + rng.uniform(0, 2 * np.pi)) * np.exp(-x / decay)
    out *= np.clip(x / max(strike, 1e-4), 0, 1)
    hit = noise(seconds, 300, 5000) * env(seconds, 0.0005, strike * 2) * 0.25
    return out + hit


def saturate(signal, drive=1.6):
    """A soft tape-like saturation: adds warmth and glues layers without audible distortion."""
    return np.tanh(signal * drive) / np.tanh(drive)


def place(total, *parts):
    """Mixes (start seconds, signal) parts into one buffer."""
    out = np.zeros(int(RATE * total))
    for start, sig in parts:
        i = int(start * RATE)
        j = min(len(out), i + len(sig))
        out[i:j] += sig[: j - i]
    return out


def mix(*signals):
    out = np.zeros(max(len(s) for s in signals))
    for s in signals:
        out[: len(s)] += s
    return out


def norm(signal):
    peak = np.max(np.abs(signal)) or 1.0
    return signal / peak


# ---------------------------------------------------------------- instruments

def mallet(note, decay=0.35, velocity=1.0):
    """A felt mallet on a rosewood bar with a soft sub-octave underneath: round, warm, in tune."""
    f = NOTE[note] if isinstance(note, str) else note
    seconds = decay * 4.5
    bar = modal([(f, decay, 1.0), (f * 3.93, decay * 0.25, 0.10 * velocity), (f * 9.1, decay * 0.08, 0.025 * velocity)],
                seconds, strike=0.004)
    sub = np.sin(2 * np.pi * f / 2 * t(seconds)) * env(seconds, 0.012, decay * 0.7) * 0.28
    felt = noise(seconds, 150, 1200) * env(seconds, 0.001, 0.008) * 0.12 * velocity
    return (bar + sub + felt) * velocity


def chime(notes, step, decay, velocity=1.0):
    """Notes struck one after another (step seconds apart) and left ringing together."""
    return [(i * step, mallet(n, decay, velocity * (1 - 0.08 * i))) for i, n in enumerate(notes)]


def bass(note, seconds, decay):
    """A soft upright-like bass note for the big moments, felt more than heard."""
    f = NOTE[note]
    x = t(seconds)
    sig = np.sin(2 * np.pi * f * x) + 0.35 * np.sin(4 * np.pi * f * x) + 0.1 * np.sin(6 * np.pi * f * x)
    return sig * env(seconds, 0.015, decay) * 0.5


def wood(size=1.0, seconds=0.25, brightness=1.0):
    """A wooden piece knocked on the wooden table: low inharmonic modes and a short thump (size > 1 = bigger, deeper)."""
    base = 210 / size
    modes = [(base * r * rng.uniform(0.97, 1.03), d / size ** 0.3, a) for r, d, a in
             ((1.0, 0.045, 1.0), (2.31, 0.03, 0.55 * brightness), (3.9, 0.018, 0.3 * brightness), (6.2, 0.01, 0.14 * brightness))]
    body = modal(modes, seconds, strike=0.001)
    thump = np.sin(2 * np.pi * (75 / size) * t(seconds)) * env(seconds, 0.002, 0.03 * size) * 0.6
    return body + thump


def die_hit(strength=1.0):
    """A hard plastic die on a felt-topped wooden board: a short bright knock over a dull wooden body."""
    modes = [(rng.uniform(1700, 2600), 0.012, 0.6), (rng.uniform(3200, 4200), 0.008, 0.3), (rng.uniform(380, 520), 0.03, 0.8)]
    knock = modal(modes, 0.12, strike=0.0006)
    board = wood(rng.uniform(1.2, 1.6), 0.12, 0.6) * 0.5
    return (knock * 0.7 + board) * strength


def coin(strength=1.0):
    """A brass coin dropped on a pile: sparse high modes, slightly detuned, ringing a little."""
    base = rng.uniform(1500, 1900)  # A large brass coin: lower and fuller than a small one.
    modes = [(base, 0.09, 1.0), (base * 1.47, 0.07, 0.5), (base * 2.13, 0.05, 0.3), (base * 0.61, 0.05, 0.25)]
    return spectral(modal(modes, 0.35, strike=0.0004), 300, 5000) * strength


def coins(count, spread, strength=1.0):
    return [(s, coin(strength * rng.uniform(0.5, 1.0))) for s in np.sort(rng.uniform(0, spread, count))]


def paper(seconds=0.28, strength=1.0):
    """A card or a sheet of paper turned over: band noise with a crackling, uneven envelope."""
    n = int(RATE * seconds)
    crackle = np.repeat(rng.uniform(0.3, 1.0, n // 220 + 1), 220)[:n]
    shape = np.sin(np.linspace(0, np.pi, n)) ** 1.5
    return noise(seconds, 500, 3800) * crackle * shape * 0.5 * strength


def room(signal, amount=0.2, seconds=0.6):
    """A small wooden room: early reflections and a short warm tail, the same for every sound."""
    n = int(RATE * seconds)
    impulse = spectral(rng.standard_normal(n), 120, 3200) * np.exp(-np.arange(n) / (RATE * 0.16))
    for delay, gain in ((0.011, 0.5), (0.019, 0.35), (0.027, 0.25), (0.041, 0.18)):
        impulse[int(delay * RATE)] += gain * 8
    impulse /= np.sqrt(np.sum(impulse ** 2)) or 1.0
    size = len(signal) + n
    wet = np.fft.irfft(np.fft.rfft(signal, size) * np.fft.rfft(impulse, size), size)
    out = np.zeros(size)
    out[: len(signal)] += signal
    return out + wet * amount


# ---------------------------------------------------------------- the sounds

def dice_shake():
    hits = [(s, die_hit(rng.uniform(0.25, 0.55))) for s in np.sort(rng.uniform(0, 0.4, 14))]
    rattle = noise(0.45, 400, 3000) * np.sin(np.linspace(0, np.pi, int(RATE * 0.45))) * 0.08
    return mix(place(0.55, *hits), rattle)


def dice_land():
    times, strength, parts = 0.0, 1.0, []
    for gap in (0.0, 0.085, 0.075, 0.07, 0.06, 0.055):
        times += gap * rng.uniform(0.85, 1.15)
        parts.append((times, die_hit(strength)))
        parts.append((times + rng.uniform(0.005, 0.02), die_hit(strength * 0.6)))  # The second die, just after.
        strength *= 0.62
    return place(0.75, *parts)


def pawn_step():
    return mix(wood(rng.uniform(1.3, 1.7), 0.22, 0.7), noise(0.22, 100, 900) * env(0.22, 0.001, 0.012) * 0.2)


def ui_click():
    return wood(rng.uniform(0.55, 0.7), 0.12, 0.5) * 0.6


def money_in():
    return mix(place(0.9, *chime(["B4", "D5"], 0.07, 0.32, 0.8)), place(0.9, *coins(4, 0.18, 0.16)))


def money_out():
    return mix(place(0.8, *chime(["B4", "G4"], 0.09, 0.3, 0.75)), place(0.8, *coins(2, 0.1, 0.12)))


def build():
    return place(0.8, (0.0, wood(1.0, 0.3)), (0.17, wood(0.92, 0.3) * 0.85), (0.02, mallet("G3", 0.2, 0.35)))


def bid():
    return mix(wood(0.8, 0.2, 0.9) * 0.7, mallet("B4", 0.15, 0.25))


def recipes():
    takes = {"dice_shake": (dice_shake, 2), "dice_land": (dice_land, 3), "pawn_step": (pawn_step, 4),
             "ui_click": (ui_click, 3), "money_in": (money_in, 2), "money_out": (money_out, 2),
             "build": (build, 2), "bid": (bid, 2)}
    for name, (make, count) in takes.items():
        for i in range(count):
            yield (name if i == 0 else f"{name}_{i + 1}"), make()

    yield "buy", mix(place(1.2, *chime(["G4", "B4", "D5"], 0.07, 0.4)), place(1.2, (0.0, bass("G2", 1.0, 0.35)), *coins(5, 0.2, 0.15)))
    yield "sell", mix(place(0.8, (0.0, wood(0.9, 0.25) * 0.8), *chime(["D5", "A4"], 0.1, 0.25, 0.6)), place(0.8, *coins(3, 0.12, 0.12)))
    yield "property_sale", place(1.1, (0.0, wood(1.1, 0.3)), *[(0.1 + s, sig) for s, sig in chime(["D5", "B4", "G4", "D4"], 0.1, 0.3, 0.6)])
    gavel = mix(wood(1.4, 0.5, 1.2) * 1.2, np.sin(2 * np.pi * 60 * t(0.5)) * env(0.5, 0.002, 0.05))
    yield "gavel", place(1.3, (0.0, gavel), (0.03, mallet("G4", 0.45, 0.4)), (0.03, mallet("D5", 0.45, 0.3)), (0.0, bass("G2", 1.0, 0.3)))
    yield "ui_open", paper(0.26)
    yield "your_turn", place(1.2, *chime(["D4", "G4", "D5"], 0.11, 0.42, 0.85), (0.0, bass("G2", 1.0, 0.3) * 0.6))
    yield "achievement", place(1.6, *chime(["G4", "B4", "D5", "G5"], 0.085, 0.5), (0.0, bass("G2", 1.4, 0.45)))
    # Winning: a short cadence (C, D, G) and the tonic chord left ringing over the bass.
    cadence = [(0.0, mallet("C5", 0.3)), (0.0, mallet("E4", 0.3, 0.7)), (0.0, bass("C4", 0.5, 0.2) * 0.5),
               (0.22, mallet("D5", 0.3)), (0.22, mallet("F#4", 0.3, 0.7)), (0.22, bass("D3", 0.5, 0.2)),
               *[(0.46 + s, sig) for s, sig in chime(["G4", "B4", "D5", "G5", "B5"], 0.06, 0.75)], (0.46, bass("G2", 1.8, 0.6))]
    yield "win", place(2.6, *cadence)
    # Going bankrupt: a slow fall down the scale over a low, dark G, then a soft thud as the pawn goes down.
    fall = [(i * 0.2, mallet(n, 0.45, 0.8 - i * 0.1)) for i, n in enumerate(["D4", "B3", "A3", "G3"])]
    yield "bankrupt", place(2.0, *fall, (0.0, bass("G2", 1.8, 0.7)), (0.85, wood(1.8, 0.4) * 0.7))
    # A handshake: two quick knocks on the table, then two notes that agree.
    yield "trade", place(1.0, (0.0, wood(0.9, 0.2) * 0.6), (0.06, wood(0.95, 0.2) * 0.5), *[(0.14 + s, sig) for s, sig in chime(["B4", "D5"], 0.12, 0.3, 0.7)])
    # Passing start: a rising arpeggio and a coin shower, brighter than an ordinary gain.
    yield "pass_start", mix(place(1.4, *chime(["G4", "B4", "D5", "G5"], 0.08, 0.38), (0.0, bass("G2", 1.2, 0.4))), place(1.4, *coins(8, 0.4, 0.15)))
    # A hotel going up: two heavy knocks, then a full chord, bigger than a house.
    yield "hotel", place(1.6, (0.0, wood(1.2, 0.35)), (0.16, wood(1.1, 0.35)), *[(0.32 + s, sig) for s, sig in chime(["G4", "B4", "D5", "G5"], 0.06, 0.55)],
                         (0.32, bass("G2", 1.2, 0.5)))
    yield "turn_pass", mallet("D4", 0.32, 0.55)


# Small, frequent sounds sit under the big moments already in the files, not only in the game's mix.
QUIET = {"pawn_step": 0.38, "ui_click": 0.3, "ui_open": 0.32, "bid": 0.45, "dice_shake": 0.5, "dice_land": 0.6, "turn_pass": 0.4}
PEAK = 0.72


def write(name, signal):
    signal = saturate(norm(spectral(signal, 30, 6000)), 1.4)
    signal[: int(RATE * 0.0015)] *= np.linspace(0, 1, int(RATE * 0.0015))  # No click at the very start.
    signal = room(signal)
    base = name.rsplit("_", 1)[0] if name[-1].isdigit() else name
    data = norm(signal) * QUIET.get(base, PEAK)
    fade = min(len(data), int(RATE * 0.015))
    data[-fade:] *= np.linspace(1, 0, fade)
    trimmed = trim(data)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes((trimmed * 32767).astype("<i2").tobytes())
    return len(trimmed) / RATE


def trim(data, floor=10 ** (-60 / 20)):
    """Cuts the silent tail (under -60 dB) and fades the last few milliseconds."""
    loud = np.nonzero(np.abs(data) > floor)[0]
    end = min(len(data), (loud[-1] + int(RATE * 0.02)) if len(loud) else len(data))
    data = data[:end].copy()
    fade = min(len(data), int(RATE * 0.01))
    data[-fade:] *= np.linspace(1, 0, fade)
    return data


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, signal in recipes():
        print(f"{name}.wav {write(name, signal):.2f}s")
