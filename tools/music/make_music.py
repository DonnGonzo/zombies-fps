"""
Renders the game's music from scratch (no samples, so it's ours to ship): the main menu theme, the quiet
in-match ambience and the epic easter egg track. Each is a seamless loop written as OGG Vorbis into
Assets/_Project/Resources/Audio/Music.

    python3 tools/music/make_music.py            (needs numpy, scipy and ffmpeg)
"""
import os
import subprocess
import sys
import tempfile

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio", "Music")
rng = np.random.default_rng(7)

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def hz(name):
    """'D3' -> frequency."""
    n, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((NOTE[n] + 12 * (octave + 1) - 69) / 12)


def env(n, a, d, s, r, sustain_len):
    """ADSR in samples: attack, decay, sustain level, release; sustain_len is how long the note is held."""
    a, d, r = max(1, int(a * SR)), max(1, int(d * SR)), max(1, int(r * SR))
    hold = max(0, int(sustain_len * SR) - a - d)
    e = np.concatenate([np.linspace(0, 1, a), np.linspace(1, s, d), np.full(hold, s), np.linspace(s, 0, r)])
    if len(e) < n:
        e = np.pad(e, (0, n - len(e)))
    return e[:n]


def saw(f, t, detune=0.0):
    ph = (f * (1 + detune)) * t
    return 2 * (ph - np.floor(ph + 0.5))


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff / (SR / 2), 0.99), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, cutoff / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi / (SR / 2), 0.99)], "band")
    return signal.lfilter(b, a, x)


class Track:
    def __init__(self, seconds):
        self.n = int(seconds * SR)
        # Room for tails, folded back onto the start so the loop is seamless.
        self.buf = np.zeros((self.n + 6 * SR, 2))

    def add(self, start, mono, pan=0.0, gain=1.0):
        i = int(start * SR)
        if i >= self.n:
            return
        mono = mono[: len(self.buf) - i]
        l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
        self.buf[i:i + len(mono), 0] += mono * gain * l * 1.41
        self.buf[i:i + len(mono), 1] += mono * gain * r * 1.41

    def reverb(self, mix=0.25, length=2.8, seed=3):
        """A noise-burst impulse response: big, dark hall."""
        g = np.random.default_rng(seed)
        m = int(length * SR)
        t = np.arange(m) / SR
        out = np.zeros_like(self.buf)
        for ch in range(2):
            ir = g.standard_normal(m) * np.exp(-t * 6.9 / length)
            ir = lowpass(ir, 4200)
            ir /= np.sqrt(np.sum(ir ** 2))
            out[:, ch] = signal.fftconvolve(self.buf[:, ch], ir)[: len(self.buf)]
        self.buf = self.buf * (1 - mix) + out * mix * 1.6

    def finish(self, name, peak=0.89):
        loop = self.buf[: self.n].copy()
        tail = self.buf[self.n:]
        loop[: len(tail)] += tail[: self.n]
        # Gentle glue: soft clip, then normalise.
        loop = np.tanh(loop * 1.1) / np.tanh(1.1)
        loop *= peak / max(1e-6, np.max(np.abs(loop)))
        os.makedirs(OUT, exist_ok=True)
        with tempfile.TemporaryDirectory() as tmp:
            wav = os.path.join(tmp, name + ".wav")
            wavfile.write(wav, SR, (loop * 32767).astype(np.int16))
            dst = os.path.join(OUT, name + ".ogg")
            subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "4", dst], check=True)
        print(f"{name}: {self.n / SR:.1f} s -> {dst}")


# ---------------------------------------------------------------- instruments

def pad(freqs, length, bright=900.0, a=1.2, r=2.0):
    """Detuned saw chord through a slow filter: strings or choir-ish."""
    n = int((length + r) * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f in freqs:
        for d in (-0.006, 0.0, 0.007):
            x += saw(f, t + rng.random(), d)
    x /= max(1, len(freqs) * 3)
    sweep = bright * (1 + 0.3 * np.sin(2 * np.pi * 0.15 * t))
    x = lowpass(x, float(np.mean(sweep)), 2)
    return x * env(n, a, 0.5, 0.85, r, length)


def choir(freqs, length, a=0.9, r=1.6):
    """Saw chord through two vowel formants ('ah'), with vibrato."""
    n = int((length + r) * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * t)
    for f in freqs:
        for d in (-0.005, 0.004):
            ph = np.cumsum(f * (1 + d) * vib) / SR
            x += 2 * (ph - np.floor(ph + 0.5))
    x /= max(1, len(freqs) * 2)
    y = bandpass(x, 600, 1100) * 1.0 + bandpass(x, 1000, 1400) * 0.6 + bandpass(x, 2300, 2900) * 0.25
    return y * env(n, a, 0.4, 0.9, r, length) * 2.2


def bell(f, length=4.0, gain=1.0):
    n = int(length * SR)
    t = np.arange(n) / SR
    x = (np.sin(2 * np.pi * f * t) * np.exp(-t * 1.2) + 0.5 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 2.5)
         + 0.25 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 4.0) + 0.15 * np.sin(2 * np.pi * f * 8.9 * t) * np.exp(-t * 7))
    return x * np.minimum(1, t * 400) * gain


def musicbox(f, length=1.6):
    n = int(length * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t * 8)
    return x * np.exp(-t * 2.6) * np.minimum(1, t * 800)


def bass(f, length, cutoff=500.0, punch=True):
    n = int((length + 0.08) * SR)
    t = np.arange(n) / SR
    x = saw(f, t) * 0.6 + np.sin(2 * np.pi * f * t) * 0.8 + np.sin(2 * np.pi * f / 2 * t) * 0.4
    x = lowpass(x, cutoff * (1.6 if punch else 1.0), 2)
    return x * env(n, 0.004, 0.12, 0.65, 0.06, length)


def ostinato(f, length, cutoff=2400.0):
    """Short, bright string stab for driving sixteenths."""
    n = int((length + 0.06) * SR)
    t = np.arange(n) / SR
    x = saw(f, t, -0.004) + saw(f, t, 0.005)
    x = lowpass(x, cutoff, 2)
    return x * env(n, 0.003, 0.05, 0.5, 0.05, length) * 0.5


def brass(freqs, length):
    n = int((length + 0.4) * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f in freqs:
        x += saw(f, t, -0.003) + saw(f, t, 0.003)
    x /= max(1, len(freqs) * 2)
    # The filter opens with the swell: a brassy 'blat'.
    e = env(n, 0.06, 0.25, 0.7, 0.35, length)
    lo = lowpass(x, 700, 2)
    hi = lowpass(x, 3200, 2)
    return (lo * (1 - e) + hi * e) * e * 1.2


def kick(gain=1.0):
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    f = 42 + 110 * np.exp(-t * 28)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 6.5)
    return x * gain


def taiko(gain=1.0, pitch=1.0):
    n = int(1.3 * SR)
    t = np.arange(n) / SR
    f = (70 + 60 * np.exp(-t * 20)) * pitch
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 3.8)
    skin = lowpass(rng.standard_normal(n), 1800) * np.exp(-t * 30) * 0.7
    return (body + skin) * gain


def snare(gain=1.0):
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    noise = bandpass(rng.standard_normal(n), 1500, 9000) * np.exp(-t * 16)
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 25) * 0.6
    return (noise * 0.9 + tone) * gain


def hat(gain=1.0, open_=False):
    n = int((0.3 if open_ else 0.06) * SR)
    t = np.arange(n) / SR
    return highpass(rng.standard_normal(n), 7000, 2) * np.exp(-t * (12 if open_ else 70)) * gain


def swell(length=2.0, gain=1.0):
    """A reversed cymbal: noise rising into the downbeat."""
    n = int(length * SR)
    t = np.arange(n) / SR
    x = highpass(rng.standard_normal(n), 4000, 2) * (t / length) ** 3
    return x * gain


def drone(f, length, gain=1.0):
    n = int(length * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * f * t) + 0.5 * np.sin(2 * np.pi * f * 1.5 * t + 0.4 * np.sin(2 * np.pi * 0.07 * t)) + 0.3 * np.sin(2 * np.pi * f * 2 * t)
    return x * gain


# ---------------------------------------------------------------- pieces

def chord(root, kind="m", octave=3):
    r = hz(root + str(octave))
    steps = {"m": (0, 3, 7, 12), "M": (0, 4, 7, 12), "sus": (0, 5, 7, 12)}[kind]
    return [r * 2 ** (s / 12) for s in steps]


def menu_theme():
    """Slow and uneasy: a music-box tune over strings and a low drone, a toll every phrase. 72 bpm, 16 bars."""
    bpm = 72
    beat = 60 / bpm
    bars = 16
    tr = Track(bars * 4 * beat)
    prog = [("D", "m"), ("Bb", "M"), ("G", "m"), ("A", "M")] * 4
    for i, (root, kind) in enumerate(prog):
        start = i * 4 * beat
        octave = 2 if root in ("Bb", "A", "G") else 3
        tr.add(start, pad(chord(root, kind, octave), 4 * beat, bright=700, a=1.8, r=2.5), gain=0.32)
        tr.add(start, drone(hz("D2"), 4 * beat + 0.02) * env(int((4 * beat + 0.02) * SR), 0.3, 0.1, 1, 0.3, 4 * beat - 0.3), gain=0.12)
        if i % 4 == 0:
            tr.add(start, bell(hz("D4"), 6.0), pan=-0.3, gain=0.22)
    melody = ["A5", "F5", "E5", "D5", "-", "A4", "D5", "E5", "F5", "-", "E5", "D5", "C#5", "-", "-", "-",
              "D5", "F5", "A5", "Bb5", "A5", "-", "G5", "F5", "E5", "-", "D5", "E5", "C#5", "-", "-", "-"]
    # The tune comes in on the second half, so the theme opens on strings alone.
    for j, note in enumerate(melody):
        if note == "-":
            continue
        tr.add((32 + j) * beat, musicbox(hz(note)), pan=0.25 * np.sin(j), gain=0.3)
        if j % 2 == 0:
            tr.add((32 + j + 0.5) * beat, musicbox(hz(note[:-1] + str(int(note[-1]) - 1)), 2.0), pan=-0.2, gain=0.12)
    tr.reverb(mix=0.35, length=3.5)
    tr.finish("menu_theme")


def ambience():
    """Under a match: a low, breathing bed with distant tolls and the odd high string. 96 s."""
    length = 96.0
    tr = Track(length)
    tr.add(0, drone(hz("D1") * 2, length + 0.01) * (0.6 + 0.4 * np.sin(2 * np.pi * np.arange(int((length + 0.01) * SR)) / SR / 24)), gain=0.16)
    chords = [chord("D", "m", 2), chord("Bb", "M", 1), chord("G", "m", 2), chord("A", "sus", 2)]
    for i in range(8):
        tr.add(i * 12.0, pad(chords[i % 4], 11.0, bright=420, a=4.0, r=5.0), gain=0.3)
    for t0, note in [(5.0, "D3"), (29.0, "A2"), (53.0, "F3"), (77.0, "D3")]:
        tr.add(t0, bell(hz(note), 8.0), pan=rng.uniform(-0.6, 0.6), gain=0.18)
    for t0, note in [(18.0, "A5"), (42.0, "F5"), (66.0, "E5"), (88.0, "D5")]:
        n = int(7 * SR)
        t = np.arange(n) / SR
        s = np.sin(2 * np.pi * hz(note) * t * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))) * env(n, 2.5, 1.0, 0.7, 3, 3.5)
        tr.add(t0, s, pan=rng.uniform(-0.7, 0.7), gain=0.05)
    tr.reverb(mix=0.45, length=5.0)
    tr.finish("ambience", peak=0.6)


def easter_egg():
    """The Lantern trial: 140 bpm, D minor, taiko and kit, driving bass and strings, choir and brass. 32 bars."""
    bpm = 140
    beat = 60 / bpm
    s16 = beat / 4
    bars = 32
    tr = Track(bars * 4 * beat)
    progA = [("D", "m"), ("Bb", "M"), ("F", "M"), ("C", "M")]
    progB = [("D", "m"), ("Bb", "M"), ("G", "m"), ("A", "M")]
    roots = {"D": "D2", "Bb": "Bb1", "F": "F2", "C": "C2", "G": "G1", "A": "A1"}
    for bar in range(bars):
        start = bar * 4 * beat
        section = bar // 8          # 0 intro-ish, 1 full, 2 breakdown, 3 climax
        prog = progA if section in (0, 2) else progB
        root, kind = prog[bar % 4] if section != 2 else prog[(bar // 2) % 4]
        f_root = hz(roots[root])
        # Bass: driving eighths, octave jumps on the off-beats.
        if section != 2 or bar % 2 == 1:
            for e in range(8):
                f = f_root * (2 if e % 4 == 3 else 1)
                tr.add(start + e * 2 * s16, bass(f, 2 * s16 * 0.9, cutoff=420 + 200 * (section == 3)), gain=0.5)
        # Strings: sixteenth ostinato on the chord's tones.
        tones = chord(root, kind, 4 if root in ("D", "C") else 3)
        pattern = [0, 2, 1, 2, 0, 2, 1, 3, 0, 2, 1, 2, 0, 3, 2, 1]
        if section in (1, 3) or (section == 0 and bar >= 2):
            for k in range(16):
                tr.add(start + k * s16, ostinato(tones[pattern[k]], s16 * 0.8, cutoff=1800 + 1200 * (section == 3)), pan=0.35, gain=0.34)
        # Choir: a held chord per bar (two in the breakdown).
        if section != 0 or bar >= 4:
            tr.add(start, choir(chord(root, kind, 3), 4 * beat * 0.98), gain=0.7 if section != 2 else 0.85)
        # Brass: stabs on the 'and' of 2 and on 4, swelling chords in the climax.
        if section == 1 and bar % 2 == 1:
            tr.add(start + 1.5 * beat, brass(chord(root, kind, 3)[:3], 0.35), pan=-0.3, gain=0.45)
            tr.add(start + 3 * beat, brass(chord(root, kind, 3)[:3], 0.7), pan=-0.3, gain=0.45)
        if section == 3:
            tr.add(start, brass(chord(root, kind, 3)[:3], 3.6 * beat), pan=-0.25, gain=0.42)
            tr.add(start, brass([f * 0.5 for f in chord(root, kind, 3)[:2]], 3.6 * beat), pan=0.25, gain=0.3)
        # Drums.
        if section == 2:
            for b in range(4):
                if b in (0, 3) or bar % 2 == 1:
                    tr.add(start + b * beat, taiko(0.9, 0.9 + 0.1 * (b % 2)), gain=0.7)
        elif section == 0 and bar < 4:
            # The opening: just the war drums.
            for b in range(4):
                if b in (0, 2) or (b == 3 and bar % 2 == 1):
                    tr.add(start + b * beat, taiko(0.9, 0.95 + 0.05 * b), gain=0.55)
        else:
            for b in range(4):
                tr.add(start + b * beat, kick(1.0), gain=0.5)
                if b in (1, 3):
                    tr.add(start + b * beat, snare(1.0), gain=0.2)
            for k in range(8):
                tr.add(start + k * 2 * s16, hat(0.6 if k % 2 else 0.35, open_=(k == 7)), pan=-0.4, gain=0.07)
            # Taiko hits on the big beats, a fill every fourth bar.
            tr.add(start, taiko(1.0), gain=0.5)
            if bar % 4 == 3:
                for k in range(4):
                    tr.add(start + 3 * beat + k * s16, taiko(0.6 + 0.1 * k, 1.2 + 0.1 * k), pan=0.2 * (k - 1.5), gain=0.4)
        if bar % 8 == 7:
            tr.add(start + 2 * beat, swell(2 * beat), gain=0.25)
            tr.add(start + 4 * beat, bell(hz("D3"), 4.0), gain=0.25)
    # The theme on top in the climax: a high, defiant line.
    lead = ["D5", "-", "A4", "D5", "F5", "-", "E5", "D5", "Bb4", "-", "-", "C5", "D5", "-", "E5", "-",
            "D5", "-", "A4", "D5", "F5", "-", "G5", "A5", "Bb5", "-", "A5", "G5", "A5", "-", "-", "-"]
    for j, note in enumerate(lead):
        if note == "-":
            continue
        held = 2 if j + 1 < len(lead) and lead[j + 1] == "-" else 1
        tr.add((24 * 4 + j) * beat, brass([hz(note)], held * beat * 0.95), pan=0.1, gain=0.5)
    tr.reverb(mix=0.22, length=2.2)
    tr.finish("easter_egg", peak=0.92)


if __name__ == "__main__":
    which = sys.argv[1:] or ["menu", "ambience", "egg"]
    if "menu" in which:
        menu_theme()
    if "ambience" in which:
        ambience()
    if "egg" in which:
        easter_egg()
