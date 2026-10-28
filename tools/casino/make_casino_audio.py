"""
Makes the slot machines' sounds under Assets/_Project/Resources/Audio/Casino:

    slot_music.ogg    a seamless 16 s lounge loop played at a machine while someone is gambling on it (rendered
                      from scratch here: no samples, so it's ours to ship)
    slot_spin.ogg     the reels' ratchet, timed to the HUD (reels stop at 1.30, 1.65 and 2.00 s)
    slot_win.ogg      a spin that pays more than the bet: a rising sax jingle over a pour of chips and payout bells
    slot_jackpot.ogg  three diamonds or sevens: the same, bigger and longer
    slot_lose.ogg     a spin that loses or breaks even: the descending "wah wah" sax, slowed down a little

The jingles and chips come from two CC0 Kenney packs (https://kenney.nl/assets/music-jingles and
https://kenney.nl/assets/casino-audio). Download both zips and unzip each into its own folder:

    python3 tools/casino/make_casino_audio.py <music-jingles folder> <casino-audio folder>

Needs numpy, scipy and ffmpeg. Writes .meta files with fixed guids, so a re-run doesn't churn the repository.
"""
import os
import subprocess
import sys
import tempfile
import uuid

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Resources', 'Audio', 'Casino')
rng = np.random.default_rng(21)

NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6, 'G': 7, 'G#': 8,
        'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}


def hz(name):
    n, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((NOTE[n] + 12 * (octave + 1) - 69) / 12)


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff / (SR / 2), 0.99), 'low')
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, cutoff / (SR / 2), 'high')
    return signal.lfilter(b, a, x)


def decay(n, tau, attack=0.004):
    t = np.arange(n) / SR
    return np.minimum(1, t / attack) * np.exp(-t / tau)


def add(buf, at, x, gain=1.0):
    """Mixes x into buf at time `at`, wrapping round the end (so loops stay seamless)."""
    i = int(at * SR) % len(buf)
    end = min(len(buf), i + len(x))
    buf[i:end] += gain * x[:end - i]
    if i + len(x) > len(buf):
        rest = x[end - i:]
        buf[:len(rest)] += gain * rest


# ---------------------------------------------------------------- instruments

def rhodes(f, length):
    n = int((length + 0.6) * SR)
    t = np.arange(n) / SR
    mod = np.sin(2 * np.pi * f * t) * 1.2 * np.exp(-t / 0.25)
    tone = np.sin(2 * np.pi * f * t + mod) + 0.25 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t / 0.3)
    env = decay(n, 1.4, 0.006)
    hold = int(length * SR)
    env[hold:] *= np.exp(-np.arange(n - hold) / SR / 0.08)
    return tone * env


def vibes(f, length=1.6):
    n = int(length * SR)
    t = np.arange(n) / SR
    trem = 1 + 0.25 * np.sin(2 * np.pi * 5.5 * t)
    tone = np.sin(2 * np.pi * f * t) + 0.18 * np.sin(2 * np.pi * 4 * f * t) * np.exp(-t / 0.08)
    return tone * decay(n, 0.7, 0.002) * trem


def bass(f, length):
    n = int((length + 0.1) * SR)
    t = np.arange(n) / SR
    tone = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t) + 0.08 * np.sin(2 * np.pi * 3 * f * t)
    env = decay(n, 0.9, 0.008)
    hold = int(length * 0.9 * SR)
    env[hold:] *= np.exp(-np.arange(n - hold) / SR / 0.03)
    return lowpass(tone * env, 900)


def brush(length=0.18, tau=0.05):
    n = int(length * SR)
    return highpass(rng.standard_normal(n), 5000) * decay(n, tau, 0.002)


def kick():
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    f = 55 + 60 * np.exp(-t / 0.03)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * decay(n, 0.12)


def bell(f, length=0.9):
    """A bright payout bell: a few inharmonic partials."""
    n = int(length * SR)
    t = np.arange(n) / SR
    tone = sum(a * np.sin(2 * np.pi * f * r * t) * np.exp(-t / (0.5 / r)) for r, a in ((1, 1), (2.76, 0.5), (5.4, 0.25), (8.9, 0.1)))
    return tone * decay(n, 0.6, 0.001)


# ---------------------------------------------------------------- the loop

def music():
    bpm = 116
    beat = 60 / bpm
    bars = 8
    buf = np.zeros(int(bars * 4 * beat * SR))
    # A minor-key lounge turnaround: ii-V into the relative major, then a ii-V back home.
    chords = [
        ('A2', ['C4', 'E4', 'G4', 'A4']), ('D2', ['C4', 'F#4', 'A4', 'D5']),
        ('G2', ['B3', 'D4', 'F#4', 'A4']), ('C2', ['B3', 'E4', 'G4', 'C5']),
        ('F#2', ['A3', 'C4', 'E4', 'F#4']), ('B2', ['A3', 'D#4', 'F#4', 'B4']),
        ('E2', ['G3', 'B3', 'E4', 'G4']), ('E2', ['G#3', 'B3', 'D4', 'F4']),
    ]
    swing = beat * 2 / 3
    for bar, (root, voicing) in enumerate(chords):
        t0 = bar * 4 * beat
        # Walking bass: root, fifth, octave, then a chromatic step into the next bar's root.
        r = hz(root)
        nxt = hz(chords[(bar + 1) % bars][0])
        walk = [r, r * 1.5, r * 2 if bar % 2 else r * 1.335, nxt * (2 ** (1 / 12) if nxt < r else 2 ** (-1 / 12))]
        for k, f in enumerate(walk):
            while f > 130:
                f /= 2
            add(buf, t0 + k * beat, bass(f, beat * 0.92), 0.55)
        # Rhodes comping: a stab on 1 and a push on the "and" of 2.
        for at, ln, g in ((0, beat * 1.4, 0.16), (beat + swing, beat * 1.2, 0.12)):
            for i, name in enumerate(voicing):
                add(buf, t0 + at + i * 0.008, rhodes(hz(name), ln), g)
        # Vibraphone arpeggio up and back down the chord, swung 8ths, with a rest on beat 4.
        arp = voicing + [voicing[2], voicing[1]]
        for k, name in enumerate(arp):
            at = (k // 2) * beat + (swing if k % 2 else 0)
            f = hz(name) * 2
            add(buf, t0 + at, vibes(f), 0.075 if k % 2 else 0.1)
        # Brushes on every swung 8th, soft kick on 1 and 3, and a "ding" of the machine every other bar.
        for k in range(4):
            add(buf, t0 + k * beat, brush(), 0.05 if k % 2 else 0.035)
            add(buf, t0 + k * beat + swing, brush(0.1, 0.025), 0.03)
        add(buf, t0, kick(), 0.35)
        add(buf, t0 + 2 * beat, kick(), 0.25)
        if bar % 2 == 1:
            add(buf, t0 + 3 * beat + swing, bell(hz(voicing[-1]) * 2, 0.7), 0.05)
    # A little room: short slapback, then gentle tape warmth.
    room = np.zeros_like(buf)
    for d, g in ((0.031, 0.25), (0.067, 0.18), (0.113, 0.12), (0.171, 0.08)):
        room += g * np.roll(buf, int(d * SR))
    return lowpass(buf + room, 9000)


# ---------------------------------------------------------------- stings

def load(path, semis=0.0):
    chain = []
    if semis:
        chain = [f'asetrate={SR * 2 ** (semis / 12):.0f}', f'aresample={SR}']
    cmd = ['ffmpeg', '-v', 'error', '-i', path, '-ac', '1', '-ar', str(SR)] + (['-af', ','.join(chain)] if chain else []) + ['-f', 'f32le', '-']
    return np.frombuffer(subprocess.run(cmd, check=True, capture_output=True).stdout, np.float32).astype(np.float64)


def spin():
    total = 2.25
    buf = np.zeros(int(total * SR))
    clicks = []
    t = 0.0
    while t < 2.0:
        clicks.append(t)
        t += 0.045 + 0.02 * (t / 2.0) ** 2   # the ratchet slows as the reels come round
    stops = (1.30, 1.65, 2.00)
    for c in clicks:
        reels = sum(1 for s in stops if c < s)
        if reels == 0:
            continue
        n = int(0.025 * SR)
        click = highpass(rng.standard_normal(n), 2500) * decay(n, 0.005, 0.0005)
        add(buf, c, click, 0.12 * reels / 3 + 0.05)
    for s in stops:
        n = int(0.15 * SR)
        tt = np.arange(n) / SR
        thunk = np.sin(2 * np.pi * 140 * tt) * decay(n, 0.04) + 0.6 * highpass(rng.standard_normal(n), 1500) * decay(n, 0.01)
        add(buf, s, thunk, 0.5)
    return buf


def chips_pour(casino, length, density, seed):
    r = np.random.default_rng(seed)
    names = [f'chips-collide-{i}' for i in range(1, 5)] + [f'chips-stack-{i}' for i in range(1, 7)] + [f'chips-handle-{i}' for i in range(1, 7)]
    clips = [load(os.path.join(casino, 'Audio', n + '.ogg')) for n in names]
    buf = np.zeros(int((length + 1.0) * SR))
    t = 0.05
    while t < length:
        c = clips[r.integers(len(clips))]
        add(buf, t, c, r.uniform(0.25, 0.5) * (1 - 0.5 * t / length))
        t += r.exponential(1 / density)
    return buf


def win(jingles, casino, big):
    sax = load(os.path.join(jingles, 'Audio', 'Sax jingles', 'jingles_SAX02.ogg'))
    fan = load(os.path.join(jingles, 'Audio', '8-Bit jingles', 'jingles_NES12.ogg'))
    length = 3.2 if big else 1.9
    buf = np.zeros(int((length + 0.8) * SR))
    add(buf, 0.0, sax, 1.0)
    add(buf, 0.05, fan, 0.35)
    if big:
        # A second, higher run and then the whole fanfare again a fourth up.
        add(buf, 0.95, load(os.path.join(jingles, 'Audio', 'Sax jingles', 'jingles_SAX10.ogg')), 1.0)
        add(buf, 1.9, load(os.path.join(jingles, 'Audio', 'Sax jingles', 'jingles_SAX02.ogg'), 5), 1.0)
        add(buf, 1.95, load(os.path.join(jingles, 'Audio', '8-Bit jingles', 'jingles_NES12.ogg'), 5), 0.35)
    pour = chips_pour(casino, length - 0.3, 22 if big else 14, 5 if big else 3)
    add(buf, 0.25, pour[:len(buf)], 0.9)
    # Payout bells ringing up the win.
    bells = ['E6', 'G#6', 'B6', 'E7'] if big else ['E6', 'B6']
    steps = 12 if big else 5
    for k in range(steps):
        add(buf, 0.3 + k * (length - 0.6) / steps, bell(hz(bells[k % len(bells)]), 0.5), 0.12)
    return buf


def lose(jingles):
    # Two semitones down, which also plays it about an eighth slower: droopier.
    return load(os.path.join(jingles, 'Audio', 'Sax jingles', 'jingles_SAX01.ogg'), -2)


# ---------------------------------------------------------------- output

META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: {mono}
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write(name, x, lufs, mono=True, stream=False):
    x = np.asarray(x, np.float64)
    x = x / max(1e-6, np.max(np.abs(x))) * 0.9
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, name + '.ogg')
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, name + '.wav')
        wavfile.write(wav, SR, (x * 32767).astype(np.int16))
        # Loops are levelled by gain only (loudnorm's dynamic mode would break the seam).
        af = f'volume={lufs}dB' if stream else f'loudnorm=I={lufs}:TP=-1.5:LRA=11,areverse,silenceremove=start_periods=1:start_threshold=-60dB,areverse'
        subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', wav, '-af', af, '-ar', str(SR), '-c:a', 'libvorbis', '-q:a', '5', dst], check=True)
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zombies-fps/casino/' + name).hex
    with open(dst + '.meta', 'w', newline='\n') as f:
        # The music streams; the stings are short and decompress on load so they start instantly.
        f.write(META.format(guid=guid, load=2 if stream else 0, mono=1 if mono else 0))
    print('wrote', dst)


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    jingles, casino = sys.argv[1], sys.argv[2]
    loop = music()
    loop = loop / np.max(np.abs(loop)) * 0.9
    write('slot_music', loop, -3, stream=True)
    write('slot_spin', spin(), -20)
    write('slot_win', win(jingles, casino, False), -16)
    write('slot_jackpot', win(jingles, casino, True), -15)
    write('slot_lose', lose(jingles), -17)


if __name__ == '__main__':
    main()
