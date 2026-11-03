"""Cuts CC0 creature recordings from OpenGameArt into the zombie voice clips the game plays, under
Resources/Audio/Zombie as groan_NN, attack_NN and death_NN (ZombieVoice picks them up by prefix).

    python3 tools/zombievox/pack_zombie_vox.py <download folder> [out folder]

The download folder holds the files exactly as OpenGameArt serves them (zips unzipped in place):
    darsycho__zombie-moans.ogg            https://opengameart.org/content/zombie-moans               (Darsycho, CC0)
    hungry-ghoul.ogg                      https://opengameart.org/content/hungry-ghoul               (Darsycho, CC0)
    qubodup-GhostMoans/wav/*.wav          https://opengameart.org/content/ghost-monster-voice-moaning-growling (qubodup, CC0)
    Monster-Sounds-Volume-2/*.wav         https://opengameart.org/node/4017                          (Ogrebane, CC0)

Every cut is folded to mono, trimmed of silence, faded at both ends and levelled to the same loudness, so no one
clip jumps out. The ghost moans are pitched down a few semitones: sung by a ghost they are too airy for a corpse.
Needs ffmpeg on the PATH. Writes .meta files with fixed guids, so a re-run doesn't churn the repository.
"""
import os, subprocess, sys, uuid

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources', 'Audio', 'Zombie')

MOANS = 'darsycho__zombie-moans.ogg'
GHOUL = 'hungry-ghoul.ogg'
GHOST = 'qubodup-GhostMoans/wav/qubodup-GhostMoan0{}.wav'
OGRE = 'Monster-Sounds-Volume-2/{}.wav'

# (pool, source, start s, end s or None, semitones). Cut points sit in the silences ffmpeg's silencedetect finds.
CUTS = [
    ('groan', MOANS, 0.24, 2.95, 0), ('groan', MOANS, 3.66, 6.24, 0), ('groan', MOANS, 6.63, 8.89, 0),
    ('groan', MOANS, 11.47, 14.44, 0), ('groan', MOANS, 14.81, None, 0),
    ('attack', MOANS, 9.30, 10.19, 0), ('attack', MOANS, 10.42, 11.21, 0),
    ('groan', GHOUL, 0.0, 4.42, 0), ('groan', GHOUL, 5.34, 7.70, 0), ('groan', GHOUL, 8.38, 10.92, 0),
    ('groan', GHOUL, 11.87, 14.21, 0), ('groan', GHOUL, 15.08, 17.45, 0), ('groan', GHOUL, 18.35, 20.70, 0),
    ('groan', GHOUL, 28.39, 30.67, 0), ('groan', GHOUL, 32.00, None, 0),
    ('attack', GHOUL, 21.78, 22.62, 0), ('attack', GHOUL, 23.29, 24.32, 0), ('attack', GHOUL, 24.92, 25.92, 0),
    ('death', GHOUL, 26.48, 27.59, -2),
    ('groan', GHOST.format(1), 0, None, -4), ('groan', GHOST.format(2), 0, None, -4),
    ('groan', GHOST.format(3), 0, None, -5), ('groan', GHOST.format(4), 0, None, -4),
    ('death', GHOST.format(5), 0, None, -5),
    ('death', OGRE.format('Monster-1'), 0, None, -2), ('death', OGRE.format('Monster-2'), 0, None, -2),
    ('death', OGRE.format('Monster-3'), 0, None, -2), ('death', OGRE.format('monster-14'), 0, None, -2),
] + [('attack', OGRE.format(n), 0, None, -1) for n in (
    'Monster-4', 'Monster-5', 'Monster-6', 'Monster-7', 'Monster-8', 'monster-9', 'monster-10', 'monster-11',
    'monster-12', 'monster-13', 'monster-15', 'monster-16', 'monster-17', 'monster-18')]

# Played far more often than any other sound, by many zombies at once: compressed in memory, not streamed.
META = """AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 1
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.6
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def cut(src, start, end, semis, dst):
    rate = 44100
    shift = 2 ** (semis / 12)
    chain = [f'asetrate={rate * shift:.0f}', f'aresample={rate}'] if semis else []
    chain += [
        'silenceremove=start_periods=1:start_threshold=-45dB:stop_periods=-1:stop_threshold=-45dB:stop_duration=0.15',
        'loudnorm=I=-18:TP=-2:LRA=11',
        'afade=t=in:d=0.02', 'areverse', 'afade=t=in:d=0.08', 'areverse',
    ]
    cmd = ['ffmpeg', '-v', 'error', '-y', '-ss', str(start)] + (['-to', str(end)] if end else []) + [
        '-i', src, '-ac', '1', '-af', ','.join(chain), '-ar', str(rate), '-c:a', 'libvorbis', '-q:a', '4', dst]
    subprocess.run(cmd, check=True)


def main(src, out=OUT):
    os.makedirs(out, exist_ok=True)
    counts = {}
    for pool, rel, start, end, semis in CUTS:
        counts[pool] = counts.get(pool, 0) + 1
        name = f'{pool}_{counts[pool]:02d}'
        dst = os.path.join(out, name + '.ogg')
        cut(os.path.join(src, rel), start, end, semis, dst)
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zombies-fps/zombievox/' + name).hex
        with open(dst + '.meta', 'w', newline='\n') as f:
            f.write(f'fileFormatVersion: 2\nguid: {guid}\n' + META)
        print(name, '<-', rel, start, end, semis)


if __name__ == '__main__':
    main(*sys.argv[1:3])
