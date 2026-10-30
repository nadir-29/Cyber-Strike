"""Generates Cyber Strike's retro synth sound effects into Assets/Audio/.

Run from the project root:  py Tools/generate_sfx.py

Pure Python (no numpy): simple oscillators, pitch sweeps, envelopes, noise and bitcrush,
written as 22.05 kHz 16-bit mono WAV files.

Outputs (Assets/Audio/)
  punch.wav, kick.wav, hit.wav, deflect.wav, pickup.wav,
  ability_double_damage.wav, ability_healing.wav, ability_steal.wav, ability_ice_strike.wav,
  win.wav, lose.wav
Outputs (Assets/Audio/UI/)
  ui_hover.wav (soft high blip), ui_click.wav (punchier confirm); both quiet, menu-only
Outputs (Assets/Audio/Ambience/)
  neon_buzz.wav (failing neon tube), rain_loop.wav (seamless 6 s rain bed, played quietly on loop)
"""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "Audio"
RATE = 22050


# ---------------------------------------------------------------- building blocks

def square(phase, duty=0.5):
    return 1.0 if (phase % 1.0) < duty else -1.0


def triangle(phase):
    return 4.0 * abs((phase % 1.0) - 0.5) - 1.0


def saw(phase):
    return 2.0 * (phase % 1.0) - 1.0


def sine(phase):
    return math.sin(2 * math.pi * phase)


def tone(duration, freq, wave_fn=square, volume=0.5, attack=0.005, release=None, curve=1.0):
    """One voice. freq is a number or a function of t (0..1). Envelope: linear attack, power-curve decay."""
    n = int(duration * RATE)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        f = freq(t) if callable(freq) else freq
        phase += f / RATE
        env = min(1.0, i / max(1, attack * RATE))
        if release is None:
            env *= (1.0 - t) ** curve
        else:
            tail = n - int(release * RATE)
            if i > tail:
                env *= 1.0 - (i - tail) / max(1, n - tail)
        out.append(wave_fn(phase) * env * volume)
    return out


def noise(duration, volume=0.5, curve=2.0, rng=None, hold=1):
    """White noise; hold > 1 repeats samples for a crunchier, lower-pitched hiss."""
    rng = rng or random.Random(7)
    n = int(duration * RATE)
    out, value = [], 0.0
    for i in range(n):
        if i % hold == 0:
            value = rng.uniform(-1, 1)
        out.append(value * volume * (1.0 - i / n) ** curve)
    return out


def sweep(f0, f1, shape=1.0):
    """Exponential pitch glide from f0 to f1."""
    return lambda t: f0 * (f1 / f0) ** (t ** shape)


def mix(*tracks, offsets=None):
    offsets = offsets or [0.0] * len(tracks)
    n = max(int(o * RATE) + len(tr) for tr, o in zip(tracks, offsets))
    out = [0.0] * n
    for tr, o in zip(tracks, offsets):
        start = int(o * RATE)
        for i, s in enumerate(tr):
            out[start + i] += s
    return out


def seq(notes, wave_fn=square, volume=0.35, gap=0.0, duty_fn=None):
    """Melody from (frequency, seconds) pairs; frequency 0 is a rest."""
    out = []
    for f, d in notes:
        if f <= 0:
            out += [0.0] * int(d * RATE)
            continue
        fn = (lambda ph, df=duty_fn: square(ph, df)) if duty_fn else wave_fn
        out += tone(d - gap, f, fn, volume, attack=0.004, curve=0.6)
        out += [0.0] * int(gap * RATE)
    return out


def bitcrush(track, steps=16, hold=3):
    out, value = [], 0.0
    for i, s in enumerate(track):
        if i % hold == 0:
            value = round(s * steps) / steps
        out.append(value)
    return out


def echo(track, delay=0.09, feedback=0.35, taps=3):
    out = track + [0.0] * int(delay * RATE * taps)
    d = int(delay * RATE)
    for k in range(1, taps + 1):
        g = feedback ** k
        for i, s in enumerate(track):
            out[i + d * k] += s * g
    return out


def save(name, track, peak=0.85):
    m = max(1e-6, max(abs(s) for s in track))
    scale = peak / m if m > peak else 1.0
    path = OUT / f"{name}.wav"
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in track))


def note(semitones_from_a4):
    return 440.0 * 2 ** (semitones_from_a4 / 12)


# ---------------------------------------------------------------- sounds

def punch():
    """Quick swing whoosh: filtered-sounding noise blip with a falling square chirp."""
    return mix(noise(0.09, 0.45, curve=1.5, hold=2),
               tone(0.07, sweep(900, 300), lambda p: square(p, 0.25), 0.18))


def kick():
    """Heavier, longer whoosh with a low falling tone."""
    return mix(noise(0.16, 0.45, curve=1.2, hold=3),
               tone(0.14, sweep(520, 140), lambda p: square(p, 0.3), 0.22))


def hit():
    """Impact crunch: low thump + noisy crack + short bitcrushed tail."""
    thump = tone(0.16, sweep(180, 45), sine, 0.9, curve=1.5)
    crack = noise(0.08, 0.7, curve=2.5, hold=1)
    body = tone(0.1, sweep(320, 90), lambda p: square(p, 0.4), 0.3)
    return bitcrush(mix(thump, crack, body), steps=24, hold=2)


def deflect():
    """Blocked hit: bright metallic ting on an energy shield, no low thump. Two inharmonic
    partials ring over a tiny click, with a short upward zap and a shimmering echo."""
    click = noise(0.02, 0.5, curve=3.0, hold=1)
    ring = mix(tone(0.22, 1760, triangle, 0.35, curve=2.2),
               tone(0.22, 2490, triangle, 0.22, curve=2.6),
               tone(0.12, 3950, sine, 0.12, curve=3.0))
    zap = tone(0.06, sweep(600, 1500), lambda p: square(p, 0.25), 0.18, curve=1.0)
    return echo(mix(click, ring, zap), delay=0.045, feedback=0.3, taps=2)


def pickup():
    """Bright rising arpeggio."""
    notes = [(note(n), 0.05) for n in (3, 7, 10, 15)] + [(note(22), 0.12)]
    return echo(seq(notes, duty_fn=0.25, volume=0.32), delay=0.07, feedback=0.3, taps=2)


def double_damage():
    """Power surge: rising detuned saws with tremolo, capped by a stab."""
    a = tone(0.45, sweep(110, 440, 0.7), saw, 0.3, release=0.08)
    b = tone(0.45, sweep(112, 452, 0.7), saw, 0.3, release=0.08)
    surge = [s * (0.6 + 0.4 * sine(i / RATE * 18)) for i, s in enumerate(mix(a, b))]
    stab = mix(tone(0.18, note(15), lambda p: square(p, 0.5), 0.3, curve=1.2),
               tone(0.18, note(3), lambda p: square(p, 0.5), 0.25, curve=1.2))
    return mix(surge, stab, offsets=[0.0, 0.42])


def healing():
    """Soft shimmering sine arpeggio upward."""
    notes = [(note(n), 0.07) for n in (0, 4, 7, 12, 16, 19)] + [(note(24), 0.25)]
    sparkle = seq(notes, wave_fn=sine, volume=0.4)
    air = tone(len(sparkle) / RATE, sweep(1800, 3200), triangle, 0.05)
    return echo(mix(sparkle, air), delay=0.08, feedback=0.4, taps=3)


def steal():
    """Glitchy data grab: random pitch blips, bitcrushed, ending in a downward zip."""
    rng = random.Random(42)
    blips = []
    for _ in range(9):
        blips += tone(0.035, rng.choice([300, 600, 900, 1500, 2400]), lambda p: square(p, rng.choice([0.125, 0.5])), 0.3, curve=0.3)
    zip_down = tone(0.16, sweep(2000, 120), saw, 0.35, curve=0.8)
    return bitcrush(blips + zip_down, steps=8, hold=4)


def ice_strike():
    """Frost charge: high triangle cluster with icy noise and a crystalline ping."""
    cluster = mix(*[tone(0.5, note(n), triangle, 0.15, curve=1.4) for n in (24, 27, 31, 36)],
                  offsets=[0.0, 0.03, 0.06, 0.09])
    frost = noise(0.35, 0.12, curve=1.0, hold=1)
    ping = tone(0.3, note(43), sine, 0.25, curve=2.0)
    return echo(mix(cluster, frost, ping, offsets=[0.0, 0.0, 0.08]), delay=0.06, feedback=0.35, taps=3)


def win():
    """Major victory jingle."""
    lead = [(note(3), 0.1), (note(7), 0.1), (note(10), 0.1), (note(15), 0.2), (0, 0.05),
            (note(10), 0.1), (note(15), 0.45)]
    bass = [(note(-21), 0.3), (note(-17), 0.3), (note(-14), 0.5)]
    return echo(mix(seq(lead, duty_fn=0.25, volume=0.35, gap=0.01),
                    seq(bass, wave_fn=triangle, volume=0.45)), delay=0.12, feedback=0.25, taps=2)


def lose():
    """Descending minor jingle that winds down."""
    lead = [(note(7), 0.18), (note(6), 0.18), (note(5), 0.18), (note(3), 0.5)]
    track = seq(lead, duty_fn=0.5, volume=0.3, gap=0.02)
    bass = seq([(note(-17), 0.36), (note(-21), 0.7)], wave_fn=triangle, volume=0.45)
    sag = tone(0.4, sweep(note(-9), note(-24)), triangle, 0.25)
    return mix(track, bass, sag, offsets=[0.0, 0.0, 0.62])


def ui_hover():
    """Soft high blip: a tiny triangle chirp sliding up, very short and quiet."""
    return tone(0.045, sweep(1500, 2100), triangle, 0.22, attack=0.002, curve=1.6)


def ui_click():
    """Confirm: a short click tick then two quick rising square notes."""
    tick = noise(0.012, 0.25, curve=3.0, hold=1)
    notes = seq([(note(10), 0.04), (note(17), 0.08)], duty_fn=0.25, volume=0.28)
    return mix(tick, notes)


def neon_buzz():
    """Electric buzz of a failing neon tube: a 120 Hz hum with harsh harmonics, chopped into
    irregular bursts with a crackle on each re-strike. Short and quiet."""
    rng = random.Random(9)
    n = int(0.42 * RATE)
    out, gate, crackle = [], 1.0, 0.0
    for i in range(n):
        t = i / RATE
        if i % int(0.025 * RATE) == 0:  # re-roll the gate every 25 ms: tube sputtering
            on = rng.random() < 0.7
            if on and gate < 0.5:
                crackle = 1.0
            gate = 1.0 if on else 0.15
        hum = 0.55 * saw(t * 120) + 0.25 * square(t * 240, 0.3) + 0.12 * sine(t * 1800)
        crackle *= 0.995
        s = hum * gate + rng.uniform(-1, 1) * (0.08 + 0.6 * crackle)
        env = min(1.0, i / (0.01 * RATE)) * min(1.0, (n - i) / (0.06 * RATE))
        out.append(s * env * 0.4)
    return bitcrush(out, steps=20, hold=2)


def rain_loop():
    """Seamless 6 s rain bed (ambience, not music): soft hiss from filtered noise with slow
    swells, plus sparse droplet ticks. The tail is crossfaded into the head so it loops cleanly."""
    rng = random.Random(77)
    seconds, fade = 6.0, 0.5
    n, nf = int(seconds * RATE), int(fade * RATE)
    total = n + nf
    lo = hi = 0.0
    out = []
    for i in range(total):
        w = rng.uniform(-1, 1)
        lo += 0.18 * (w - lo)    # one-pole lowpass ~700 Hz: the body of the rain
        hi += 0.6 * (w - hi)     # brighter hiss
        swell = 0.8 + 0.2 * sine(i / RATE / 3.0)  # period 3 s fits the 6 s loop twice
        out.append((lo * 0.9 + (hi - lo) * 0.25) * swell)
    # Droplet ticks: tiny falling sine blips.
    for _ in range(140):
        start = rng.randrange(total - 800)
        f0 = rng.uniform(1800, 4200)
        vol = rng.uniform(0.04, 0.12)
        for k, s in enumerate(tone(0.03, sweep(f0, f0 * 0.6), sine, vol, attack=0.001, curve=3.0)):
            out[start + k] += s
    # Fold the extra tail over the head with an equal-power crossfade.
    loop = out[:n]
    for i in range(nf):
        a = i / nf
        loop[i] = out[n + i] * math.cos(a * math.pi / 2) + out[i] * math.sin(a * math.pi / 2)
    return loop


def main():
    sounds = {
        "Ambience/neon_buzz": neon_buzz, "Ambience/rain_loop": rain_loop,
        "punch": punch, "kick": kick, "hit": hit, "deflect": deflect, "pickup": pickup,
        "ability_double_damage": double_damage, "ability_healing": healing,
        "ability_steal": steal, "ability_ice_strike": ice_strike,
        "win": win, "lose": lose,
        "UI/ui_hover": ui_hover, "UI/ui_click": ui_click,
    }
    for name, make in sounds.items():
        track = make()
        save(name, track)
        print(f"{name}.wav  {len(track) / RATE:.2f}s")


if __name__ == "__main__":
    main()
