"""
Packs Ivan's Quaternius character models into the game's own skinned-mesh format.

    python3 tools/characters/pack_characters.py <dir with the unzipped packs>

Unzip "Bestiary - Dungeon Monsters Kit[Standard].zip" and "Modular Character Outfits - Fantasy[Standard].zip"
into one directory and pass it. For each character this writes Assets/_Project/Resources/Characters/<Name>/:

  <Name>.bytes                 the skeleton and skinned mesh (format below), read by Characters/CharacterSkin.cs
  <key>_albedo[_<n>].jpg       colour per material (variants _2, _3), ambient occlusion baked in
  <key>_normal.jpg             tangent-space normal map
  <key>_emission.jpg           glow (eyes), when the material has one

plus Unity .meta files for anything new (existing metas are kept so GUIDs never change).

Format "SKN1" (little endian, Unity axes: glTF X is negated, triangles flipped, metres):
  int boneCount, then per bone: string name, int parent (-1 = root), float3 localPos, float4 localRot, float3 localScale
  int subCount, then per submesh: string materialKey
  int vertexCount, float3 position * n, float3 normal * n, float2 uv * n, byte4 boneIndex * n, float4 weight * n
  per submesh: int indexCount, int index * indexCount
  (string = int byteLength + UTF-8). The rest pose is the bind pose and is a T-pose facing +Z.
"""
import os, struct, sys, uuid
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from gltfio import Gltf

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(REPO, 'Assets', '_Project', 'Resources', 'Characters')
TEX_SIZE = 1024

BESTIARY = 'Bestiary - Dungeon Monsters Kit[Standard]'
OUTFITS = 'Modular Character Outfits - Fantasy[Standard]'

# name: (model file, {material: (texture prefix, [albedo variants], normal, orm, emission)}, meshes to leave out)
CHARACTERS = {
    'Imp': (BESTIARY + '/Exports/GLB (Godot-Unreal)/Imp.glb', {
        'MI_Imp': ('Imp', [BESTIARY + '/Textures/T_Imp_BaseColor_%d.png' % i for i in (1, 2, 3)],
                   BESTIARY + '/Textures/T_Imp_Normal.png', BESTIARY + '/Textures/T_Imp_ORM.png',
                   BESTIARY + '/Textures/T_Imp_Emissive.png'),
    }, []),
    'Puglin': (BESTIARY + '/Exports/GLB (Godot-Unreal)/Puglin.glb', {
        'MI_Puglin': ('Puglin', [BESTIARY + '/Textures/T_Puglin_BaseColor_%d.png' % i for i in (1, 2, 3)],
                      BESTIARY + '/Textures/T_Puglin_Normal.png', BESTIARY + '/Textures/T_Puglin_ORM.png',
                      BESTIARY + '/Textures/T_Puglin_Emissive.png'),
    }, []),
    'Ranger': (OUTFITS + '/Exports/glTF (Godot-Unreal)/Outfits/Male_Ranger.gltf', {
        'MI_Ranger': ('Ranger', [OUTFITS + '/Textures/Ranger/T_Ranger_BaseColor.png', OUTFITS + '/Textures/Ranger/T_Ranger_3_BaseColor.png'],
                      OUTFITS + '/Textures/Ranger/T_Ranger_Normal.png', OUTFITS + '/Textures/Ranger/T_Ranger_ORM.png', None),
        'MI_Regular_Male': ('SkinMale', [OUTFITS + '/Textures/Base/T_Regular_Male_Dark_BaseColor.png'],
                            OUTFITS + '/Textures/Base/T_Regular_Male_Normal.png', None, None),
    }, []),
}


def s(b):
    e = b.encode('utf-8')
    return struct.pack('<i', len(e)) + e


def to_unity_quat(q):
    x, y, z, w = q
    return (x, -y, -z, w)


def pack(src_root, name, spec):
    path, mats, skip = spec
    g = Gltf(os.path.join(src_root, path))
    skin = g.j['skins'][0]
    joints = skin['joints']
    jindex = {n: i for i, n in enumerate(joints)}
    bones = []
    for n in joints:
        node = g.nodes[n]
        p = g.parent.get(n)
        parent = jindex.get(p, -1)
        if parent == -1 and p is not None:
            # The bone's parent is the armature node: fold that node's transform into the bone.
            M = g.world(n)
        else:
            M = g.local(n)
        t = M[:3, 3]
        sc = np.linalg.norm(M[:3, :3], axis=0)
        R = M[:3, :3] / sc
        q = mat_to_quat(R)
        bones.append((g.name(n), parent, (-t[0], t[1], t[2]), to_unity_quat(q), tuple(sc)))

    material_names = [m['name'] for m in g.j['materials']]
    subs = {}
    pos, nrm, uv, bi, bw = [], [], [], [], []
    base = 0
    for i, node in enumerate(g.nodes):
        if 'mesh' not in node or node.get('name') in skip:
            continue
        for prim in g.j['meshes'][node['mesh']]['primitives']:
            a = prim['attributes']
            P = g.acc(a['POSITION'])
            N = g.acc(a['NORMAL'])
            T = g.acc(a['TEXCOORD_0'])
            J = g.acc(a['JOINTS_0']).astype(int)
            W = g.acc(a['WEIGHTS_0'])
            W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)
            idx = g.acc(prim['indices']).astype(int).reshape(-1, 3)
            P = P.copy(); P[:, 0] *= -1
            N = N.copy(); N[:, 0] *= -1
            T = T.copy(); T[:, 1] = 1.0 - T[:, 1]
            pos.append(P); nrm.append(N); uv.append(T); bi.append(J); bw.append(W)
            key = material_names[prim['material']]
            subs.setdefault(key, []).append(idx[:, [0, 2, 1]] + base)
            base += len(P)
    pos, nrm, uv, bi, bw = map(np.concatenate, (pos, nrm, uv, bi, bw))
    keys = [k for k in material_names if k in subs]

    out = [b'SKN1', struct.pack('<i', len(bones))]
    for bname, parent, t, q, sc in bones:
        out.append(s(bname) + struct.pack('<i3f4f3f', parent, *t, *q, *sc))
    out.append(struct.pack('<i', len(keys)))
    for k in keys:
        out.append(s(mats[k][0]))
    out.append(struct.pack('<i', len(pos)))
    out.append(pos.astype('<f4').tobytes())
    out.append(nrm.astype('<f4').tobytes())
    out.append(uv.astype('<f4').tobytes())
    out.append(bi.astype('u1').tobytes())
    out.append(bw.astype('<f4').tobytes())
    for k in keys:
        tri = np.concatenate(subs[k]).reshape(-1)
        out.append(struct.pack('<i', len(tri)) + tri.astype('<i4').tobytes())

    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    write(os.path.join(folder, name + '.bytes'), b''.join(out))
    print(f'{name}: {len(bones)} bones, {len(pos)} vertices, {sum(len(np.concatenate(v)) for v in subs.values())} triangles, materials {keys}')

    for k in keys:
        prefix, albedos, normal, orm, emission = mats[k]
        ao = None
        if orm:
            ao = Image.open(os.path.join(src_root, orm)).convert('RGB').split()[0].resize((TEX_SIZE, TEX_SIZE), Image.LANCZOS)
        for v, a in enumerate(albedos):
            img = Image.open(os.path.join(src_root, a)).convert('RGB').resize((TEX_SIZE, TEX_SIZE), Image.LANCZOS)
            if ao is not None:
                arr = np.asarray(img).astype(float) * (0.45 + 0.55 * np.asarray(ao)[..., None] / 255.0)
                img = Image.fromarray(arr.clip(0, 255).astype(np.uint8))
            save_jpg(img, os.path.join(folder, f'{prefix}_albedo' + (f'_{v + 1}' if v else '') + '.jpg'), 90, srgb=True)
            if prefix == 'Ranger' and v == 0:
                # Sergeant Brannock's Lantern Watch colours: the ranger's greens dyed Watch blue (variant 3).
                watch = recolor(img, (55, 170), 222, 0.85)
                save_jpg(watch, os.path.join(folder, f'{prefix}_albedo_{len(albedos) + 1}.jpg'), 90, srgb=True)
        if normal:
            img = Image.open(os.path.join(src_root, normal)).convert('RGB').resize((TEX_SIZE, TEX_SIZE), Image.LANCZOS)
            save_jpg(img, os.path.join(folder, f'{prefix}_normal.jpg'), 95, srgb=False)
        if emission:
            img = Image.open(os.path.join(src_root, emission)).convert('RGB').resize((TEX_SIZE // 2, TEX_SIZE // 2), Image.LANCZOS)
            save_jpg(img, os.path.join(folder, f'{prefix}_emission.jpg'), 90, srgb=True)
    folder_meta(folder)


def recolor(img, hue_range, hue, sat_scale):
    """Moves every colour whose hue (degrees) is inside hue_range to hue, keeping its shading."""
    hsv = np.asarray(img.convert('HSV')).astype(float)
    h = hsv[..., 0] * 360 / 255
    m = (h >= hue_range[0]) & (h <= hue_range[1]) & (hsv[..., 1] > 40)
    hsv[..., 0][m] = hue * 255 / 360
    hsv[..., 1][m] *= sat_scale
    return Image.fromarray(hsv.clip(0, 255).astype(np.uint8), 'HSV').convert('RGB')


def mat_to_quat(R):
    t = np.trace(R)
    if t > 0:
        S = np.sqrt(t + 1.0) * 2
        return ((R[2, 1] - R[1, 2]) / S, (R[0, 2] - R[2, 0]) / S, (R[1, 0] - R[0, 1]) / S, 0.25 * S)
    i = int(np.argmax(np.diag(R)))
    if i == 0:
        S = np.sqrt(1.0 + R[0, 0] - R[1, 1] - R[2, 2]) * 2
        return (0.25 * S, (R[0, 1] + R[1, 0]) / S, (R[0, 2] + R[2, 0]) / S, (R[2, 1] - R[1, 2]) / S)
    if i == 1:
        S = np.sqrt(1.0 + R[1, 1] - R[0, 0] - R[2, 2]) * 2
        return ((R[0, 1] + R[1, 0]) / S, 0.25 * S, (R[1, 2] + R[2, 1]) / S, (R[0, 2] - R[2, 0]) / S)
    S = np.sqrt(1.0 + R[2, 2] - R[0, 0] - R[1, 1]) * 2
    return ((R[0, 2] + R[2, 0]) / S, (R[1, 2] + R[2, 1]) / S, 0.25 * S, (R[1, 0] - R[0, 1]) / S)


def write(path, data):
    with open(path, 'wb') as f:
        f.write(data)
    meta(path, 'TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def save_jpg(img, path, quality, srgb):
    img.save(path, quality=quality)
    meta(path, TEXTURE_META.format(srgb=1 if srgb else 0, type=0 if srgb else 1))


def meta(path, body):
    m = path + '.meta'
    if os.path.exists(m):
        return
    with open(m, 'w', newline='\n') as f:
        f.write(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n' + body)


def folder_meta(folder):
    meta(folder, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


TEXTURE_META = """TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {srgb}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 0
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: {type}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

if __name__ == '__main__':
    src = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    folder_meta(OUT)
    for name, spec in CHARACTERS.items():
        pack(src, name, spec)
