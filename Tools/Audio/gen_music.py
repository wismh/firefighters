"""Procedural music: two seamless loops for the city.

  music_chase.wav  - 132 BPM slapstick fire-brigade march: tuba oom-pah, brass lead, snare rolls, a funk "chase" B part.
  music_dusk.wav   -  88 BPM evening stroll: electric piano, soft brushes, round bass, a lazy whistle tune.

Run: python Tools/Audio/gen_music.py -> Assets/_Project/Audio/Generated/Music/*.wav (44.1 kHz, 16-bit stereo).
Loops are seamless: everything that rings past the end (notes, reverb) is wrapped back onto the start.
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Audio", "Generated", "Music")

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
        "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name, octave):
    return 12 * (octave + 1) + NOTE[name]


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


CHORD_SHAPES = {"": (0, 4, 7), "m": (0, 3, 7), "7": (0, 4, 7, 10), "m7": (0, 3, 7, 10), "maj7": (0, 4, 7, 11),
                "dim": (0, 3, 6), "6": (0, 4, 7, 9), "9": (0, 4, 7, 10, 14)}


def chord(symbol):
    root = symbol[:2] if len(symbol) > 1 and symbol[1] in "b#" else symbol[:1]
    return NOTE[root], CHORD_SHAPES[symbol[len(root):]]


class Track:
    def __init__(self, seconds, rng_seed):
        self.n = int(SR * seconds)
        self.tail = int(SR * 3)
        self.buses = {}
        self.rng = np.random.default_rng(rng_seed)

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = np.zeros(self.n + self.tail)
        return self.buses[name]

    def add(self, name, start_s, x):
        b = self.bus(name)
        s = int(start_s * SR)
        end = min(len(b), s + len(x))
        if end > s:
            b[s:end] += x[: end - s]

    def wrapped(self, name):
        b = self.bus(name).copy()
        b[: self.tail] += b[self.n:]
        return b[: self.n]


# ------------------------------------------------------------------ instruments

def adsr(n, a, d, s, r, hold_n=None):
    a, d, r = max(1, int(a * SR)), max(1, int(d * SR)), max(1, int(r * SR))
    hold_n = n - r if hold_n is None else hold_n
    e = np.zeros(n)
    idx = np.arange(n)
    e = np.where(idx < a, idx / a, e)
    dec = (idx >= a) & (idx < a + d)
    e = np.where(dec, 1 - (1 - s) * (idx - a) / d, e)
    e = np.where((idx >= a + d) & (idx < hold_n), s, e)
    rel = idx >= hold_n
    level = s if hold_n >= a + d else 1.0
    e = np.where(rel, level * np.clip(1 - (idx - hold_n) / r, 0, 1), e)
    return e


def lp(x, hz_cut, order=2):
    return signal.sosfilt(signal.butter(order, min(hz_cut, SR / 2 - 200), fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def hp(x, hz_cut, order=2):
    return signal.sosfilt(signal.butter(order, hz_cut, btype="high", fs=SR, output="sos"), x)


def saw(phase):
    return 2 * ((phase / (2 * np.pi)) % 1) - 1


def tuba(f, dur):
    n = int(SR * (dur + 0.15))
    t = np.arange(n) / SR
    ph = 2 * np.pi * f * t
    x = np.sin(ph) + 0.55 * np.sin(2 * ph) + 0.3 * np.sin(3 * ph) + 0.12 * np.sin(4 * ph)
    blat = 1 + 0.6 * np.exp(-t * 30)
    return np.tanh(x * blat * 0.9) * adsr(n, 0.025, 0.08, 0.75, 0.12, int(SR * dur))


def brass(f, dur, rng, bright=1.0):
    n = int(SR * (dur + 0.2))
    t = np.arange(n) / SR
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.6 * t) * np.clip((t - 0.15) * 4, 0, 1)
    x = np.zeros(n)
    for det in (-0.004, 0.0, 0.005):
        x += saw(np.cumsum(2 * np.pi * f * (1 + det) * vib / SR))
    env = adsr(n, 0.03, 0.1, 0.8, 0.12, int(SR * dur))
    # Brass opens up as it gets louder: cutoff follows the envelope.
    out = np.zeros(n)
    hop = 512
    zi = None
    sos_cache = {}
    for s in range(0, n, hop):
        cut = int(400 + 3200 * bright * env[s] ** 1.5) // 100 * 100 + 200
        sos = sos_cache.setdefault(cut, signal.butter(2, cut, fs=SR, output="sos"))
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        out[s:s + hop], zi = signal.sosfilt(sos, x[s:s + hop], zi=zi)
    return out * env * 0.35


def stab(freqs, dur):
    n = int(SR * (dur + 0.1))
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f in freqs:
        ph = 2 * np.pi * f * t
        x += np.sign(np.sin(ph)) * 0.3 + saw(ph) * 0.3
    return lp(x, 2400) * adsr(n, 0.004, 0.08, 0.35, 0.06, int(SR * dur)) * 0.3


def epiano(freqs, dur, vel=1.0):
    n = int(SR * (dur + 1.2))
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f in freqs:
        mod = np.sin(2 * np.pi * f * 2 * t) * 1.6 * np.exp(-t * 6) * vel
        x += np.sin(2 * np.pi * f * t + mod) * np.exp(-t * 1.4)
        x += 0.15 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t * 9)
    x *= adsr(n, 0.004, 0.2, 0.85, 0.35, int(SR * dur))
    trem = 1 + 0.12 * np.sin(2 * np.pi * 4.5 * t)
    return x * trem * 0.18 * vel


def round_bass(f, dur):
    n = int(SR * (dur + 0.1))
    t = np.arange(n) / SR
    ph = 2 * np.pi * f * t
    x = np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.1 * np.tanh(3 * np.sin(ph))
    return x * adsr(n, 0.008, 0.25, 0.6, 0.08, int(SR * dur)) * 0.55


def funk_bass(f, dur):
    n = int(SR * (dur + 0.06))
    t = np.arange(n) / SR
    ph = 2 * np.pi * f * t
    x = saw(ph) + 0.5 * np.sign(np.sin(ph))
    x = lp(x, 900) * (1 + 1.5 * np.exp(-t * 25))
    return np.tanh(x) * adsr(n, 0.003, 0.06, 0.6, 0.04, int(SR * dur)) * 0.5


def whistle(f, dur, rng):
    n = int(SR * (dur + 0.15))
    t = np.arange(n) / SR
    vib = 1 + 0.008 * np.sin(2 * np.pi * 5.0 * t) * np.clip(t * 3, 0, 1)
    x = np.sin(np.cumsum(2 * np.pi * f * vib / SR))
    breath = bp(rng.standard_normal(n), f * 0.8, min(f * 1.4, 15000)) * 0.08
    return (x + breath) * adsr(n, 0.04, 0.1, 0.8, 0.1, int(SR * dur)) * 0.22


def kick(rng, soft=False):
    n = int(SR * 0.35)
    t = np.arange(n) / SR
    f = 48 + 110 * np.exp(-t * (28 if soft else 40))
    x = np.sin(np.cumsum(2 * np.pi * f / SR)) * np.exp(-t * (9 if soft else 12))
    click = rng.standard_normal(n) * np.exp(-t * 400) * (0.05 if soft else 0.2)
    return (x + click) * (0.6 if soft else 0.9)


def snare(rng, vel=1.0):
    n = int(SR * 0.25)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 30)
    rattle = bp(rng.standard_normal(n), 1500, 9000) * np.exp(-t * 18)
    return (body * 0.6 + rattle * 0.5) * vel * 0.5


def brush(rng, vel=1.0):
    n = int(SR * 0.3)
    t = np.arange(n) / SR
    return bp(rng.standard_normal(n), 2000, 9000) * np.exp(-t * 14) * vel * 0.06


def hat(rng, open_=False, vel=1.0):
    n = int(SR * (0.25 if open_ else 0.06))
    t = np.arange(n) / SR
    return hp(rng.standard_normal(n), 7000, 3) * np.exp(-t * (12 if open_ else 70)) * vel * 0.11


def cymbal(rng):
    n = int(SR * 2.2)
    t = np.arange(n) / SR
    return hp(rng.standard_normal(n), 4500, 2) * np.exp(-t * 1.8) * 0.1


def slide_whistle(f0, f1, dur):
    n = int(SR * dur)
    t = np.arange(n) / SR
    f = f0 * (f1 / f0) ** (t / dur)
    return np.sin(np.cumsum(2 * np.pi * f / SR)) * adsr(n, 0.02, 0.05, 0.9, 0.08) * 0.18


# ------------------------------------------------------------------ mixing

def reverb(x, seconds=1.6, mix=0.18):
    """Schroeder: four combs in parallel, two allpasses in series."""
    out = np.zeros_like(x)
    for delay_ms, g in ((29.7, 0.0), (37.1, 0.0), (41.1, 0.0), (43.7, 0.0)):
        d = int(SR * delay_ms / 1000)
        g = 10 ** (-3 * d / (SR * seconds))
        a = np.zeros(d + 1)
        a[0], a[d] = 1, -g
        out += signal.lfilter([1], a, x)
    for delay_ms in (5.0, 1.7):
        d = int(SR * delay_ms / 1000)
        b = np.zeros(d + 1)
        a = np.zeros(d + 1)
        b[0], b[d] = -0.7, 1
        a[0], a[d] = 1, -0.7
        out = signal.lfilter(b, a, out)
    out = lp(out, 6000)
    return x + mix * out / 4


def wrap_tail(x, n):
    y = x[:n].copy()
    rest = x[n:]
    while len(rest):
        k = min(n, len(rest))
        y[:k] += rest[:k]
        rest = rest[k:]
    return y


def master(track, pans, wet):
    n = track.n
    left = np.zeros(n + track.tail)
    right = np.zeros(n + track.tail)
    send = np.zeros(n + track.tail)
    for name, b in track.buses.items():
        p = pans.get(name, 0.0)
        gl, gr = np.cos((p + 1) * np.pi / 4), np.sin((p + 1) * np.pi / 4)
        left += b * gl
        right += b * gr
        send += b * wet.get(name, 0.0)
    # Run the reverb over two loop lengths so its state is warmed up, then keep the second pass: seamless.
    send_loop = wrap_tail(send, n)
    wet_sig = reverb(np.concatenate([send_loop, send_loop]), 1.8, 1.0)[n:] - np.concatenate([send_loop])
    left = wrap_tail(left, n) + wet_sig * 0.9
    right = wrap_tail(right, n) + np.roll(wet_sig, int(SR * 0.011)) * 0.9
    st = np.stack([left, right], axis=1)
    st = np.tanh(st * 1.4 / np.abs(st).max())
    st = st / np.abs(st).max() * 0.89
    return st


def save(name, st):
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (st * 32767).astype(np.int16))
    print(f"{name}: {len(st) / SR:.1f}s")


# ------------------------------------------------------------------ melody

def scale_notes(key_root, low, high, minor=False):
    steps = (0, 2, 3, 5, 7, 8, 10) if minor else (0, 2, 4, 5, 7, 9, 11)
    return [m for m in range(low, high + 1) if (m - key_root) % 12 in steps]


def make_phrase(rng, chords, key_root, low, high, beats_per_bar, rhythm_bank):
    """A melody over the given chords: chord tones on strong beats, steps between, rhythms from a bank."""
    scale = scale_notes(key_root, low, high)
    notes = []
    cur = scale[len(scale) // 2]
    for bar, sym in enumerate(chords):
        root, shape = chord(sym)
        tones = [m for m in scale if (m - root) % 12 in [(s % 12) for s in shape]] or scale
        rhythm = rhythm_bank[rng.integers(len(rhythm_bank))]
        t = 0.0
        for i, dur in enumerate(rhythm):
            if dur < 0:
                t += -dur
                continue
            strong = abs(t - round(t)) < 1e-6 and int(round(t)) % 2 == 0
            pool = tones if strong or i == 0 else scale
            # Prefer small moves; occasionally leap.
            cand = sorted(pool, key=lambda m: abs(m - cur) + rng.random() * (6 if rng.random() < 0.2 else 2))
            cur = cand[0] if cand[0] != cur or rng.random() < 0.3 else cand[1]
            notes.append((bar * beats_per_bar + t, dur, cur))
            t += dur
    return notes


# ------------------------------------------------------------------ chase theme

def chase():
    bpm = 132
    beat = 60 / bpm
    bars = 32
    tr = Track(bars * 4 * beat, 11)
    rng = tr.rng
    key = NOTE["Bb"]

    a_part = ["Bb", "Bb", "Eb", "Bb", "Bb", "Gm", "C7", "F7"]
    a_end = ["Bb", "Bb", "Eb", "Ebm", "Bb", "G7", "C7", "F7"]
    b_part = ["Gm", "Gm", "Eb", "F", "Gm", "Gm", "Eb", "D7"]
    b_end = ["Cm7", "F7", "Bb", "G7", "Cm7", "F7", "Bb", "F7"]
    form = [("A", a_part), ("A2", a_end), ("B", b_part), ("B2", b_end)]

    march = [[1, 0.5, 0.5, 1, 1], [1.5, 0.5, 1, 1], [0.5, 0.5, 0.5, 0.5, 2], [1, 1, 1.5, 0.5], [2, 1, 1]]
    chase_r = [[0.5, 0.5, -0.5, 0.5, 1, 1], [0.75, 0.75, 0.5, 2], [0.5] * 8, [1, 0.5, 0.5, -1, 1]]
    motif_a = make_phrase(rng, a_part[:4], key, midi("F", 4), midi("F", 5), 4, march)
    motif_b = make_phrase(rng, b_part[:4], key, midi("D", 4), midi("G", 5), 4, chase_r)

    bar_i = 0
    for section, chords in form:
        chase_part = section.startswith("B")
        for local, sym in enumerate(chords):
            t0 = bar_i * 4 * beat
            root, shape = chord(sym)
            r_low = midi("C", 2) + root if root < 5 else midi("C", 1) + root
            fifth = r_low + 7
            tones = [midi("C", 4) + root + s for s in shape]
            if not chase_part:
                # Oom-pah: tuba on 1 and 3, brass stabs on 2 and 4.
                tr.add("tuba", t0, tuba(hz(r_low), beat * 0.8))
                tr.add("tuba", t0 + 2 * beat, tuba(hz(fifth if local % 2 == 0 else r_low + 12 - 5), beat * 0.8))
                for b in (1, 3):
                    tr.add("stab", t0 + b * beat, stab([hz(m) for m in tones], beat * 0.35))
                for b in range(4):
                    tr.add("drums", t0 + b * beat, kick(rng) if b % 2 == 0 else snare(rng, 0.8))
                    tr.add("hats", t0 + (b + 0.5) * beat, hat(rng, vel=0.6))
            else:
                # Chase: running 8th bass, backbeat, offbeat open hats.
                walk = [r_low, r_low + 12, fifth, r_low + 12, r_low, fifth, r_low + 10, r_low + 12]
                for k, m in enumerate(walk):
                    tr.add("bass", t0 + k * 0.5 * beat, funk_bass(hz(m), beat * 0.42))
                for b in range(4):
                    if b in (0, 2):
                        tr.add("drums", t0 + b * beat, kick(rng))
                    else:
                        tr.add("drums", t0 + b * beat, snare(rng, 1.0))
                    tr.add("hats", t0 + b * beat, hat(rng, vel=0.5))
                    tr.add("hats", t0 + (b + 0.5) * beat, hat(rng, open_=b % 2 == 1, vel=0.7))
                if local % 2 == 0:
                    tr.add("stab", t0 + 1.5 * beat, stab([hz(m) for m in tones], beat * 0.25))
                    tr.add("stab", t0 + 3.5 * beat, stab([hz(m) for m in tones], beat * 0.25))
            bar_i += 1

        # Lead: the 4-bar motif twice per section, transposed to the section's chords on the answer.
        sec_start = (bar_i - 8) * 4 * beat
        motif = motif_b if chase_part else motif_a
        for rep in range(2):
            shift = 0 if rep == 0 else (5 if section in ("A2",) else 0)
            for (t, d, m) in motif:
                if rep == 1 and t >= 12:
                    continue
                tr.add("lead", sec_start + (rep * 16 + t) * beat, brass(hz(m + shift), d * beat * 0.92, rng,
                                                                     1.2 if chase_part else 0.9))
        # Cadence: long note on the last bar's root, a drum fill into the next section.
        end_bar = (bar_i - 1) * 4 * beat
        last_root, _ = chord(chords[-1])
        tr.add("lead", end_bar, brass(hz(midi("C", 5) + last_root), 2.5 * beat, rng, 1.1))
        for k in range(6):
            tr.add("drums", end_bar + (2.5 + k * 0.25) * beat, snare(rng, 0.5 + 0.1 * k))
        tr.add("drums", sec_start, cymbal(rng))
        if section == "A2":
            tr.add("fx", end_bar + 2 * beat, slide_whistle(hz(midi("C", 5)), hz(midi("C", 7)), beat * 1.8))
        if section == "B2":
            tr.add("fx", end_bar + 2 * beat, slide_whistle(hz(midi("C", 7)), hz(midi("C", 5)), beat * 1.8))

    st = master(tr, {"tuba": 0.0, "bass": 0.0, "stab": -0.35, "lead": 0.2, "hats": 0.3, "drums": 0.0, "fx": -0.4},
                {"lead": 0.5, "stab": 0.35, "drums": 0.2, "hats": 0.15, "fx": 0.5, "tuba": 0.1, "bass": 0.05})
    save("music_chase", st)


# ------------------------------------------------------------------ dusk theme

def dusk():
    bpm = 88
    beat = 60 / bpm
    bars = 24
    tr = Track(bars * 4 * beat, 23)
    rng = tr.rng
    key = NOTE["F"]
    prog = ["Fmaj7", "Dm7", "Gm7", "C7", "Fmaj7", "Am7", "Bbmaj7", "C7",
            "Dm7", "Am7", "Bbmaj7", "Fmaj7", "Gm7", "Am7", "Bbmaj7", "C7",
            "Fmaj7", "Dm7", "Gm7", "C7", "Am7", "Dm7", "Gm7", "C9"]
    lazy = [[1.5, 0.5, 2], [1, 1, 2], [0.5, 1, 0.5, 2], [-1, 1, 1, 1], [3, 1], [2, -1, 1]]
    tune = make_phrase(rng, prog[:8], key, midi("A", 4), midi("C", 6), 4, lazy)
    tune2 = make_phrase(rng, prog[8:16], key, midi("F", 4), midi("A", 5), 4, lazy)

    for bar, sym in enumerate(prog):
        t0 = bar * 4 * beat
        root, shape = chord(sym)
        voicing = [midi("C", 4) + root + s - (12 if root + s > 9 else 0) for s in shape]
        # Swung comp: chord on 1, a soft re-hit on the "and" of 2.
        tr.add("keys", t0, epiano([hz(m) for m in voicing], beat * 1.6, 0.9))
        tr.add("keys", t0 + 2.66 * beat, epiano([hz(m) for m in voicing], beat * 1.2, 0.6))
        b = midi("C", 2) + root
        line = [(0, b, 1.4), (1.66, b + 7, 0.3), (2, b + 12 if bar % 2 else b + 7, 1.0), (3.33, b + 10, 0.5)]
        for (bt, m, d) in line:
            tr.add("bass", t0 + bt * beat, round_bass(hz(m), d * beat))
        tr.add("drums", t0, kick(rng, soft=True))
        tr.add("drums", t0 + 2.66 * beat, kick(rng, soft=True) * 0.6)
        for k in range(4):
            tr.add("brush", t0 + k * beat, brush(rng, 0.9 if k % 2 else 0.5))
            tr.add("brush", t0 + (k + 0.66) * beat, brush(rng, 0.35))
        if bar % 2 == 1:
            tr.add("drums", t0 + 3 * beat, snare(rng, 0.25))

    for start_bar, phrase in ((0, tune), (8, tune2), (16, tune)):
        if start_bar == 0:
            continue  # the tune enters after an intro of keys and bass
        for (t, d, m) in phrase:
            tr.add("lead", (start_bar * 4 + t) * beat, whistle(hz(m), d * beat * 0.9, rng))

    st = master(tr, {"keys": -0.25, "bass": 0.0, "drums": 0.0, "brush": 0.3, "lead": 0.2},
                {"keys": 0.45, "lead": 0.6, "brush": 0.25, "drums": 0.15})
    save("music_dusk", st)


if __name__ == "__main__":
    chase()
    dusk()
