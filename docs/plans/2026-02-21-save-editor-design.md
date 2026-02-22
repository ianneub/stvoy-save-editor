# STV Save Editor — Design Document

## Overview

A Windows desktop application (C# / WPF / .NET 8) that lets users edit save files for Star Trek Voyager: Across the Unknown. Ports the existing Python toolkit logic to C# and wraps it in a graphical interface.

## Goals

- Let users edit resource quantities and hull integrity without command-line tools
- Auto-detect the Steam save game folder
- Create automatic backups before any modification
- Distribute as a single .exe (self-contained publish)

## Architecture

Single C# WPF project with MVVM pattern. All save parsing/writing logic ported from the Python toolkit.

### Project Structure

```
STVSaveEditor/
├── STVSaveEditor.sln
├── STVSaveEditor/
│   ├── STVSaveEditor.csproj       (.NET 8, WPF, win-x64)
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── ViewModels/
│   │   └── MainViewModel.cs       (UI state, commands)
│   ├── Models/
│   │   ├── SaveFile.cs            (load/save/backup, base64 layer)
│   │   ├── BitReader.cs           (bit-stream reader, LSB-first)
│   │   ├── BitWriter.cs           (bit-stream reconstruction)
│   │   ├── PackedEncoding.cs      (I32/U32 packed encode/decode)
│   │   ├── Crc32.cs               (CRC32 with custom init 0x61635263)
│   │   ├── ChunkNavigator.cs      (navigate chunk hierarchy to cser/tsnc)
│   │   ├── ResourceEntry.cs       (resource name, quantity, bit position, flag)
│   │   └── HullData.cs            (hull integrity float + bit position)
│   └── Helpers/
│       └── SaveFolderDetector.cs  (find Steam save directory)
```

### Data Flow

1. App launches → auto-detect or browse for save folder
2. List `.sav` files in a dropdown selector
3. User selects a file → parse resources + hull → populate UI
4. User edits values in grouped card layout
5. User clicks Save → backup original → reconstruct bit stream → recompute CRC → base64 encode → write file

## UI Layout

### File Selection

Top bar with a dropdown of detected `.sav` files and a Browse button to override.

### Hull Integrity

Single editable field showing the current hull float value.

### Resources — Grouped Cards

Two groups separated visually:

- **Base Resources** (flag=0): Crew, Energy, Food, Deuterium, etc. displayed as labeled cards in a wrapping grid (2-3 per row).
- **Items** (flag=1): Items.Item.ResearchPoints, Items.Item.Item_Torpedo, etc. displayed similarly with cleaner display names (strip the `Items.Item.` prefix).

Each card shows the resource name and an editable numeric field.

### Status Bar

Bottom bar with status messages (Ready, Saving..., Saved successfully, Error details).

## Save Engine — Ported Logic

### CRC32 (Crc32.cs)

Standard CRC32 table with custom initial value `0x61635263`. Computed over `data[16..]`.

### BitReader (BitReader.cs)

Direct port of Python `BitReader`. Reads bits LSB-first within each byte. Methods:
- `ReadBool()`, `ReadBits(count)`, `DebugSkip(oid)`
- `ReadU32Packed(oid)`, `ReadI32Packed(oid)`, `ReadU64Packed(oid)`
- `ReadString(oid)`, `ReadFloat(oid)`, `ReadBoolWrapped(oid)`
- `ReadChunkHeader()`, `SkipChunk(chunk)`, `ReadChunkStart(oid)`

### PackedEncoding (PackedEncoding.cs)

Static methods `EncodeI32Packed(value)` and `EncodeU32Packed(value)` returning `List<int>` of bits. Same algorithm as the Python version.

### ChunkNavigator (ChunkNavigator.cs)

- `NavigateToCser(data)` → reader positioned at cser data start
- `NavigateToTsnc(data)` → reader positioned at tsnc data start
- `FindShctPosition(data, tsnc)` → bit position after shct tag
- `GetChunkSizePositions(data)` → list of 4 size field bit positions

### Resource Reading/Writing

- `ReadResources(data)` → list of `ResourceEntry` (name, quantity, bit position, flag)
- `ModifyResources(data, modifications)` → new byte array with patched bit stream
- `ReadHullIntegrity(data)` → (float value, bit position)
- `SetHullIntegrity(data, newValue)` → in-place 32-bit write

### SaveFile (SaveFile.cs)

- `Load(path)` → base64 decode, validate file_size header
- `Save(data, path)` → recompute CRC, base64 encode, write
- `MakeBackup(path)` → copy to `.backup` if not exists

## Auto-Detect Save Folder (SaveFolderDetector.cs)

Search strategy:
1. Check common Steam library paths: `C:\Program Files (x86)\Steam\steamapps\common\`
2. Parse `libraryfolders.vdf` for additional Steam library locations
3. Look for `STVoyager/Saved/SaveGames/` under each library
4. List subdirectories (Steam user IDs) and find `.sav` files
5. Fall back to manual browse if not found

## Backup Strategy

Before any write operation, copy the original file to `<filename>.backup`. Only create the backup if a `.backup` file doesn't already exist (same as Python toolkit behavior).

## Non-Goals

- No file info/debug view (chunk structure inspection)
- No construction/module editing
- No cross-platform support (Windows-only WPF)
- No auto-update mechanism
