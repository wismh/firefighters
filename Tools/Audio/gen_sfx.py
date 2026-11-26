"""Procedural sound effects for the city: loops (engine, siren, hose, fire, steam, skid, ambience) and one-shots.

Run: python Tools/Audio/gen_sfx.py  -> writes Assets/_Project/Audio/Generated/Sfx/*.wav (44.1 kHz, 16-bit mono).
Every loop is seamless: built from whole periods or crossfaded end-into-start.
"""
import os
import numpy as np
from scipy.io import wavfile
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Audio", "Generated", "Sfx")
rng = np.random.default_rng(7)


def t_axis(seconds):
    return np.arange(int(SR * seconds)) / SR


def bandpass(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, hz, order=2):
    sos = signal.butter(order, hz, btype="low", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def highpass(x, hz, order=2):
    sos = signal.butter(order, hz, btype="high", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def brown(n):
    x = np.cumsum(rng.standard_normal(n))
    x = highpass(x, 20)
    return x / (np.abs(x).max() + 1e-9)


def white(n):
    return rng.standard_normal(n)


def smooth_noise(n, rate_hz):
    """Slow random control signal in 0..1."""
    points = max(4, int(n / SR * rate_hz) + 2)
    knots = rng.random(points)
    xs = np.linspace(0, n, points)
    return np.interp(np.arange(n), xs, knots)


def loop_crossfade(x, fade_s):
    """Make x loop seamlessly: the tail fades into the head (equal power), result is len(x)-fade long."""
    f = int(SR * fade_s)
    head, body, tail = x[:f], x[f:-f] if f else x, x[-f:]
    k = np.linspace(0, np.pi / 2, f)
    mixed = tail * np.cos(k) + head * np.sin(k)
    return np.concatenate([mixed, body])


def env_adsr(n, a, d, s, r):
    a, d, r = int(a * SR), int(d * SR), int(r * SR)
    sus = max(0, n - a - d - r)
    e = np.concatenate([
        np.linspace(0, 1, a, endpoint=False),
        np.linspace(1, s, d, endpoint=False),
        np.full(sus, s),
        np.linspace(s, 0, r),
    ])
    return np.pad(e, (0, max(0, n - len(e))))[:n]


def decay(n, seconds):
    return np.exp(-np.arange(n) / (SR * seconds))


def save(name, x, peak=0.89):
    x = np.asarray(x, dtype=np.float64)
    if not name.endswith("_loop"):
        # One-shots: no DC, no click at either end.
        x = x - x.mean()
        f_in, f_out = int(SR * 0.002), int(SR * 0.03)
        x[:f_in] *= np.linspace(0, 1, f_in)
        x[-f_out:] *= np.linspace(1, 0, f_out)
    m = np.abs(x).max()
    if m > 0:
        x = x / m * peak
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print(f"{name}: {len(x) / SR:.2f}s")


# ---------------------------------------------------------------- loops

def engine_loop():
    # Smooth low hum: whole-Hz harmonics of a 48 Hz firing rate inside exactly 1 s, so it loops at any pitch.
    # No hard clipping: the old pulse-and-distortion version buzzed like a chainsaw.
    seconds, fire_hz = 1.0, 48
    n = int(SR * seconds)
    t = t_axis(seconds)
    x = np.zeros(n)
    phases = rng.random(16) * 2 * np.pi
    for k in range(1, 15):
        x += np.sin(2 * np.pi * fire_hz * k * t + phases[k]) / k ** 1.5
    # Half-order rumble (one crank turn) and a gentle 4-cylinder lope give it character.
    x += 0.35 * np.sin(2 * np.pi * fire_hz / 2 * t + phases[0])
    x *= 1 + 0.12 * np.sin(2 * np.pi * fire_hz / 4 * t)
    # Periodic noise (built in the frequency domain) for intake breath, under the tone.
    spec = np.zeros(n // 2 + 1, complex)
    band = slice(60, 700)
    spec[band] = np.exp(2j * np.pi * rng.random(700 - 60)) / np.sqrt(np.arange(60, 700))
    breath = np.fft.irfft(spec, n)
    x = x / np.abs(x).max() + 0.12 * breath / np.abs(breath).max()
    x = np.tanh(x * 1.1)
    x = np.fft.irfft(np.fft.rfft(x) * (1 / (1 + (np.fft.rfftfreq(n, 1 / SR) / 700) ** 4)), n)
    save("engine_loop", x)


def siren_loop():
    # Two-tone "nee-naw", soft: mostly sine with a touch of 2nd/3rd harmonic, smooth glide between tones.
    seconds = 2.4
    n = int(SR * seconds)
    t = t_axis(seconds)
    lo, hi = 465.0, 620.0
    # Square-ish switching smoothed into short glides; one full lo/hi cycle per 1.2 s.
    sw = np.tanh(np.sin(2 * np.pi * t / 1.2) * 12) * 0.5 + 0.5
    f = lo + (hi - lo) * sw
    phase = np.cumsum(2 * np.pi * f / SR)
    # Force a whole number of cycles over the loop so the end meets the start.
    phase *= np.round(phase[-1] / (2 * np.pi)) * 2 * np.pi / phase[-1]
    x = np.sin(phase) + 0.18 * np.sin(2 * phase) + 0.06 * np.sin(3 * phase)
    save("siren_loop", x, 0.6)


def hose_loop():
    n = int(SR * 4.5)
    hiss = bandpass(white(n), 1800, 9000, 2)
    body = bandpass(white(n), 250, 1400, 2)
    rumble = lowpass(white(n), 160)
    flutter = 0.85 + 0.15 * smooth_noise(n, 9)
    x = (hiss * 0.55 + body * 0.9 * flutter + rumble * 1.4 * (0.8 + 0.2 * smooth_noise(n, 3)))
    save("hose_loop", loop_crossfade(x, 0.5), 0.8)


def fire_loop():
    n = int(SR * 6.5)
    roar = lowpass(brown(n), 420) * (0.6 + 0.4 * smooth_noise(n, 2.5))
    whoosh = bandpass(white(n), 300, 1500) * 0.25 * smooth_noise(n, 1.5) ** 2
    crack = np.zeros(n)
    for _ in range(int(n / SR * 34)):
        pos = int(rng.integers(0, n - 4000))
        length = int(rng.integers(80, 1400))
        amp = rng.random() ** 2.2
        burst = white(length) * decay(length, length / SR / 4)
        crack[pos:pos + length] += burst * amp
    crack = highpass(crack, 900)
    x = roar * 1.0 + whoosh + crack * 0.9
    save("fire_loop", loop_crossfade(x, 0.5), 0.85)


def steam_loop():
    n = int(SR * 3.5)
    hiss = highpass(white(n), 2600, 3)
    sizzle = (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 23 * t_axis(n / SR) + 4 * smooth_noise(n, 6))))
    pops = np.zeros(n)
    for _ in range(int(n / SR * 40)):
        pos = int(rng.integers(0, n - 600))
        pops[pos:pos + 300] += white(300) * decay(300, 0.002) * rng.random()
    x = hiss * sizzle * (0.75 + 0.25 * smooth_noise(n, 4)) + highpass(pops, 1500) * 0.6
    save("steam_loop", loop_crossfade(x, 0.4), 0.75)


def skid_loop():
    # Rubber scrub rather than a whistle: band noise with a faint, wobbly tone inside.
    n = int(SR * 2.2)
    t = t_axis(n / SR)
    wobble = 1 + 0.05 * (smooth_noise(n, 10) - 0.5)
    tone = np.sin(np.cumsum(2 * np.pi * 820 * wobble / SR))
    rubber = bandpass(white(n), 500, 2200, 2)
    grit = bandpass(white(n), 150, 500, 2) * 0.6
    x = rubber + grit + 0.35 * tone * (0.5 + 0.5 * smooth_noise(n, 6))
    x *= 0.8 + 0.2 * smooth_noise(n, 12)
    save("skid_loop", loop_crossfade(x, 0.3), 0.6)


def ambience_loop():
    n = int(SR * 16)
    t = t_axis(n / SR)
    traffic = lowpass(brown(n), 260) * (0.7 + 0.3 * smooth_noise(n, 0.4))
    wind = bandpass(white(n), 200, 900) * 0.18 * smooth_noise(n, 0.3)
    # Distant cars passing: noise swells with a little doppler-ish band sweep.
    passes = np.zeros(n)
    for start in (1.5, 6.0, 9.5, 13.0):
        length = int(SR * rng.uniform(2.5, 3.5))
        s = int(SR * start)
        e = np.sin(np.linspace(0, np.pi, length)) ** 2
        car = bandpass(white(length), 120, 700) * e
        passes[s:s + length] += car[: max(0, min(length, n - s))] * rng.uniform(0.25, 0.45)
    # Crickets at dusk: 4.4 kHz chirps in bursts.
    crickets = np.zeros(n)
    chirp_t = 0.0
    while chirp_t < n / SR - 0.5:
        s = int(chirp_t * SR)
        for k in range(3):
            length = int(SR * 0.018)
            ss = s + k * int(SR * 0.032)
            tt = np.arange(length) / SR
            crickets[ss:ss + length] += np.sin(2 * np.pi * 4400 * tt) * np.sin(np.pi * tt / tt[-1])
        chirp_t += rng.uniform(0.45, 1.1)
    x = traffic * 1.0 + wind + passes + crickets * 0.035
    save("ambience_loop", loop_crossfade(x, 1.5), 0.7)


# ---------------------------------------------------------------- one-shots

def footsteps():
    for i in range(6):
        length = int(SR * 0.22)
        tt = np.arange(length) / SR
        f = rng.uniform(70, 95)
        thud = np.sin(2 * np.pi * f * tt * (1 - tt * 1.5)) * np.exp(-tt * 38)
        grit = bandpass(white(length), 1200, 6000) * np.exp(-tt * rng.uniform(55, 80))
        scuff = bandpass(white(length), 400, 1800) * np.exp(-((tt - 0.03) ** 2) / 0.0004) * 0.4
        x = thud * 0.9 + grit * rng.uniform(0.35, 0.55) + scuff
        save(f"footstep_{i + 1}", x, rng.uniform(0.7, 0.85))


def land():
    length = int(SR * 0.45)
    tt = np.arange(length) / SR
    thud = np.sin(2 * np.pi * 55 * tt * (1 - tt)) * np.exp(-tt * 14)
    grit = bandpass(white(length), 800, 5000) * np.exp(-tt * 30)
    save("land", thud + grit * 0.5)


def door(name, slam):
    length = int(SR * 0.6)
    tt = np.arange(length) / SR
    x = np.zeros(length)
    if slam:
        body = np.sin(2 * np.pi * 68 * tt) * np.exp(-tt * 18) + 0.6 * np.sin(2 * np.pi * 143 * tt) * np.exp(-tt * 30)
        rattle = bandpass(white(length), 500, 2500) * np.exp(-tt * 25) * 0.5
        x += body + rattle
    latch_at = 0.0 if slam else 0.05
    ls = int(latch_at * SR)
    ln = int(SR * 0.05)
    latch = bandpass(white(ln), 2000, 8000) * np.exp(-np.arange(ln) / SR * 160)
    latch += 0.5 * np.sin(2 * np.pi * 2600 * np.arange(ln) / SR) * np.exp(-np.arange(ln) / SR * 120)
    x[ls:ls + ln] += latch * (0.6 if slam else 1.0)
    if not slam:
        swing = bandpass(white(length), 300, 1200) * env_adsr(length, 0.12, 0.1, 0.3, 0.3) * 0.25
        x += swing
    save(name, x)


def whoosh():
    length = int(SR * 0.9)
    tt = np.arange(length) / SR
    noise = white(length)
    x = np.zeros(length)
    # Sweep a band upward and back: render in slices.
    hop = 256
    for s in range(0, length - hop, hop):
        p = s / length
        centre = 300 + 2200 * np.sin(np.pi * p)
        seg = noise[max(0, s - 2048):s + hop]
        y = bandpass(seg, centre * 0.6, min(centre * 1.6, SR / 2 - 100))[-hop:]
        x[s:s + hop] = y
    x *= np.sin(np.pi * tt / tt[-1]) ** 1.5
    save("whoosh", x)


def splash():
    length = int(SR * 0.8)
    tt = np.arange(length) / SR
    hit = lowpass(white(length), 3500) * np.exp(-tt * 9)
    spray = highpass(white(length), 3000) * np.exp(-tt * 6) * 0.4
    bubbles = np.zeros(length)
    for _ in range(14):
        s = int(rng.uniform(0.02, 0.45) * SR)
        bl = int(SR * 0.05)
        bt = np.arange(bl) / SR
        f0 = rng.uniform(500, 1400)
        bubbles[s:s + bl] += np.sin(2 * np.pi * f0 * bt * (1 + bt * 8)) * np.exp(-bt * 70) * rng.uniform(0.2, 0.5)
    save("splash", hit + spray + bubbles)


def horn():
    # Short friendly double "beep" instead of a harsh square blast.
    t = t_axis(0.42)
    x = np.zeros_like(t)
    for f in (392, 494):
        phase = 2 * np.pi * f * t
        x += np.sin(phase) + 0.3 * np.sin(2 * phase) + 0.1 * np.sin(3 * phase)
    gate = ((t < 0.16) | ((t > 0.22) & (t < 0.4))).astype(float)
    gate = lowpass(gate, 60)
    save("horn", lowpass(x, 2500) * gate, 0.7)


def boing():
    # Cartoon spring for a car launched off the ground.
    t = t_axis(0.7)
    f = 180 + 260 * (1 - np.exp(-t * 6)) + 40 * np.sin(2 * np.pi * 14 * t) * np.exp(-t * 4)
    x = np.sin(np.cumsum(2 * np.pi * f / SR)) * np.exp(-t * 4.5)
    save("boing", x)


if __name__ == "__main__":
    engine_loop()
    siren_loop()
    hose_loop()
    fire_loop()
    steam_loop()
    skid_loop()
    ambience_loop()
    footsteps()
    land()
    door("door_open", False)
    door("door_close", True)
    whoosh()
    splash()
    horn()
    boing()
