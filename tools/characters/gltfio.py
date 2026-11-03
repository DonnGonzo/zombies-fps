"""Minimal glTF 2.0 reader (numpy): nodes, skins, skinned meshes. Used by pack_characters.py."""
import json, os, struct
import numpy as np

COMP = {5120: 'i1', 5121: 'u1', 5122: 'i2', 5123: 'u2', 5125: 'u4', 5126: 'f4'}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


class Gltf:
    def __init__(self, path):
        self.path = path
        raw = open(path, 'rb').read()
        if raw[:4] == b'glTF':
            n = struct.unpack_from('<I', raw, 12)[0]
            self.j = json.loads(raw[20:20 + n])
            off = 20 + n
            blen = struct.unpack_from('<I', raw, off)[0]
            self.blobs = [raw[off + 8:off + 8 + blen]]
        else:
            self.j = json.loads(raw)
            base = os.path.dirname(path)
            self.blobs = [open(os.path.join(base, b['uri']), 'rb').read() for b in self.j['buffers']]
        self.nodes = self.j['nodes']
        self.parent = {}
        for i, n in enumerate(self.nodes):
            for c in n.get('children', []):
                self.parent[c] = i

    def acc(self, i):
        a = self.j['accessors'][i]
        bv = self.j['bufferViews'][a['bufferView']]
        n = NCOMP[a['type']]
        dt = np.dtype(COMP[a['componentType']])
        off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = bv.get('byteStride', dt.itemsize * n)
        blob = self.blobs[bv['buffer']]
        if stride == dt.itemsize * n:
            out = np.frombuffer(blob, dtype=dt, count=a['count'] * n, offset=off).reshape(a['count'], n)
        else:
            out = np.stack([np.frombuffer(blob, dtype=dt, count=n, offset=off + k * stride) for k in range(a['count'])])
        out = out.astype(np.float64)
        if a.get('normalized'):
            out /= {np.dtype('u1'): 255.0, np.dtype('u2'): 65535.0}.get(dt, 1.0)
        return out

    def local(self, i):
        n = self.nodes[i]
        if 'matrix' in n:
            return np.array(n['matrix'], dtype=float).reshape(4, 4).T
        t = np.array(n.get('translation', [0, 0, 0]), float)
        x, y, z, w = n.get('rotation', [0, 0, 0, 1])
        s = np.array(n.get('scale', [1, 1, 1]), float)
        R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                      [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                      [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
        M = np.eye(4)
        M[:3, :3] = R * s
        M[:3, 3] = t
        return M

    def world(self, i):
        M = self.local(i)
        while i in self.parent:
            i = self.parent[i]
            M = self.local(i) @ M
        return M

    def name(self, i):
        return self.nodes[i].get('name', 'node%d' % i)
