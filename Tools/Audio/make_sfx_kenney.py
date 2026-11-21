"""
Builds the game's sound effects from Kenney's CC0 recordings (kenney.nl: Casino Audio, Impact Sounds,
Interface Sounds, RPG Audio, Music Jingles, UI Audio). Only the recordings the game uses are kept, under
Tools/Audio/Kenney/<pack>/ next to each pack's License.txt.

Each cue is one recording or a few laid over each other (a deed card, coins and a short pizzicato phrase
for a purchase), cut to length, faded out, turned to mono 44.1 kHz 16-bit and brought to the same
loudness (a jingle and a handful of coins should not differ by 20 dB as their peaks would leave them), so
the game's own mix (SoundCues.Volume) decides what sits under what. The musical cues all come from the
pizzicato set, so the table sounds like one instrument; only the win gets the bigger steel-drum phrase.
The packs hold no rocket and no explosion, so the missile's engine, its whistle and the body of its boom are
worked out here (shaped noise and tones: made()), under and over the recordings.

    python Tools/Audio/make_sfx_kenney.py        (needs numpy and ffmpeg on PATH)
    python Tools/Audio/make_sfx_kenney.py firework    (only the cues named)
"""
import os
import subprocess
import wave

import numpy as np

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, "Kenney")
OUT = os.path.normpath(os.path.join(ROOT, "..", "..", "Assets", "_Lazpoly", "Audio", "Resources", "Lazpoly", "Sfx"))
RATE = 44100
PEAK = 0.95
# Loudness every cue is brought to: the RMS over the part that sounds (not the silence around it), about
# -19 dBFS. A click or a die cannot get that loud without clipping; those stop near the peak instead.
LOUDNESS = 0.11
DRIVE = 2.0

CASINO, IMPACT, UI, RPG, JINGLE, CLICKS = "casino-audio", "impact-sounds", "interface-sounds", "rpg-audio", "music-jingles", "ui-audio"


def layer(pack, name, at=0.0, db=0.0):
    return pack, name, at, db


def hit(pack, name, at=0.0, db=0.0, rate=1.0):
    """A layer brought to the same peak first (so db is its level whatever the recording's own), played at
    rate times its speed: below 1 it is slower, longer and lower, as a bigger thing would sound."""
    return pack, name, at, db, rate


def made(sound, at=0.0, db=0.0):
    """A layer that is not a recording but worked out here (a rocket's roar is shaped noise): sound() gives its
    samples, brought to one peak like a hit()."""
    return None, sound, at, db, 1.0


def noise(seconds, low, high, seed):
    """Noise between two frequencies, the same every time it is built."""
    n = int(seconds * RATE)
    spectrum = np.fft.rfft(np.random.default_rng(seed).standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    # Soft edges an octave wide, so the band does not ring.
    keep = np.clip(np.log2(np.maximum(freqs, 1e-9) / low) + 1, 0, 1) * np.clip(np.log2(high / np.maximum(freqs, 1e-9)) + 1, 0, 1)
    return np.fft.irfft(spectrum * keep, n)


def swell(seconds, points):
    """A loudness curve through (share of the time, level) points."""
    t = np.linspace(0, 1, int(seconds * RATE))
    return np.interp(t, [p[0] for p in points], [p[1] for p in points])


def glide(seconds, from_hz, to_hz):
    """A tone gliding from one pitch to another, with a little wobble, as a falling thing whistles."""
    n = int(seconds * RATE)
    t = np.arange(n) / RATE
    hz = from_hz * (to_hz / from_hz) ** (t / seconds) * (1 + 0.012 * np.sin(2 * np.pi * 9 * t))
    return np.sin(2 * np.pi * np.cumsum(hz) / RATE)


def rocket_roar():
    """The missile's engine over its whole flight: it catches with a burst, burns low, and climbs to a hiss as it
    gathers speed and comes down."""
    low = noise(MISSILE_FLIGHT, 70, 500, 561) * swell(MISSILE_FLIGHT, [(0, 0), (0.02, 1), (0.12, 0.75), (0.5, 0.5), (1, 0.6)])
    high = noise(MISSILE_FLIGHT, 500, 3200, 562) * swell(MISSILE_FLIGHT, [(0, 0), (0.03, 0.5), (0.15, 0.25), (0.6, 0.4), (1, 0.75)])
    # The burn flutters a little, like a flame.
    t = np.arange(len(low)) / RATE
    return (low + 0.45 * high) * (1 + 0.18 * np.sin(2 * np.pi * 23 * t) + 0.1 * np.sin(2 * np.pi * 37 * t + 1))


def rocket_whistle():
    """It whistles as it comes down on the town: a falling tone that grows until the hit."""
    seconds = MISSILE_FLIGHT * 0.5
    return glide(seconds, 1500, 620) * swell(seconds, [(0, 0), (0.3, 0.35), (1, 1)])


def blast_boom():
    """The body of an explosion: a deep thump that drops in pitch, and a low rumble dying away under it."""
    seconds = 1.2
    t = np.arange(int(seconds * RATE)) / RATE
    thump = np.sin(2 * np.pi * np.cumsum(38 + 60 * np.exp(-t * 14)) / RATE) * np.exp(-t * 5.5)
    rumble = noise(seconds, 35, 260, 563) * np.exp(-t * 4.2)
    crack = noise(seconds, 300, 4500, 564) * np.exp(-t * 26)
    return thump + 0.8 * rumble / np.abs(rumble).max() + 0.5 * crack / np.abs(crack).max()


def shield_fizz():
    """A missile spent on a shield: a short bright hiss, gone at once."""
    seconds = 0.45
    t = np.arange(int(seconds * RATE)) / RATE
    return noise(seconds, 1200, 7000, 565) * np.exp(-t * 11)


def pizzi(n, at=0.0, db=0.0):
    return layer(JINGLE, f"jingles_PIZZI{n:02d}.ogg", at, db)


# When the jail's gate strikes its frame in jail_shut, from the cue's start.
JAIL_STRIKE = 0.22

# A building leaving its square (Faz 52). The cue starts with the animation, so these are the animation's own
# moments: a sold building pops into its puff SOLD_POP seconds in (LeavingMotion.SoldSeconds * PopAt); a torn-down
# one is jolted at once, gives way FALL_DROP seconds in (DemolishedSeconds * Jolt) and is down at FALL_DOWN.
SOLD_POP = 0.45
FALL_DROP = 0.2
FALL_DOWN = 0.9

# The missile card (Faz 56). missile_launch starts as the missile leaves its hatch and lasts its flight
# (MissileFlight.Seconds); missile_blast and missile_block start at the hit.
MISSILE_FLIGHT = 1.7

# cue (or cue_take) -> (layers, longest seconds or None, fade-out seconds)
RECIPES = {
    # The dice animation lasts 0.8 s: the shake stops with it, the throw is the dice landing.
    "dice_shake": ([layer(CASINO, "dice-shake-1.ogg")], 0.8, 0.15),
    "dice_shake_2": ([layer(CASINO, "dice-shake-2.ogg")], 0.8, 0.15),
    "dice_shake_3": ([layer(CASINO, "dice-shake-3.ogg")], 0.8, 0.15),
    "dice_land": ([layer(CASINO, "dice-throw-1.ogg")], None, 0.05),
    "dice_land_2": ([layer(CASINO, "dice-throw-2.ogg")], None, 0.05),
    "dice_land_3": ([layer(CASINO, "dice-throw-3.ogg")], None, 0.05),
    # A pawn set down on a wooden board, one square at a time.
    "pawn_step": ([layer(IMPACT, "impactWood_light_000.ogg")], None, 0.03),
    "pawn_step_2": ([layer(IMPACT, "impactWood_light_001.ogg")], None, 0.03),
    "pawn_step_3": ([layer(IMPACT, "impactWood_light_002.ogg")], None, 0.03),
    "pawn_step_4": ([layer(IMPACT, "impactWood_light_003.ogg")], None, 0.03),
    "ui_click": ([layer(CLICKS, "click1.ogg")], None, 0.01),
    "ui_click_2": ([layer(CLICKS, "click3.ogg")], None, 0.01),
    "ui_click_3": ([layer(CLICKS, "click4.ogg")], None, 0.01),
    # Cards and trade offers slide onto the table like paper.
    "ui_open": ([layer(CASINO, "card-slide-1.ogg")], None, 0.05),
    "money_in": ([layer(RPG, "handleCoins.ogg")], None, 0.08),
    "money_in_2": ([layer(RPG, "handleCoins2.ogg"), layer(CASINO, "chips-collide-1.ogg", 0.05, -6)], None, 0.05),
    "money_out": ([layer(CASINO, "chips-stack-1.ogg")], None, 0.04),
    "money_out_2": ([layer(CASINO, "chips-stack-3.ogg")], None, 0.04),
    # The deed card laid down, the coins, and a small rising phrase.
    "buy": ([layer(CASINO, "card-place-2.ogg"), layer(RPG, "handleCoins.ogg", 0.05, -3), pizzi(8, 0.18, -4)], None, 0.1),
    # Two hammer knocks for a house; heavier knocks and a rising phrase for a hotel.
    "build": ([layer(IMPACT, "impactWood_medium_000.ogg"), layer(IMPACT, "impactWood_medium_001.ogg", 0.17)], None, 0.05),
    "build_2": ([layer(IMPACT, "impactWood_medium_002.ogg"), layer(IMPACT, "impactWood_medium_003.ogg", 0.15)], None, 0.05),
    "hotel": ([layer(IMPACT, "impactWood_heavy_000.ogg"), layer(IMPACT, "impactWood_heavy_001.ogg", 0.16), pizzi(15, 0.32, -2)], None, 0.1),
    # Selling: a building comes off with a knock and coins come back; a town goes with its card and a falling phrase.
    "sell": ([layer(IMPACT, "impactWood_light_004.ogg"), layer(RPG, "handleCoins2.ogg", 0.1)], None, 0.05),
    "property_sale": ([layer(CASINO, "card-place-1.ogg"), layer(RPG, "handleCoins2.ogg", 0.1, -3), pizzi(5, 0.2, -5)], None, 0.1),
    # Two cards change hands.
    "trade": ([layer(CASINO, "card-slide-2.ogg"), layer(CASINO, "card-slide-3.ogg", 0.22), pizzi(13, 0.35, -5)], None, 0.1),
    "pass_start": ([layer(RPG, "handleCoins.ogg"), pizzi(4, 0.05, -2)], None, 0.1),
    "your_turn": ([pizzi(16)], None, 0.08),
    "turn_pass": ([layer(UI, "pluck_002.ogg")], None, 0.03),
    "achievement": ([pizzi(10)], None, 0.1),
    "win": ([layer(JINGLE, "jingles_STEEL02.ogg")], None, 0.15),
    "bankrupt": ([pizzi(1), layer(IMPACT, "impactSoft_heavy_000.ogg", 0.85, -4)], None, 0.15),
    # A firework over a trade agreement's towns: a dull thump, then the sparks crackling as they spread (Faz 27).
    "firework": ([layer(IMPACT, "impactPunch_heavy_000.ogg"),
                  layer(IMPACT, "impactGlass_light_000.ogg", 0.10, -9), layer(IMPACT, "impactMetal_light_000.ogg", 0.16, -14),
                  layer(IMPACT, "impactGlass_light_002.ogg", 0.21, -11), layer(IMPACT, "impactGlass_light_003.ogg", 0.30, -13),
                  layer(IMPACT, "impactMetal_light_001.ogg", 0.38, -16), layer(IMPACT, "impactGlass_light_004.ogg", 0.47, -16)], 0.9, 0.25),
    # The jail's iron gate (Faz 46). Opening: the bolt is drawn and the hinges creak. Shutting: a creak, the leaf
    # strikes its frame JAIL_STRIKE seconds in (PawnView.JailStrikeAt plays the cue that long before the gate is
    # shut) and the lock turns.
    "jail_open": ([layer(RPG, "metalLatch.ogg"), layer(RPG, "creak3.ogg", 0.10, -5)], None, 0.05),
    "jail_shut": ([layer(RPG, "creak3.ogg", 0.0, -8), layer(IMPACT, "impactMetal_heavy_001.ogg", JAIL_STRIKE),
                   layer(IMPACT, "impactMetal_medium_003.ogg", JAIL_STRIKE + 0.005, -6), layer(RPG, "metalClick.ogg", 0.52, 4)], None, 0.08),
    # A building sold (Faz 52): a knock as it comes off, the swish of its spring, then as it pops into its puff the
    # till: the drawer's latch, a soft puff of air, the bell, and coins again and again as the dollar signs rise.
    "building_sold": ([hit(IMPACT, "impactWood_light_004.ogg", 0.0, -9),
                       hit(RPG, "cloth4.ogg", 0.20, -5),
                       hit(RPG, "metalLatch.ogg", SOLD_POP - 0.03, -7),
                       hit(RPG, "dropLeather.ogg", SOLD_POP - 0.01, -5), hit(RPG, "cloth2.ogg", SOLD_POP, -8), hit(IMPACT, "impactSoft_medium_001.ogg", SOLD_POP, -9),
                       hit(IMPACT, "impactBell_heavy_000.ogg", SOLD_POP + 0.02, 0, 2.5), hit(IMPACT, "impactMetal_light_003.ogg", SOLD_POP + 0.02, -9),
                       hit(RPG, "handleCoins.ogg", SOLD_POP + 0.05, -2), hit(RPG, "handleCoins2.ogg", SOLD_POP + 0.45, -6, 0.93),
                       hit(RPG, "handleCoins.ogg", SOLD_POP + 0.70, -9, 1.12), hit(RPG, "handleCoins2.ogg", SOLD_POP + 1.05, -12, 1.1)], 1.9, 0.3),
    # A building torn down (Faz 52). The jolt: timber cracks and the walls take a blow. Then it gives way: masonry
    # crumbling ever louder, planks breaking, stones clattering, a window or two; it comes down with a deep crash; and the rubble
    # lands and settles, piece after piece, quieter and quieter.
    "building_fall": ([hit(RPG, "chop.ogg", 0.0, -3), hit(IMPACT, "impactPlank_medium_002.ogg", 0.01, -5), hit(IMPACT, "impactMining_002.ogg", 0.0, -5),
                       hit(UI, "drop_002.ogg", 0.09, -15), hit(IMPACT, "impactGeneric_light_001.ogg", 0.14, -13),
                       hit(IMPACT, "footstep_snow_001.ogg", FALL_DROP + 0.02, -12, 0.85), hit(IMPACT, "impactMining_000.ogg", FALL_DROP + 0.05, -9, 0.85),
                       hit(IMPACT, "impactPlank_medium_000.ogg", FALL_DROP + 0.11, -9), hit(IMPACT, "footstep_snow_003.ogg", FALL_DROP + 0.15, -10, 0.8),
                       hit(RPG, "doorClose_3.ogg", FALL_DROP + 0.21, -7, 0.85), hit(CASINO, "chips-handle-5.ogg", FALL_DROP + 0.25, -9, 0.55), hit(IMPACT, "footstep_snow_002.ogg", FALL_DROP + 0.27, -8, 0.75),
                       hit(IMPACT, "impactGlass_light_001.ogg", FALL_DROP + 0.30, -12), hit(IMPACT, "impactMining_003.ogg", FALL_DROP + 0.32, -6, 0.8),
                       hit(IMPACT, "impactPlank_medium_004.ogg", FALL_DROP + 0.39, -7, 0.9), hit(IMPACT, "footstep_snow_004.ogg", FALL_DROP + 0.43, -6, 0.7),
                       hit(IMPACT, "impactGlass_light_003.ogg", FALL_DROP + 0.47, -12, 1.1), hit(IMPACT, "footstep_snow_001.ogg", FALL_DROP + 0.53, -5, 0.65),
                       hit(RPG, "bookPlace2.ogg", FALL_DROP + 0.58, -5, 0.8), hit(IMPACT, "footstep_snow_003.ogg", FALL_DROP + 0.62, -4, 0.6),
                       hit(IMPACT, "impactMining_001.ogg", FALL_DOWN - 0.03, 0, 0.6), hit(IMPACT, "impactPlate_heavy_004.ogg", FALL_DOWN - 0.01, -2, 0.8),
                       hit(IMPACT, "impactPunch_heavy_000.ogg", FALL_DOWN, -3), hit(RPG, "doorClose_4.ogg", FALL_DOWN, -2, 0.7),
                       hit(RPG, "doorClose_1.ogg", FALL_DOWN + 0.02, -4, 0.7), hit(CASINO, "chips-handle-5.ogg", FALL_DOWN + 0.08, -6, 0.5), hit(IMPACT, "footstep_snow_002.ogg", FALL_DOWN + 0.04, -5, 0.55),
                       hit(RPG, "bookPlace1.ogg", FALL_DOWN + 0.20, -7), hit(IMPACT, "footstep_snow_000.ogg", FALL_DOWN + 0.27, -12, 0.8),
                       hit(IMPACT, "impactGeneric_light_002.ogg", FALL_DOWN + 0.33, -12), hit(UI, "drop_004.ogg", FALL_DOWN + 0.41, -15),
                       hit(RPG, "bookPlace3.ogg", FALL_DOWN + 0.50, -10, 1.2), hit(IMPACT, "impactGeneric_light_003.ogg", FALL_DOWN + 0.62, -15),
                       hit(UI, "drop_002.ogg", FALL_DOWN + 0.74, -17), hit(IMPACT, "impactGeneric_light_004.ogg", FALL_DOWN + 0.88, -18, 1.1),
                       hit(UI, "drop_001.ogg", FALL_DOWN + 1.0, -20)], 2.0, 0.35),
    # The missile card (Faz 56). Lift-off: a thump as it catches and the hatch's iron rattles, the engine's roar for
    # the whole flight, and its whistle as it comes down; it stops where the blast begins.
    "missile_launch": ([hit(IMPACT, "impactPunch_heavy_000.ogg", 0.0, -4, 0.7), hit(IMPACT, "impactMetal_medium_003.ogg", 0.01, -12, 0.8),
                        hit(RPG, "cloth4.ogg", 0.03, -8, 0.7),
                        made(rocket_roar, 0.0, -3), made(rocket_whistle, MISSILE_FLIGHT * 0.5, -13)], MISSILE_FLIGHT + 0.04, 0.06),
    # The blast: a deep boom (heavy blows played slow, over a worked-out thump and rumble), walls breaking in it, then
    # the rubble thrown far coming down piece after piece, quieter and quieter.
    "missile_blast": ([made(blast_boom, 0.0, 0),
                       hit(IMPACT, "impactPunch_heavy_000.ogg", 0.0, -2, 0.5), hit(IMPACT, "impactMining_001.ogg", 0.0, -3, 0.5),
                       hit(IMPACT, "impactPlate_heavy_004.ogg", 0.01, -5, 0.6), hit(RPG, "doorClose_4.ogg", 0.01, -4, 0.55),
                       hit(IMPACT, "impactPlank_medium_002.ogg", 0.03, -8, 0.9), hit(IMPACT, "impactMining_003.ogg", 0.06, -8, 0.7),
                       hit(IMPACT, "impactGlass_light_001.ogg", 0.08, -13), hit(IMPACT, "footstep_snow_002.ogg", 0.10, -8, 0.6),
                       hit(CASINO, "chips-handle-5.ogg", 0.16, -9, 0.5), hit(IMPACT, "impactGlass_light_003.ogg", 0.20, -15, 1.1),
                       hit(IMPACT, "footstep_snow_004.ogg", 0.28, -9, 0.65),
                       hit(RPG, "bookPlace2.ogg", 0.62, -9, 0.8), hit(IMPACT, "impactGeneric_light_002.ogg", 0.74, -12),
                       hit(RPG, "bookPlace1.ogg", 0.85, -10), hit(IMPACT, "footstep_snow_000.ogg", 0.93, -13, 0.8),
                       hit(UI, "drop_004.ogg", 1.04, -15), hit(RPG, "bookPlace3.ogg", 1.15, -12, 1.2),
                       hit(IMPACT, "impactGeneric_light_003.ogg", 1.28, -15), hit(UI, "drop_002.ogg", 1.42, -17),
                       hit(IMPACT, "impactGeneric_light_004.ogg", 1.58, -18, 1.1), hit(UI, "drop_001.ogg", 1.72, -20)], 2.2, 0.35),
    # A missile spent on a protection card's shield: no boom. A dull knock, the shield ringing like glass and a
    # bell, and a bright hiss gone at once.
    "missile_block": ([hit(IMPACT, "impactSoft_heavy_000.ogg", 0.0, -5), hit(IMPACT, "impactBell_heavy_000.ogg", 0.0, -3, 1.6),
                       hit(IMPACT, "impactGlass_light_000.ogg", 0.01, -6), hit(IMPACT, "impactMetal_light_003.ogg", 0.02, -10),
                       made(shield_fizz, 0.0, -8), hit(RPG, "cloth2.ogg", 0.05, -9),
                       hit(IMPACT, "impactGlass_light_002.ogg", 0.16, -14), hit(IMPACT, "impactGlass_light_004.ogg", 0.27, -17)], 1.1, 0.3),
}

# Small, frequent sounds sit under the big moments already in the files, not only in the game's mix.
QUIET = {"firework": 0.7, "pawn_step": 0.5, "ui_click": 0.45, "ui_open": 0.5, "turn_pass": 0.5, "dice_shake": 0.7, "dice_land": 0.8}

FOLDERS = {JINGLE: ["8-Bit jingles", "Hit jingles", "Pizzicato jingles", "Sax jingles", "Steel jingles"]}


def source(pack, name):
    for folder in [""] + FOLDERS.get(pack, []):
        path = os.path.join(SRC, pack, folder, name)
        if os.path.exists(path):
            return path
    raise FileNotFoundError(f"{pack}/{name}")


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(RATE), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)


def trim_silence(x, floor=0.002):
    loud = np.nonzero(np.abs(x) > floor)[0]
    return x[loud[0]:loud[-1] + 1] if len(loud) else x


def respeed(x, rate):
    """The recording played at rate times its speed (pitch and length change together, as on a tape)."""
    if rate == 1.0 or len(x) < 2:
        return x
    return np.interp(np.arange(0, len(x) - 1, rate), np.arange(len(x)), x)


def build(layers, longest, fade):
    parts = []
    for pack, name, at, db, *more in layers:
        sig = name() if pack is None else trim_silence(decode(source(pack, name)))
        if more:  # A hit() or a made(): brought to one peak, and played faster or slower.
            sig = respeed(sig / max(np.abs(sig).max(), 1e-9), more[0])
        parts.append((int(at * RATE), sig * 10 ** (db / 20)))
    out = np.zeros(max(start + len(sig) for start, sig in parts))
    for start, sig in parts:
        out[start:start + len(sig)] += sig
    if longest:
        out = out[:int(longest * RATE)]
    ramp = min(int(fade * RATE), len(out) // 2)
    if ramp > 0:
        out[-ramp:] *= np.linspace(1, 0, ramp) ** 2
    attack = min(int(0.002 * RATE), len(out))
    out[:attack] *= np.linspace(0, 1, attack)
    return out


def active_rms(x):
    frame = int(0.02 * RATE)
    frames = x[:len(x) // frame * frame].reshape(-1, frame)
    levels = np.sqrt((frames ** 2).mean(axis=1))
    loud = levels[levels > levels.max() * 0.03]
    return float(np.sqrt((loud ** 2).mean())) if len(loud) else 0.0


def level(x, quiet):
    # Sharp sounds may be driven up to 6 dB past the peak; a soft limiter rounds those few tips off,
    # so dice and coins are heard next to a jingle without their attack being clipped hard.
    gain = min(LOUDNESS * quiet / max(active_rms(x), 1e-9), DRIVE * PEAK * quiet / max(np.abs(x).max(), 1e-9))
    return PEAK * np.tanh(x * gain / PEAK)


def write(path, x):
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def main(only=None):
    for cue, (layers, longest, fade) in RECIPES.items():
        if only and cue not in only:
            continue
        x = build(layers, longest, fade)
        base = cue.rsplit("_", 1)[0] if cue[-1].isdigit() else cue
        x = level(x, QUIET.get(base, 1.0))
        write(os.path.join(OUT, cue + ".wav"), x)
        print(f"{cue:16s} {len(x) / RATE:5.2f} s")


if __name__ == "__main__":
    import sys
    main(sys.argv[1:])
