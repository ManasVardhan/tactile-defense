"""Minimal FBX binary reader: prints Model nodes with their Lcl transforms, the
GlobalSettings axes and AnimStack names. Usage: python3 fbx_inspect.py file.fbx"""
import struct
import sys
import zlib


def read_props(d, off, n):
    out = []
    for _ in range(n):
        t = chr(d[off]); off += 1
        if t in 'YCIFDL':
            fmt = {'Y': '<h', 'C': '<?', 'I': '<i', 'F': '<f', 'D': '<d', 'L': '<q'}[t]
            v = struct.unpack_from(fmt, d, off)[0]; off += struct.calcsize(fmt)
        elif t in 'SR':
            ln = struct.unpack_from('<I', d, off)[0]; off += 4
            v = d[off:off + ln]; off += ln
            if t == 'S':
                v = v.replace(b'\x00\x01', b'::').decode('utf8', 'replace')
        elif t in 'fdlib':
            ln, enc, clen = struct.unpack_from('<III', d, off); off += 12
            raw = d[off:off + clen]; off += clen
            if enc:
                raw = zlib.decompress(raw)
            v = ('array', t, ln)
        else:
            raise ValueError(t)
        out.append(v)
    return out, off


def read_node(d, off, v64):
    if v64:
        end, nprops, plen = struct.unpack_from('<QQQ', d, off); off += 24
    else:
        end, nprops, plen = struct.unpack_from('<III', d, off); off += 12
    nl = d[off]; off += 1
    name = d[off:off + nl].decode(); off += nl
    if end == 0:
        return None, off
    props, _ = read_props(d, off, nprops)
    off += plen
    kids = []
    sentinel = 25 if v64 else 13
    while off < end - sentinel or (off < end and d[off:end] != b'\x00' * (end - off)):
        k, off = read_node(d, off, v64)
        if k is None:
            break
        kids.append(k)
    return (name, props, kids), end


def main(path):
    d = open(path, 'rb').read()
    ver = struct.unpack_from('<I', d, 23)[0]
    v64 = ver >= 7500
    off = 27
    top = []
    while off < len(d):
        n, off = read_node(d, off, v64)
        if n is None:
            break
        top.append(n)
    print("FBX version", ver)
    for name, props, kids in top:
        if name == 'GlobalSettings':
            for k in kids:
                if k[0] == 'Properties70':
                    for p in k[2]:
                        if p[1][0] in ('UpAxis', 'UpAxisSign', 'FrontAxis', 'FrontAxisSign',
                                       'CoordAxis', 'CoordAxisSign', 'UnitScaleFactor',
                                       'OriginalUnitScaleFactor'):
                            print("  GlobalSettings", p[1][0], p[1][-1])
        if name == 'Objects':
            for k in kids:
                if k[0] in ('Model', 'AnimationStack'):
                    tr = {}
                    for c in k[2]:
                        if c[0] == 'Properties70':
                            for p in c[2]:
                                if p[1][0] in ('Lcl Translation', 'Lcl Rotation', 'Lcl Scaling',
                                               'PreRotation', 'PostRotation', 'LocalStop'):
                                    tr[p[1][0]] = tuple(round(x, 4) if isinstance(x, float)
                                                        else x for x in p[1][4:])
                    print("  %s %s %s %s" % (k[0], k[1][1], k[1][2] if len(k[1]) > 2 else '',
                                             tr))


if __name__ == '__main__':
    main(sys.argv[1])
