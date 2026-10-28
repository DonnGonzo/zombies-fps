"""Repair Ogg Vorbis end-of-stream lengths without re-encoding.

FFmpeg 8's libvorbis wrapper can stamp the last page with the input's length
while the encoded packets hold up to ~0.1 s less audio. Unity's FSBTool then
warns "This source file was truncated". This walks the Vorbis packets, counts
the samples each one really decodes to and rewrites every page's granule
position (and CRC) to match, keeping any genuine end trim. Packet data is copied byte for byte, so the
decoded audio is unchanged.

  python tools/voice/fix_ogg_granules.py [--check] [paths or folders...]

With no paths it scans Assets/_Project/Resources/Audio/Voice. --check only
reports. Also used by generate_elevenlabs.py after each encode.
"""
import argparse, struct, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT/'Assets/_Project/Resources/Audio/Voice'

def _crc_table():
    table = []
    for i in range(256):
        r = i << 24
        for _ in range(8): r = ((r << 1) ^ 0x04C11DB7) if r & 0x80000000 else r << 1
        table.append(r & 0xFFFFFFFF)
    return table
CRC = _crc_table()

def ogg_crc(data):
    c = 0
    for b in data: c = ((c << 8) & 0xFFFFFFFF) ^ CRC[((c >> 24) ^ b) & 0xFF]
    return c

def read_pages(data):
    pages, i = [], 0
    while i < len(data):
        if data[i:i+4] != b'OggS': raise ValueError(f'bad page at byte {i}')
        n = data[i+26]
        lacing = data[i+27:i+27+n]
        body = i+27+n
        pages.append({'header': bytearray(data[i:body]), 'lacing': lacing,
                      'body': data[body:body+sum(lacing)]})
        i = body+sum(lacing)
    return pages

def packets(pages):
    """Yields (page index the packet ends on, packet bytes)."""
    buf = b''
    for p, page in enumerate(pages):
        pos = 0
        for seg in page['lacing']:
            buf += page['body'][pos:pos+seg]; pos += seg
            if seg < 255: yield p, buf; buf = b''

def bit(data, k): return (data[k >> 3] >> (k & 7)) & 1

def bits(data, start, count):
    return sum(bit(data, start+j) << j for j in range(count))

def mode_blockflags(setup):
    """Block flag per mode, read backwards from the setup header's framing bit
    (the same trick FFmpeg's vorbis parser uses; avoids decoding codebooks)."""
    end = len(setup)*8-1
    while end >= 0 and not bit(setup, end): end -= 1
    found, n, pos = 0, 0, end
    while pos-41 >= 6:
        pos -= 41
        if bits(setup, pos+1, 16) or bits(setup, pos+17, 16) or bits(setup, pos+33, 8) > 63: break
        n += 1
        if bits(setup, pos-6, 6)+1 == n: found = n
        if n >= 64: break
    if not found: raise ValueError('could not read Vorbis modes')
    first = end-41*found
    return [bit(setup, first+41*m) for m in range(found)]

def fixed_granules(pages):
    """Correct granule per page (-1 where no packet ends) and total samples."""
    pk = list(packets(pages))
    ident, setup = pk[0][1], pk[2][1]
    if ident[:7] != b'\x01vorbis' or setup[:7] != b'\x05vorbis': raise ValueError('not Ogg Vorbis')
    sizes = (1 << (ident[28] & 15), 1 << (ident[28] >> 4))
    flags = mode_blockflags(setup)
    mode_bits = (len(flags)-1).bit_length()
    granules, total, prev = [-1]*len(pages), 0, None
    for p, data in pk[3:]:
        if not data or data[0] & 1: continue
        size = sizes[flags[bits(data, 1, mode_bits)]]
        if prev is not None: total += prev//4 + size//4
        prev = size
        granules[p] = total
    for p in range(pk[2][0]+1): granules[p] = 0  # header pages
    return granules, total

def granule(page): return struct.unpack('<q', page['header'][6:14])[0]

def repair(path, check=False):
    data = Path(path).read_bytes()
    pages = read_pages(data)
    want, total = fixed_granules(pages)
    reported = granule(pages[-1])
    # A last granule below the packet total is the encoder's normal end trim;
    # only one past it (audio that isn't there) is wrong.
    want[-1] = min(reported, total)
    bad = [i for i, page in enumerate(pages) if granule(page) != want[i]]
    if not bad: return None
    if not check:
        out = bytearray()
        for i, page in enumerate(pages):
            h = page['header']
            h[6:14] = struct.pack('<q', want[i]); h[22:26] = b'\0\0\0\0'
            h[22:26] = struct.pack('<I', ogg_crc(bytes(h)+page['body']))
            out += h + page['body']
        tmp = Path(path).with_suffix('.fixing')
        tmp.write_bytes(out); tmp.replace(path)
    return reported, total, len(bad)

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('paths', nargs='*', type=Path)
    ap.add_argument('--check', action='store_true', help='report only, change nothing')
    args = ap.parse_args()
    files = []
    for p in args.paths or [DEFAULT]:
        files += sorted(p.rglob('*.ogg')) if p.is_dir() else [p]
    fixed = 0
    for f in files:
        result = repair(f, args.check)
        if result:
            fixed += 1
            print(f'{"BAD" if args.check else "FIXED"} {f}: reported {result[0]} samples, holds {result[1]} ({result[2]} pages)')
    print(f'{fixed} of {len(files)} files {"need fixing" if args.check else "fixed"}')
    return 1 if args.check and fixed else 0

if __name__ == '__main__': sys.exit(main())
