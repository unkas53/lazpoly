"""
Builds the bus's four sounds (the owner's wish, 2026-11-11: the engine starting, the horn, the brakes and the
engine pulling away) for the ride from the bus station. No recording of a bus is among the game's CC0 sources, so
these are worked out here from scratch, by plain arithmetic (no AI, no samples):

- bus_start: the starter turning the engine over, the engine catching and settling to its idle.
- bus_rev:   the engine pulling away through its gears and holding a cruise; the game fades it out when the bus stops.
- bus_horn:  two short notes of a two-tone horn.
- bus_brake: a short squeal of the brakes and the hiss of their air let out.

The engine is a six-cylinder diesel in miniature: one combustion pulse for each firing, no two cylinders quite alike
(that unevenness is the diesel's lumpy beat), ringing in a few fixed resonances of the body and the exhaust, with a
clatter of noise that rides on the pulses. Every sound is brought to the loudness of the other cues by
make_sfx_kenney.level, so the game's own mix (SoundCues.Volume) decides what sits under what.

    python Tools/Audio/make_sfx_bus.py        (needs numpy and scipy)
"""
import os
import sys

import numpy as np
from scipy.signal import butter, lfilter, sosfilt

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_sfx_kenney import OUT, RATE, level, write  # noqa: E402

IDLE = 30.0  # Firings a second at idle: 600 turns a minute, three firings a turn.
CYLINDERS = np.array([1.0, 0.80, 0.96, 0.72, 1.04, 0.86])


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def curve(times, values, length):
    """A value at every sample, running smoothly through the given points."""
    t = seconds(length)
    raw = np.interp(t, times, values)
    width = int(0.03 * RATE)
    window = np.hanning(width * 2 + 1)
    return np.convolve(np.pad(raw, width, mode="edge"), window / window.sum(), mode="valid")


def band(x, low, high, order=2):
    return sosfilt(butter(order, [low, high], btype="band", fs=RATE, output="sos"), x)


def lowpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="low", fs=RATE, output="sos"), x)


def highpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="high", fs=RATE, output="sos"), x)


def ring(x, frequency, sharpness):
    """One resonance: a filter ringing at a frequency, deaf to what lies far below and above it."""
    r = np.exp(-np.pi * frequency / sharpness / RATE)
    a = [1.0, -2.0 * r * np.cos(2.0 * np.pi * frequency / RATE), r * r]
    return lfilter([(1.0 - r * r) / 2.0, 0.0, -(1.0 - r * r) / 2.0], a, x)


def drift(count, rate, rng):
    """Slow random wander between -1 and 1, changing about `rate` times a second."""
    steps = max(4, int(count / RATE * rate) + 3)
    points = rng.uniform(-1.0, 1.0, steps)
    return np.interp(np.linspace(0, steps - 1, count), np.arange(steps), points)


def unit(x):
    """A part brought to the same strength as the others, so the mix below is in plain shares."""
    return x / max(float(np.sqrt(np.mean(x * x))), 1e-9)


def engine(firing, load, seed):
    """The engine's sound for a firing rate (a second) and a load (0 idling, 1 pulling hard) at every sample."""
    rng = np.random.default_rng(seed)
    count = len(firing)
    phase = np.cumsum(firing * (1.0 + 0.012 * drift(count, 7.0, rng))) / RATE
    firings = np.floor(phase).astype(np.int64)
    part = phase - firings
    # One combustion: a quick rise and a decay within the firing's own time; harder and sharper under load.
    strength = CYLINDERS[firings % len(CYLINDERS)] * (1.0 + 0.06 * rng.standard_normal(firings.max() + 1)[firings])
    pulse = strength * np.exp(-(5.0 + 8.0 * load) * part) * (1.0 - np.exp(-part * 16.0))
    beat = pulse - lowpass(pulse, 8.0)
    # What the pulses ring in: the exhaust's boom, the body's growl, and a hard edge that comes out under load.
    boom = unit(ring(beat, 92.0, 2.5) + 0.25 * np.sin(2.0 * np.pi * phase))
    growl = unit(ring(beat, 210.0, 3.0) + 0.9 * ring(beat, 330.0, 3.5) + 0.8 * ring(beat, 520.0, 4.0))
    edge = unit(ring(beat, 900.0, 3.5) + 0.8 * ring(beat, 1500.0, 3.5) + 0.5 * ring(beat, 2300.0, 3.0))
    # The diesel's clatter: noise that opens with each firing; and the rush of the exhaust, louder the faster it turns.
    clatter = unit(band(rng.standard_normal(count), 900.0, 3800.0) * (pulse + 0.08))
    rush = unit(band(rng.standard_normal(count), 250.0, 1100.0))
    speed = firing / IDLE
    mix = 0.6 * boom + 1.0 * growl + (0.18 + 0.4 * load) * edge + (0.14 + 0.16 * load) * clatter + 0.10 * rush * speed * (0.4 + load)
    return mix * (0.55 + 0.6 * load)


def fade(x, attack, release):
    out = x.copy()
    a, r = int(attack * RATE), int(release * RATE)
    if a > 0:
        out[:a] *= np.linspace(0.0, 1.0, a) ** 2
    if r > 0:
        out[-r:] *= np.linspace(1.0, 0.0, r) ** 2
    return out


def start():
    """The starter turns the engine over for half a second, it catches, flares and settles to its idle."""
    length, caught = 1.25, 0.52
    t = seconds(length)
    firing = curve([0.0, 0.08, caught, caught + 0.2, caught + 0.42, length], [7.0, 10.0, 11.5, 43.0, 33.0, IDLE], length)
    load = curve([0.0, caught, caught + 0.12, caught + 0.4, length], [0.05, 0.1, 0.9, 0.3, 0.12], length)
    motor = engine(firing, load, 3)
    motor = unit(motor) * curve([0.0, 0.05, caught, caught + 0.1, length], [0.0, 0.45, 0.5, 1.0, 0.9], length)
    # The starter's whine: a buzz that sags each time a cylinder comes up to its compression, gone once it catches.
    rng = np.random.default_rng(4)
    turn = np.cumsum(firing) / RATE
    sag = 0.5 - 0.5 * np.cos(2.0 * np.pi * turn)
    pitch = 410.0 * (1.0 - 0.14 * sag) * (1.0 + 0.01 * drift(len(t), 20.0, rng))
    angle = 2.0 * np.pi * np.cumsum(pitch) / RATE
    buzz = sum(np.sin(k * angle) / k for k in range(1, 9))
    whine = band(buzz, 500.0, 2600.0) * (0.75 - 0.3 * sag) + 0.25 * band(rng.standard_normal(len(t)), 1500.0, 5000.0) * (0.4 + 0.6 * sag)
    whine = unit(whine) * curve([0.0, 0.03, caught - 0.02, caught + 0.07, length], [0.0, 1.0, 1.0, 0.0, 0.0], length)
    return fade(motor + 0.45 * whine, 0.004, 0.22)


def rev():
    """Pulling away: up through first gear, a shift, second, a shift, and a steady cruise."""
    length = 4.6
    firing = curve([0.0, 0.15, 1.15, 1.30, 2.45, 2.60, 3.4, length],
                   [IDLE, 36.0, 88.0, 56.0, 84.0, 60.0, 72.0, 74.0], length)
    load = curve([0.0, 0.1, 1.1, 1.2, 1.32, 2.4, 2.5, 2.62, 3.4, length],
                 [0.15, 0.95, 1.0, 0.25, 0.95, 1.0, 0.3, 0.8, 0.6, 0.55], length)
    motor = engine(firing, load, 7)
    # The gearbox sings a little, higher the faster the engine turns.
    angle = 2.0 * np.pi * np.cumsum(firing * 7.3) / RATE
    gear = 0.05 * (np.sin(angle) + 0.4 * np.sin(2.0 * angle)) * load
    return fade(unit(motor) + gear, 0.03, 0.5)


def horn():
    """Two notes of a two-tone horn: a short one and a longer one."""
    def note(length):
        t = seconds(length)
        rng = np.random.default_rng(int(length * 1000))
        out = np.zeros(len(t))
        for pitch, gain in ((349.2, 1.0), (440.0, 0.9)):
            # The diaphragm comes up to its note in a few hundredths of a second, and wavers a little.
            bend = 1.0 - 0.06 * np.exp(-t / 0.018) + 0.004 * drift(len(t), 35.0, rng)
            angle = 2.0 * np.pi * np.cumsum(pitch * bend) / RATE
            out += gain * sum(np.sin(k * angle + 0.3 * k) / k ** 0.8 for k in range(1, 24))
        # The trumpet of the horn: its blare lies around two to three thousand a second.
        blare = out * 0.6 + 0.8 * ring(out, 2300.0, 2.2) + 0.6 * ring(out, 1250.0, 3.0) + 0.25 * ring(out, 3300.0, 3.0)
        return fade(lowpass(blare, 5500.0), 0.008, 0.045)

    first, second, gap = note(0.12), note(0.30), int(0.085 * RATE)
    return np.concatenate([first, np.zeros(gap), second])


def brake():
    """The brakes squeal for a moment as the bus stops, then let their air out with a hiss."""
    length = 0.95
    t = seconds(length)
    rng = np.random.default_rng(11)
    # The squeal: a thin falling note that wavers, never loud.
    pitch = curve([0.0, 0.32, length], [2050.0, 1650.0, 1650.0], length) * (1.0 + 0.006 * np.sin(2.0 * np.pi * 27.0 * t))
    angle = 2.0 * np.pi * np.cumsum(pitch) / RATE
    squeal = (np.sin(angle) + 0.35 * np.sin(2.0 * angle) + 0.15 * np.sin(3.0 * angle))
    squeal *= curve([0.0, 0.06, 0.24, 0.33, length], [0.0, 0.8, 1.0, 0.0, 0.0], length) * (0.8 + 0.2 * drift(len(t), 40.0, rng))
    # The air: a sharp burst that dies away, brightest around three to four thousand.
    since = np.maximum(t - 0.34, 0.0)
    burst = np.where(t >= 0.34, (1.0 - np.exp(-since / 0.004)) * (0.75 * np.exp(-since / 0.11) + 0.25 * np.exp(-since / 0.3)), 0.0)
    noise = rng.standard_normal(len(t))
    hiss = (band(noise, 1400.0, 9000.0) + 0.8 * ring(noise, 3600.0, 1.5)) * burst
    # The body rocks back on its springs: one soft low thump.
    thump = np.where(t >= 0.33, np.sin(2.0 * np.pi * 62.0 * since) * np.exp(-since / 0.07), 0.0)
    return fade(0.16 * squeal + 1.0 * hiss + 0.5 * thump, 0.004, 0.25)


SOUNDS = {"bus_start": start, "bus_rev": rev, "bus_horn": horn, "bus_brake": brake}


def main(only=None):
    for cue, make in SOUNDS.items():
        if only and cue not in only:
            continue
        x = level(highpass(make(), 35.0), 1.0)
        write(os.path.join(OUT, cue + ".wav"), x)
        print(f"{cue:16s} {len(x) / RATE:5.2f} s")


if __name__ == "__main__":
    main(sys.argv[1:])
