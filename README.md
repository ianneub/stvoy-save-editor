# stvoy-save-editor

Save game editor for [**Star Trek Voyager: Across the Unknown**](https://store.steampowered.com/app/2643390/Star_Trek_Voyager__Across_the_Unknown/) by [Gamexcite / Daedalic Entertainment](https://www.stvatu.com). Read and modify resources, hull integrity, and more.

No save editor existed for this game — the save format is a proprietary bit-packed binary (C3Serialize/FunIO), not standard Unreal Engine GVAS. This toolkit reverse-engineers that format so you can tweak your saves.

## Requirements

- Python 3.8+
- No external dependencies

## Finding Your Saves

Save files are located at:

```
C:\Users\<YourName>\AppData\Local\STVoyager\Saved\SaveGames\<SteamID>\
```

Files are named `00_GX_STV_SaveGame_NNNN.sav`.

## Quick Start

```bash
# View save file info and chunk structure
python stvoy_toolkit.py info path/to/save.sav

# List all resources and their current values
python stvoy_toolkit.py resources path/to/save.sav

# Check current hull integrity
python stvoy_toolkit.py hull path/to/save.sav

# Set hull integrity to 490
python stvoy_toolkit.py set-hull path/to/save.sav 490.0

# Set a resource value (e.g. give yourself 10000 Dilithium)
python stvoy_toolkit.py set-resource path/to/save.sav Dilithium 10000
```

## What You Can Change

### Resources

Base resources like `Crew`, `Energy`, `Food`, `Deuterium`, `Dilithium`, `Duranium`, `Tritanium`, `Morale`, `SciencePoints`, `BioNeuralGelPack`, `BorgNanites`, `Batteries`, and more.

Items and recipes like `Items.Item.ResearchPoints`, `Items.Item.Item_Torpedo`, etc.

Run `python stvoy_toolkit.py resources <save>` to see the full list for your save.

### Hull Integrity

Current hull can be set to any float value. Note that **max hull capacity is computed from your installed ship modules** and is not stored in the save file, so you can't increase it directly — but you can repair to full.

## How It Works

Save files are Base64-encoded. Under that is a 16-byte header (file size, CRC32 hash, version, debug flag) followed by a bit-packed binary stream using C3Serialize/FunIO encoding. Values are stored as variable-length packed integers (smaller numbers use fewer bits), so modifying a value may shift everything after it. The toolkit handles all of this automatically — including bit-stream reconstruction and hash recomputation.

## Backup & Safety

The toolkit automatically creates a `.backup` copy of your save the first time you modify it. **Keep your own backups too** — copy your entire `SaveGames` folder before experimenting.

## Limitations

- **Max hull can't be changed** — it's computed from installed ship modules, not stored in the save
- **Not all chunks are decoded** — only resources (`cser`) and hull integrity (`tsnc`) can be edited currently
- Chunk structure for crew (`hero`, `dpsr`), quests (`ques`), events (`evnt`), etc. is mapped but not yet editable

## Disclaimer

This project is not affiliated with or endorsed by Stellar Cartography, Strange New Games, or any rights holders of Star Trek Voyager: Across the Unknown. Use at your own risk.

## License

MIT
