"""Match the Unity cutout materials without changing GLB geometry or animation."""
import json
import struct
from pathlib import Path


def fix_alpha(path):
    path = Path(path)
    data = path.read_bytes()
    magic, version, length = struct.unpack_from('<III', data)
    assert magic == 0x46546C67 and version == 2 and length == len(data)
    size, kind = struct.unpack_from('<II', data, 12)
    assert kind == 0x4E4F534A
    document = json.loads(data[20:20 + size])
    for material in document['materials']:
        if material.get('name', '').startswith('EXM_'):
            material.update(alphaMode='MASK', alphaCutoff=0.3, doubleSided=True)
    encoded = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    tail = data[20 + size:]
    result = struct.pack('<III', magic, version, 20 + len(encoded) + len(tail))
    result += struct.pack('<II', len(encoded), kind) + encoded + tail
    path.write_bytes(result)


if __name__ == '__main__':
    fix_alpha(Path(__file__).resolve().parents[1] / 'Export/Exusiai_MMD.glb')
