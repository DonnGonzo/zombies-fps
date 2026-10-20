"""Copies the sounds the game uses out of Ivan's "Rust & Blood - SFX Library [Sample]" into Resources/Audio/SFX,
under the short names ProceduralAudio looks for.

    python3 tools/sfx/pack_sfx.py <unzipped "Rust & Blood - SFX Library [Sample]" folder> [out folder]

The library's licence (Pizza Doggy) allows use in games but not sharing the files on their own, so the folder is
git-ignored while the repository is public: run this on the machine that builds the game.
"""
import os, shutil, sys, uuid

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Resources', 'Audio', 'SFX')
FX = 'OGG/Sound Effects/'
PICK = {
    'rifle_fire': 'Firearms/Fire/assault_rifle_fire_ufx_1.ogg',
    'rifle_empty': 'Firearms/Misc/rifle_universal_empty_barrel_ufx_2.ogg',
    'mag_out': 'Firearms/Reloading/assault_rifle_reloading_mag_out_ufx_1.ogg',
    'mag_in': 'Firearms/Reloading/assault_rifle_reloading_mag_in_ufx_1.ogg',
    'body_impact': 'Impact & Break/Body/impact_body_ufx_2.ogg',
    'surface_hit': 'Impact & Break/Concrete/impact_concrete_ufx_1.ogg',
    'explosion': 'Explosions/explosion_close_long_ufx_7.ogg',
    'swing': 'Melee/Swing/universal_swing_miss_light_ufx_1.ogg',
    'melee_hit': 'Melee/Hit/blunt_hit_bloody_moderate_ufx_1.ogg',
    'flesh_hit_1': 'Melee/Hit/blunt_hit_bloody_light_ufx_1.ogg',
    'flesh_hit_2': 'Melee/Hit/blunt_hit_bloody_light_ufx_2.ogg',
    'flesh_hit_3': 'Melee/Hit/blunt_hit_bloody_heavy_ufx_1.ogg',
    'footstep_concrete': 'Player/Footsteps/Concrete/footstep_concrete_ufx_4.ogg',
    'footstep_wood': 'Player/Footsteps/Wood/footstep_wood_ufx_13.ogg',
    'footstep_metal': 'Player/Footsteps/Metal/footstep_metal_ufx_3.ogg',
    'footstep_gravel': 'Player/Footsteps/Gravel/footstep_gravel_ufx_5.ogg',
    'footstep_dirt': 'Player/Footsteps/Dirt/footstep_dirt_ufx_5.ogg',
}
# Short, played often: decompressed on load. Fixed guids, so every machine that runs this gets the same metas.
META = """AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.8
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def main(src, out=OUT):
    os.makedirs(out, exist_ok=True)
    for name, rel in PICK.items():
        dst = os.path.join(out, name + '.ogg')
        shutil.copyfile(os.path.join(src, FX, rel), dst)
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zombies-fps/sfx/' + name).hex
        with open(dst + '.meta', 'w', newline='\n') as f:
            f.write(f'fileFormatVersion: 2\nguid: {guid}\n' + META.format())
        print(dst)


if __name__ == '__main__':
    main(*sys.argv[1:3])
