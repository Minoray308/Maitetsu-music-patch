"""Build the gallery-only XP3 patch; original game archives remain untouched.

The game's XP3 filter uses adlr as a cipher selector. Keep a known selector,
and satisfy its actual Adler-32 using a harmless UTF-16 CSV comment suffix.
"""
from pathlib import Path
import hashlib
import struct
import zlib

ROOT = Path(__file__).resolve().parent.parent
WORK = ROOT / 'bgm_mod_work'
MOD = ROOT / 'bgm_music_mod'
HASH = 0x8D5A1E0B


def checksum_comment(prefix: bytes) -> bytes:
    """Append 512 valid CJK characters inside the final # CSV comment."""
    n, prime = 512, 65521
    a, b = zlib.adler32(prefix) & 65535, zlib.adler32(prefix) >> 16
    total = (HASH % 65536 - a - 65 * n) % prime
    choices = [total + k * prime for k in range(3) if total + k * prime <= 255 * n]
    total = min(choices, key=lambda v: abs(v - 127 * n))
    values = [0] * n
    remaining = total
    for i in range(n):
        values[i] = min(255, remaining)
        remaining -= values[i]
    minimum = sum((i + 1) * v for i, v in enumerate(values))
    maximum = (n + 1) * total - minimum
    residue = ((HASH >> 16) - b - 2 * n * a - 65 * n * n) * pow(2, -1, prime) % prime
    target = minimum + (residue - minimum) % prime
    assert target <= maximum
    delta = target - minimum
    while delta:
        source = next(i for i, v in enumerate(values) if v and any(x < 255 for x in values[i + 1:]))
        dest = min(n - 1, source + delta)
        while values[dest] == 255:
            dest -= 1
        assert dest > source
        amount = min(values[source], 255 - values[dest], delta // (dest - source))
        values[source] -= amount
        values[dest] += amount
        delta -= amount * (dest - source)
    # Array indices are weights 1..n; on disk low-byte weights run n..1.
    suffix = bytes(byte for value in reversed(values) for byte in (value, 65))
    result = prefix + suffix
    assert zlib.adler32(result) == HASH
    result.decode('utf-16')  # valid Unicode, no embedded line breaks or CSV controls
    return result


def crypt(data: bytes) -> bytes:
    return bytes(value ^ (197 if i < 872 else 1) for i, value in enumerate(data))


def chunk(tag: bytes, data: bytes) -> bytes:
    return tag + struct.pack('<Q', len(data)) + data


def build():
    MOD.mkdir(exist_ok=True)
    original = (WORK / 'main_extra_music.func.txt').read_text('utf-8').replace('\r', '')
    # onSeekChange is a function, not a writable property.
    patched = original.replace('Current.prop("onSeekChange")', 'Current.func("onSeekChange")')
    hook = '\neval,names,exp,Scripts.execStorage(System.exePath+"bgm_music_mod/player.tjs")\n# BGM App MOD checksum padding   '
    plain = checksum_comment((patched + hook).encode('utf-16'))
    encrypted = crypt(plain)
    name = 'extra_music.func'
    name_bytes = name.encode('utf-16le')
    info = struct.pack('<IQQH', 0x80000000, len(plain), len(encrypted), len(name)) + name_bytes
    segment = struct.pack('<IQQQ', 0, 19, len(plain), len(encrypted))
    index = chunk(b'File', chunk(b'info', info) + chunk(b'segm', segment) + chunk(b'adlr', struct.pack('<I', HASH)))
    packed = zlib.compress(index)
    archive = b'XP3\r\n \n\x1a\x8bg\x01' + struct.pack('<Q', 19 + len(encrypted)) + encrypted
    archive += b'\x01' + struct.pack('<QQ', len(packed), len(index)) + packed
    assert crypt(encrypted) == plain
    assert zlib.adler32(crypt(archive[19:19 + len(encrypted)])) == HASH
    assert zlib.decompress(archive[19 + len(encrypted) + 17:]) == index
    patch = ROOT / 'patch.xp3'
    if patch.exists() and not (MOD / 'manifest.sha256').exists():
        raise RuntimeError('Existing patch.xp3 belongs to another mod; refusing to overwrite')
    if patch.exists():
        expected = (MOD / 'manifest.sha256').read_text('ascii').split()[0]
        assert hashlib.sha256(patch.read_bytes()).hexdigest() == expected, 'Installed patch changed; preserve it'
    patch.write_bytes(archive)
    player = (WORK / 'bgm_app_mod.tjs').read_text('utf-8')
    (MOD / 'player.tjs').write_text(player, encoding='utf-16')
    (MOD / 'extra_music.func').write_bytes(plain)
    (MOD / 'original_extra_music.func').write_text(original, encoding='utf-16')
    digest = hashlib.sha256(archive).hexdigest()
    (MOD / 'manifest.sha256').write_text(digest + '  patch.xp3\n', encoding='ascii')
    print(f'Installed {patch.name}: {len(archive)} bytes; cipher and checksum round-trip passed')
    print(f'SHA256 {digest}')


if __name__ == '__main__':
    build()
