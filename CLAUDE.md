# Star Trek Voyager: Across the Unknown — Save Game Reverse Engineering

## Project Overview

This project reverse engineers the save game format for **Star Trek Voyager: Across the Unknown** (UE 5.6) to read, modify, and rewrite save files. The format is **C3Serialize/FunIO** — a proprietary bit-packed binary serialization, NOT standard UE5 GVAS.

## Key Paths

- **Game binary**: `/Volumes/ian-desktop/Program Files (x86)/Steam/steamapps/common/Star Trek Voyager - Across the Unknown/STVoyager/Binaries/Win64/STVoyager-Win64-Shipping.exe` (175,535,104 bytes)
- **Save files**: `/Volumes/ian-desktop/Users/ian/AppData/Local/STVoyager/Saved/SaveGames/76561197970321648/`
- **Save file pattern**: `00_GX_STV_SaveGame_NNNN.sav`
- **Python virtualenv**: `/tmp/stvoy_venv` (pycryptodome, pefile, capstone, lief)

## Save File Format

### Encoding Layers

1. **Outer**: Base64 encoded (entire file is ASCII base64 characters)
2. **Inner**: Bit-packed binary (C3Serialize with FunIO)

To read: `data = base64.b64decode(raw_file_bytes)`
To write: `encoded = base64.b64encode(modified_bytes)`

### File Header (16 bytes, raw binary after base64 decode)

| Offset | Size | Field | Notes |
|--------|------|-------|-------|
| 0 | 4 | file_size | Little-endian uint32, equals len(decoded_data) |
| 4 | 4 | hash | CRC32 with custom init, see below |
| 8 | 4 | version | Always 1 |
| 12 | 4 | debug_flag | Always 1 (debug mode enabled) |

### Hash Algorithm

Standard CRC32 polynomial (same lookup table as `binascii.crc32`) but with **custom initial value `0x61635263`** instead of `0xFFFFFFFF`. Computed over `data[16:]` (everything after the 16-byte header).

```python
def custom_crc(data, init=0x61635263):
    # Uses standard CRC32 table
    crc_table = [...]  # standard CRC32 256-entry table
    crc = init
    for byte in data:
        crc = (crc >> 8) ^ crc_table[(crc ^ byte) & 0xFF]
    return crc & 0xFFFFFFFF
```

The CRC table can be extracted from the game binary at RVA `0x8a0ef30` (.rdata section), but it's identical to the standard CRC32 table.

## Bit-Packed Encoding

All data after the 16-byte header is a bit stream. Bits are read **LSB-first** within each byte. Bit position 0 = bit 0 of byte 0.

### Core Read Functions

#### ReadBool
Read 1 bit. `byte[bit_pos >> 3] >> (bit_pos & 7) & 1`. Advance bit_pos by 1.

#### ReadBits(count)
Read `count` bits LSB-first into a value. Calls ReadBool in a loop, setting `value |= (bit << i)`.

#### DebugIdCheck(optional_id)
Since debug_flag=1, this is present before every packed value:
- Read 1 bit (`has_tag`)
- If `optional_id != -1`: read 32 bits (tag value, should match optional_id)
- If `optional_id == -1`: the 1-bit `has_tag` should be 0

#### ReadU32Packed(optional_id) — Unsigned packed integer
1. DebugIdCheck(optional_id)
2. ReadBool → `nonzero`. If 0, return 0.
3. ReadBits(5) → `lzc` (leading zero count)
4. ReadBits(32 - lzc) → value
5. Return value

#### ReadI32Packed(optional_id) — Signed packed integer
1. DebugIdCheck(optional_id)
2. ReadBool → `nonzero`. If 0, return 0.
3. ReadBool → `sign` (1 = negative)
4. ReadBits(5) → `lzc`
5. ReadBits(32 - lzc) → magnitude
6. Return `-magnitude` if sign else `magnitude`

#### ReadString(optional_id)
1. DebugIdCheck(optional_id)
2. ReadU32Packed(-1) → string length
3. If length == 0, return empty string
4. ReadBits(4) → char_bit_width
5. ReadBits(8) → base_char
6. For each character: ReadBits(char_bit_width) + base_char → character
7. Return assembled string

### Encoding I32Packed (for writing)

```python
def encode_i32_packed(value):
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
```

## Chunk Structure

### Chunk Header
1. ReadBool → `has_parent`
2. If has_parent: ReadBits(32) → parent tag (FourCC, little-endian)
3. ReadU32Packed(-1) → chunk tag (FourCC)
4. ReadU32Packed(-1) → version
5. ReadU32Packed(-1) → subversion
6. ReadBits(32) → data size in bits

Data immediately follows the header. After the data, there's a **chunk end marker**: DebugIdCheck with the parent group's end tag (33 bits: 1 + 32).

### Chunk Hierarchy

FourCC tags are stored little-endian. "parw" in the file = tag value `0x77726170`.

```
parw (wrap) — parent=sw3c, ver=1
├── daeh (head) — parent=s4ci, ver=8
│   └── [head data: 1202 bits]
├── emag (game) — parent=s4ci, ver=1
│   └── trts (strt) — parent=s43c, ver=73
│       ├── cser (resc) — parent=scsg, ver=1  ← RESOURCES
│       ├── psjv — parent=scsg, ver=1
│       ├── tces (sect) — parent=scsg, ver=1
│       ├── tsnc (cnst) — parent=scsg, ver=5  ← construction/buildings
│       ├── dpsr (rspd) ×58 — parent=spsr  ← crew/response data
│       ├── oreh (hero) — parent=scsg, ver=3  ← crew/heroes
│       ├── afar — parent=scsg, ver=1
│       ├── tnve (evnt) — parent=scsg, ver=1  ← events
│       ├── lcyc (cycl) — parent=scsg, ver=2  ← cycles
│       ├── laid (dial) — parent=scsg, ver=3  ← dialogue
│       ├── seuq (ques) — parent=scsg, ver=4  ← quests
│       ├── tlba (ablt) — parent=scsg, ver=2  ← abilities
│       ├── atem (meta) — parent=scsg, ver=2  ← metadata
│       └── bsiu (uisb) — parent=scsg, ver=1  ← UI state
└── enod (done) — parent=s4ci, ver=1
```

### Navigation Path to Resources

```
bit 128: file header ends, parw chunk header starts
→ read parw header
→ read daeh header → skip daeh data (size bits) + 33-bit end marker
→ read emag header (enter emag)
→ read trts header (enter trts)
→ read cser header (enter cser) ← resources data starts here
```

## Resources Chunk (cser) Internal Format

The cser data contains multiple sections. The resources section uses these tagged reads:

### Resource Items Array
1. `ReadI32Packed("rict")` → count of resource items (tag `0x74636972`)
2. For each item:
   - `ReadU32Packed(-1)` → index (always 1)
   - `ReadString("rinm")` → resource name (tag `0x6d6e6972`)
   - `ReadI32Packed(-1)` → quantity
   - `ReadBoolWrapped(-1)` → flag (0 = base resource, 1 = item/recipe)

### Resource Requirements Array
3. `ReadI32Packed("rrqc")` → count (tag `0x63717272`)

### Crafting Materials Array
4. `ReadI32Packed("rtmc")` → count (tag `0x636d7472`)

### Known Resource Names and Typical Values

**Base resources (flag=0):**
Crew, Energy, Food, Deuterium, Dilithium, Duranium, Tritanium, Morale, MoraleMax, Cycles, CrewAssigned, Happiness (can be negative), LivingSpace, WorkTeams, WorkTeamsAssigned, SciencePoints, BioNeuralGelPack, BorgNanites, Batteries, Hull, ThreatLevel

**Items/recipes (flag=1):**
`Items.Item.ResearchPoints`, `Items.Item.Item_Torpedo`, `Items.Item.Collectabe_Data_Package`, and many crafting/combat/story items.

## Modifying Save Files

### Process
1. Base64 decode the .sav file
2. Parse bit stream to locate target values (navigate chunk headers to cser)
3. Record bit positions of each quantity value
4. Build new bit stream with modified values (bit lengths may change)
5. Shift subsequent bits to accommodate size changes
6. Update chunk size fields (cser, trts, emag, parw) by adding the total bit delta
7. Update file_size at bytes [0:4]
8. Recompute hash over data[16:] and write at bytes [4:8]
9. Base64 encode and write

### Important: Variable-Length Encoding

Packed integers use fewer bits for smaller values. Changing a value from 728 (17 bits) to 99999 (24 bits) requires shifting all subsequent bits by +7. You cannot simply overwrite in place unless the new value fits in the same number of bits.

**Max value for N packed bits:** `(2^(N-7)) - 1` for positive values (N >= 8).

### Bit Shift Reconstruction

When modifying values that change size:
1. Sort all modifications by bit position
2. Copy bits from original up to each modification point
3. Write new encoded value (potentially different length)
4. Continue copying from after the old value
5. After all patches, copy remaining bits
6. Update all 4 chunk size fields (add total bit delta to each)

## Game Binary Reference

**PE Info:** Image base `0x140000000`, .text VA=`0x1000` RawOff=`0x600`

### Key Functions (by RVA)

| RVA | Size | Function |
|-----|------|----------|
| `0x04f9cd90` | 663 | Serialization wrapper (calls OpenForWriting with debug=1) |
| `0x04f9f980` | ~400 | OpenForWriting (writes 16-byte header) |
| `0x04f80b50` | 1811 | serialize_1811 (main save serializer, chunk dispatch) |
| `0x04f98070` | ~1600 | Resources chunk serializer (cser handler) |
| `0x04fa5070` | ~600 | WriteChunkStart |
| `0x04fa5f40` | ~260 | WriteU32Packed |
| `0x04fa5de0` | ~193 | WriteU32 (raw bits) |
| `0x04fa4d30` | 70 | WriteBool |
| `0x04f82980` | ~480 | FinishWriting (patches file_size + hash) |
| `0x04f788a0` | ~260 | ComputeHash (CRC32 with init 0x61635263) |
| `0x04f98800` | ~170 | ReadI32Packed (signed) |
| `0x04f98ae0` | ~160 | ReadU32Packed (unsigned) |
| `0x04f98a10` | ~180 | ReadBits |
| `0x04f92890` | ~160 | ReadBool |
| `0x04f988c0` | ~320 | ReadString |
| `0x04f929c0` | ~360 | ReadChunkEnd |
| `0x04f7bed0` | ~180 | DebugIdCheck |
| `0x04f92940` | ~120 | ReadBoolWrapped |

| `0x04f98b90` | ~140 | ReadU64Packed (two U32Packed as 64-bit: lo \| hi<<32) |
| `0x04f95230` | ~130 | ReadFloat (DebugIdCheck + ReadU32Packed, interpret as IEEE 754) |
| `0x04f94180` | ~190 | ReadClassName (ReadString + FName lookup) |
| `0x04f92b30` | ~210 | ReadChunkStart (DebugIdCheck + 3×U32Packed + ReadBits(32) size) |
| `0x04f94ae0` | ~600 | ReadModuleData (complex module reader for tsnc) |
| `0x04f92c40` | ~2500 | tsnc handler (construction chunk serializer) |

### Chunk Dispatch Table (in serialize_1811)

Tags compared in the dispatch switch: `enod`, `evnt`, `hero`, `cser`, `rafa`, `ques`, `sect`, `uisb`, `psjv`, with handlers calling specialized serializer functions.

## Construction Chunk (tsnc) Internal Format

The tsnc chunk (version 5) stores ship construction data including modules, rooms, and hull integrity.

### tsnc Structure

```
ReadI32Packed("rmct")  → module count (e.g. 1584)
  For each module:
    func_94ae0("rmdt")  → complex module data:
      ReadU32Packed("rmdt" tag)
      ReadU32Packed(-1)
      ReadU64Packed(-1)      — class hash (two U32Packed as 64-bit)
      ReadClassName(-1)      — class name string
      ReadI32Packed(-1)      — sub-item count
        For each sub-item: ReadU32Packed(-1), ReadString(-1)
    ReadU32Packed("rsdt")
    ReadU32Packed(-1)
    ReadU64Packed(-1)
    ReadClassName(-1)
    ReadU32Packed(-1)
    ReadBoolWrapped(-1) × 2

ReadI32Packed("rspc")  → space piece count (e.g. 58)
  For each space:
    ReadChunkStart("rsps")  — sub-chunk with version/size
      ReadU32Packed(-1)     — room/module ID
      ReadU32Packed(-1)     — always 1
      ReadU64Packed(-1)     — class hash
      ReadClassName(-1)     — e.g. "ESTVRoomType::LifeSupport"
      [variable room-specific data]
    ReadChunkEnd("rspe")

ReadI32Packed("shct")  → ship tech count (0 in all observed saves)

ReadU32Packed(-1)      → 0 (unknown field)
ReadBool               → 1 (version >= 2 flag)
ReadBool               → 1 (version >= 4 flag)
[4 bits padding/unknown]
ReadBits(32)           → **HULL INTEGRITY** as raw IEEE 754 float
[3 trailing bits]
```

### Known Room Types (from rspc space pieces)

ESTVRoomType::LifeSupport, MainDeflector, TorpedoLaunchBay, MainEngineering, AeroshuttleBay, Shuttlebay, CargoBay, LargeHydroponicsBay, WasteDeassembler, BioLab, CrewQuarters, Sickbay, MessHall

### Hull Integrity

- **Current hull integrity** is stored as a **raw 32-bit IEEE 754 float** at the end of the tsnc chunk
- **Location**: Exactly +8 bits after the `shct` I32Packed count read position
- **Max hull capacity** (e.g. 490) is **computed from installed ship modules** — NOT stored in the save file
- The `Hull` resource in cser is always 0 across all saves and is unrelated to displayed hull
- Hull damage = max hull − current hull integrity (e.g. 490 − 12 = 478 damage)

### Modifying Hull Integrity

This is a **simple in-place 32-bit replacement** — no bit shifting needed:

1. Navigate to tsnc chunk: `parw → daeh(skip) → emag → trts → cser(skip) → psjv(skip) → tces(skip) → tsnc`
2. Scan tsnc data for the `shct` tag (`0x73686374`) as a DebugIdCheck pattern (bit=1, then 32-bit tag)
3. Read `I32Packed("shct")` to consume the tag
4. Current position = `after_shct`. Hull float is at `after_shct + 8`
5. Read/write 32 raw bits as IEEE 754 float
6. Recompute CRC hash and write back

```python
# Example: Set hull to full (490.0)
hull_float_pos = after_shct + 8
new_hull = 490.0
new_raw = struct.unpack('<I', struct.pack('<f', new_hull))[0]
write_bits(data, hull_float_pos, new_raw, 32)
# Then recompute hash and base64 encode
```

### Hull-Related Classes in Game Binary

Key classes found via string search:
- `STVHullDamageResistanceModel` — damage resistance calculations
- `STVModifier_AdjustMaxHull` — modifier that adjusts max hull
- `STVModifier_ChangeHullIntegrity` — modifier that changes current hull
- `STVRoomHull` / `STVRoomHullButton` — hull repair UI
- `BaseMaxHullIntegrity` / `AdjustmentsToMaxHullIntegrity` / `MaxHullIntegrityAtLastUpdate` — ship properties
- `HullScore` / `HullValue` / `TotalSubStat_HullScore` — tech-level contributions
- `HullRepairWorkTeams` / `PerWorkTeamHullRepairPerCycle` — repair mechanics
- `ESTVGameOverReason::HullZero` — game over when hull reaches 0
