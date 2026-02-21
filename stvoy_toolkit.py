#!/usr/bin/env python3
"""
Star Trek Voyager: Across the Unknown — Save Game Toolkit

Reads, modifies, and rewrites save files for STV:AtU.
Format: Base64-encoded, bit-packed C3Serialize/FunIO binary.

Requirements: Python 3.8+ (no external dependencies for core functionality)
Optional: pycryptodome, pefile, capstone (for binary analysis)

Usage:
    python stvoy_toolkit.py info <save_file>
    python stvoy_toolkit.py resources <save_file>
    python stvoy_toolkit.py hull <save_file>
    python stvoy_toolkit.py set-hull <save_file> <value>
    python stvoy_toolkit.py set-resource <save_file> <name> <value>
"""

import base64
import struct
import sys
import shutil
from pathlib import Path


# =============================================================================
# CRC32 Hash (custom init value)
# =============================================================================

CRC_TABLE = []
for _i in range(256):
    _crc = _i
    for _ in range(8):
        if _crc & 1:
            _crc = (_crc >> 1) ^ 0xEDB88320
        else:
            _crc >>= 1
    CRC_TABLE.append(_crc)


def custom_crc(data, init=0x61635263):
    """CRC32 with custom initial value 0x61635263. Computed over data[16:]."""
    crc = init
    for byte in data:
        crc = (crc >> 8) ^ CRC_TABLE[(crc ^ byte) & 0xFF]
    return crc & 0xFFFFFFFF


# =============================================================================
# Bit-Stream Primitives
# =============================================================================

def get_bit(data, pos):
    return (data[pos >> 3] >> (pos & 7)) & 1


def read_bits_at(data, pos, count):
    v = 0
    for i in range(count):
        v |= get_bit(data, pos + i) << i
    return v


def set_bit(data, pos, val):
    byte_idx = pos >> 3
    bit_idx = pos & 7
    if val:
        data[byte_idx] |= (1 << bit_idx)
    else:
        data[byte_idx] &= ~(1 << bit_idx)


def write_bits(data, pos, value, count):
    for i in range(count):
        set_bit(data, pos + i, (value >> i) & 1)


# =============================================================================
# Bit-Stream Reader
# =============================================================================

class BitReader:
    def __init__(self, data, pos=0):
        self.data = data
        self.pos = pos

    def read_bool(self):
        v = get_bit(self.data, self.pos)
        self.pos += 1
        return v

    def read_bits(self, n):
        v = read_bits_at(self.data, self.pos, n)
        self.pos += n
        return v

    def debug_skip(self, oid):
        self.read_bool()
        if oid != -1:
            self.read_bits(32)

    def read_u32_packed(self, oid=-1):
        self.debug_skip(oid)
        if not self.read_bool():
            return 0
        lzc = self.read_bits(5)
        vb = 32 - lzc
        return self.read_bits(vb) if vb > 0 else 0

    def read_i32_packed(self, oid=-1):
        self.debug_skip(oid)
        if not self.read_bool():
            return 0
        s = self.read_bool()
        lzc = self.read_bits(5)
        vb = 32 - lzc
        v = self.read_bits(vb) if vb > 0 else 0
        return -v if s else v

    def read_u64_packed(self, oid=-1):
        self.debug_skip(oid)
        lo = self.read_u32_packed(-1)
        hi = self.read_u32_packed(-1)
        return (hi << 32) | lo

    def read_bool_wrapped(self, oid=-1):
        self.debug_skip(oid)
        return self.read_bool()

    def read_string(self, oid=-1):
        self.debug_skip(oid)
        length = self.read_u32_packed(-1)
        if length == 0:
            return ""
        cb = self.read_bits(4)
        base = self.read_bits(8)
        return ''.join(chr(self.read_bits(cb) + base) for _ in range(length))

    def read_float(self, oid=-1):
        """ReadFloat via func_95230: DebugIdCheck + ReadU32Packed, interpret as float."""
        self.debug_skip(oid)
        raw = self.read_u32_packed(-1)
        return struct.unpack('<f', struct.pack('<I', raw))[0]

    def read_chunk_header(self):
        r = {}
        if self.read_bool():
            r['parent'] = struct.pack('<I', self.read_bits(32)).decode('ascii', 'replace')
        r['tag'] = struct.pack('<I', self.read_u32_packed(-1)).decode('ascii', 'replace')
        r['version'] = self.read_u32_packed(-1)
        r['subversion'] = self.read_u32_packed(-1)
        r['size_pos'] = self.pos
        r['size'] = self.read_bits(32)
        r['data_start'] = self.pos
        r['data_end'] = self.pos + r['size']
        return r

    def skip_chunk(self, ch):
        self.pos = ch['data_end']
        if self.read_bool():
            self.read_bits(32)

    def read_chunk_start(self, oid=-1):
        """ReadChunkStart (func_92b30): DebugIdCheck + 3×U32Packed + 32-bit size."""
        self.debug_skip(oid)
        ver = self.read_u32_packed(-1)
        subver = self.read_u32_packed(-1)
        val3 = self.read_u32_packed(-1)
        size = self.read_bits(32)
        data_end = self.pos + size
        return {
            'ver': ver, 'subver': subver, 'val3': val3,
            'size': size, 'data_start': self.pos, 'data_end': data_end
        }


# =============================================================================
# Bit-Stream Encoder (for writing packed values)
# =============================================================================

def encode_i32_packed(value):
    """Encode a signed 32-bit value as I32Packed bits (no debug prefix)."""
    bits = []
    if value == 0:
        bits.append(0)  # nonzero = 0
        return bits
    bits.append(1)  # nonzero = 1
    bits.append(1 if value < 0 else 0)  # sign
    magnitude = abs(value)
    lzc = 0
    for b in range(31, -1, -1):
        if magnitude & (1 << b):
            break
        lzc += 1
    for i in range(5):
        bits.append((lzc >> i) & 1)
    vb = 32 - lzc
    for i in range(vb):
        bits.append((magnitude >> i) & 1)
    return bits


def encode_u32_packed(value):
    """Encode an unsigned 32-bit value as U32Packed bits (no debug prefix)."""
    bits = []
    if value == 0:
        bits.append(0)  # nonzero = 0
        return bits
    bits.append(1)  # nonzero = 1
    lzc = 0
    for b in range(31, -1, -1):
        if value & (1 << b):
            break
        lzc += 1
    for i in range(5):
        bits.append((lzc >> i) & 1)
    vb = 32 - lzc
    for i in range(vb):
        bits.append((value >> i) & 1)
    return bits


# =============================================================================
# Save File I/O
# =============================================================================

def load_save(path):
    """Load and base64-decode a save file. Returns bytearray."""
    with open(path, "rb") as f:
        raw = f.read()
    data = bytearray(base64.b64decode(raw))
    file_size = struct.unpack_from('<I', data, 0)[0]
    assert file_size == len(data), f"File size mismatch: header={file_size}, actual={len(data)}"
    return data


def save_file(data, path):
    """Recompute hash, base64-encode, and write save file."""
    new_hash = custom_crc(data[16:])
    struct.pack_into('<I', data, 4, new_hash)
    encoded = base64.b64encode(bytes(data))
    with open(path, "wb") as f:
        f.write(encoded)
    return len(encoded)


def make_backup(path):
    """Create a .backup copy if one doesn't already exist."""
    backup = Path(str(path) + ".backup")
    if not backup.exists():
        shutil.copy2(path, backup)
        return True
    return False


# =============================================================================
# Chunk Navigation
# =============================================================================

def navigate_to_cser(data):
    """Navigate from file start to the cser (resources) chunk. Returns (reader, chunk_header)."""
    r = BitReader(data, 128)
    parw = r.read_chunk_header()
    daeh = r.read_chunk_header()
    r.skip_chunk(daeh)
    emag = r.read_chunk_header()
    trts = r.read_chunk_header()
    cser = r.read_chunk_header()
    return r, cser


def navigate_to_tsnc(data):
    """Navigate to the tsnc (construction) chunk. Returns (reader, chunk_header)."""
    r = BitReader(data, 128)
    parw = r.read_chunk_header()
    daeh = r.read_chunk_header()
    r.skip_chunk(daeh)
    emag = r.read_chunk_header()
    trts = r.read_chunk_header()
    cser = r.read_chunk_header()
    r.skip_chunk(cser)
    psjv = r.read_chunk_header()
    r.skip_chunk(psjv)
    tces = r.read_chunk_header()
    r.skip_chunk(tces)
    tsnc = r.read_chunk_header()
    return r, tsnc


def find_shct_position(data, tsnc):
    """Scan tsnc data for the 'shct' tag and return the bit position after reading it."""
    target_shct = 0x73686374
    for pos in range(tsnc['data_start'], tsnc['data_end'] - 33):
        if get_bit(data, pos) == 1:
            val = read_bits_at(data, pos + 1, 32)
            if val == target_shct:
                r = BitReader(data, pos)
                r.read_i32_packed(target_shct)
                return r.pos
    return None


# =============================================================================
# Resource Reading
# =============================================================================

TAG_RICT = 0x74636972  # "rict"
TAG_RINM = 0x6d6e6972  # "rinm"
TAG_RRQC = 0x63717272  # "rrqc"
TAG_RTMC = 0x636d7472  # "rtmc"


def read_resources(data):
    """Read all resources from the cser chunk. Returns list of dicts with bit positions."""
    r, cser = navigate_to_cser(data)
    count = r.read_i32_packed(TAG_RICT)
    resources = []
    for _ in range(count):
        idx = r.read_u32_packed(-1)
        name = r.read_string(TAG_RINM)
        qty_pos = r.pos  # bit position of the quantity value
        qty = r.read_i32_packed(-1)
        flag = r.read_bool_wrapped(-1)
        resources.append({
            'index': idx,
            'name': name,
            'quantity': qty,
            'quantity_bit_pos': qty_pos,
            'flag': flag,
        })
    return resources


# =============================================================================
# Hull Integrity
# =============================================================================

def read_hull_integrity(data):
    """Read current hull integrity from tsnc chunk. Returns (float_value, bit_position)."""
    r, tsnc = navigate_to_tsnc(data)
    after_shct = find_shct_position(data, tsnc)
    if after_shct is None:
        raise ValueError("Could not find shct tag in tsnc chunk")
    hull_pos = after_shct + 8
    raw = read_bits_at(data, hull_pos, 32)
    hull = struct.unpack('<f', struct.pack('<I', raw))[0]
    return hull, hull_pos


def set_hull_integrity(data, new_value):
    """Set hull integrity to a new float value. In-place 32-bit replacement."""
    hull, hull_pos = read_hull_integrity(data)
    new_raw = struct.unpack('<I', struct.pack('<f', float(new_value)))[0]
    write_bits(data, hull_pos, new_raw, 32)
    return hull, new_value, hull_pos


# =============================================================================
# Resource Modification (with bit-stream reconstruction)
# =============================================================================

def get_chunk_size_positions(data):
    """Get the bit positions of the 4 chunk size fields (parw, emag, trts, cser)."""
    r = BitReader(data, 128)
    parw = r.read_chunk_header()
    daeh = r.read_chunk_header()
    r.skip_chunk(daeh)
    emag = r.read_chunk_header()
    trts = r.read_chunk_header()
    cser = r.read_chunk_header()
    return [parw['size_pos'], emag['size_pos'], trts['size_pos'], cser['size_pos']]


def modify_resources(data, modifications):
    """
    Modify resource quantities. modifications = dict of {name: new_value}.
    Returns modified data (bytearray).

    Handles bit-stream reconstruction when packed values change size.
    """
    resources = read_resources(data)
    size_positions = get_chunk_size_positions(data)

    # Build list of patches: (bit_pos, old_bits, new_bits)
    patches = []
    for res in resources:
        if res['name'] in modifications:
            new_val = modifications[res['name']]
            old_val = res['quantity']
            old_bits = encode_i32_packed(old_val)
            new_bits = encode_i32_packed(new_val)
            # The quantity is preceded by DebugIdCheck(-1) = 1 bit
            # The encode functions don't include the debug bit
            patches.append({
                'pos': res['quantity_bit_pos'] + 1,  # skip debug bit
                'old_len': len(old_bits),
                'new_bits': new_bits,
                'name': res['name'],
                'old_val': old_val,
                'new_val': new_val,
            })

    if not patches:
        return data

    patches.sort(key=lambda p: p['pos'])

    # Compute total bit delta
    total_delta = sum(len(p['new_bits']) - p['old_len'] for p in patches)

    # Rebuild bit stream
    total_bits = len(data) * 8
    new_total_bits = total_bits + total_delta
    new_data = bytearray((new_total_bits + 7) // 8)

    src_pos = 0
    dst_pos = 0

    def copy_bits(src_start, src_end, dst_start):
        nonlocal dst_pos
        for i in range(src_end - src_start):
            bit = get_bit(data, src_start + i)
            set_bit(new_data, dst_start + i, bit)
        return dst_start + (src_end - src_start)

    for patch in patches:
        # Copy bits before this patch
        dst_pos = copy_bits(src_pos, patch['pos'], dst_pos)
        src_pos = patch['pos']

        # Write new encoded value
        for i, bit in enumerate(patch['new_bits']):
            set_bit(new_data, dst_pos + i, bit)
        dst_pos += len(patch['new_bits'])
        src_pos += patch['old_len']

    # Copy remaining bits
    copy_bits(src_pos, total_bits, dst_pos)

    # Update chunk size fields
    for size_pos in size_positions:
        # Adjust size_pos for any shifts before it (shouldn't shift, sizes are before cser data)
        old_size = read_bits_at(data, size_pos, 32)
        new_size = old_size + total_delta
        write_bits(new_data, size_pos, new_size, 32)

    # Update file_size
    new_file_size = len(new_data)
    struct.pack_into('<I', new_data, 0, new_file_size)

    return new_data


# =============================================================================
# CLI Interface
# =============================================================================

def cmd_info(path):
    data = load_save(path)
    file_size = struct.unpack_from('<I', data, 0)[0]
    hash_val = struct.unpack_from('<I', data, 4)[0]
    version = struct.unpack_from('<I', data, 8)[0]
    debug = struct.unpack_from('<I', data, 12)[0]
    print(f"File: {path}")
    print(f"Decoded size: {len(data)} bytes")
    print(f"File size field: {file_size}")
    print(f"Hash: 0x{hash_val:08x}")
    print(f"Version: {version}")
    print(f"Debug flag: {debug}")

    # Verify hash
    computed = custom_crc(data[16:])
    print(f"Hash valid: {computed == hash_val}")

    # Show chunk structure
    r = BitReader(data, 128)
    parw = r.read_chunk_header()
    print(f"\nChunk: {parw['tag']} ver={parw['version']} size={parw['size']} bits")

    daeh = r.read_chunk_header()
    print(f"  {daeh['tag']} ver={daeh['version']} size={daeh['size']}")
    r.skip_chunk(daeh)

    emag = r.read_chunk_header()
    print(f"  {emag['tag']} ver={emag['version']} size={emag['size']}")

    trts = r.read_chunk_header()
    print(f"    {trts['tag']} ver={trts['version']} size={trts['size']}")

    # List sub-chunks within trts
    while r.pos < trts['data_end']:
        try:
            ch = r.read_chunk_header()
            print(f"      {ch['tag']} ver={ch['version']} size={ch['size']}")
            r.skip_chunk(ch)
        except:
            break


def cmd_resources(path):
    data = load_save(path)
    resources = read_resources(data)
    print(f"{'Name':<45} {'Qty':>10}  {'Flag'}  {'Bit Pos':>10}")
    print("-" * 75)
    for res in resources:
        flag_str = "item" if res['flag'] else "base"
        print(f"{res['name']:<45} {res['quantity']:>10}  {flag_str}  {res['quantity_bit_pos']:>10}")
    print(f"\nTotal: {len(resources)} resources")


def cmd_hull(path):
    data = load_save(path)
    hull, hull_pos = read_hull_integrity(data)
    print(f"Hull integrity: {hull}")
    print(f"Bit position: {hull_pos}")
    print(f"Note: Max hull is computed from modules, not stored in save")


def cmd_set_hull(path, value):
    data = load_save(path)
    make_backup(path)
    old, new, pos = set_hull_integrity(data, value)
    written = save_file(data, path)
    print(f"Hull integrity: {old} → {new}")
    print(f"Saved: {written} bytes")


def cmd_set_resource(path, name, value):
    data = load_save(path)
    make_backup(path)
    new_data = modify_resources(data, {name: int(value)})
    written = save_file(new_data, path)
    print(f"Set {name} = {value}")
    print(f"Saved: {written} bytes")


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)

    cmd = sys.argv[1]
    path = sys.argv[2]

    if cmd == "info":
        cmd_info(path)
    elif cmd == "resources":
        cmd_resources(path)
    elif cmd == "hull":
        cmd_hull(path)
    elif cmd == "set-hull":
        if len(sys.argv) < 4:
            print("Usage: stvoy_toolkit.py set-hull <save_file> <value>")
            sys.exit(1)
        cmd_set_hull(path, float(sys.argv[3]))
    elif cmd == "set-resource":
        if len(sys.argv) < 5:
            print("Usage: stvoy_toolkit.py set-resource <save_file> <name> <value>")
            sys.exit(1)
        cmd_set_resource(path, sys.argv[3], sys.argv[4])
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)
        sys.exit(1)


if __name__ == "__main__":
    main()
