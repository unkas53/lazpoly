"""
Builds the game's sound effects from Kenney's CC0 recordings (kenney.nl: Casino Audio, Impact Sounds,
Interface Sounds, RPG Audio, Music Jingles, UI Audio). Only the recordings the game uses are kept, under
Tools/Audio/Kenney/<pack>/ next to each pack's License.txt.

Each cue is one recording or a few laid over each other (a deed card, coins and a short pizzicato phrase
for a purchase), cut to length, faded out, turned to mono 44.1 kHz 16-bit and brought to the same
loudness (a jingle and a handful of coins should not differ by 20 dB as their peaks would leave them), so
the game's own mix (SoundCues.Volume) decides what sits under what. The musical cues all come from the
pizzicato set, so the table sounds like one instrument; only the win gets the bigger steel-drum phrase.

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


def pizzi(n, at=0.0, db=0.0):
    return layer(JINGLE, f"jingles_PIZZI{n:02d}.ogg", at, db)


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


def build(layers, longest, fade):
    parts = []
    for pack, name, at, db in layers:
        parts.append((int(at * RATE), trim_silence(decode(source(pack, name))) * 10 ** (db / 20)))
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
