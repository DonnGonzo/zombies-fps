# Quaternius kits

Ivan's Fantasy Props MegaKit, Sci-Fi Essentials Kit and Modular SciFi MegaKit (CC0, quaternius.com). The zips stay
in the project files; only the pieces the map uses are converted. `World/ZoneMap.Kits.cs` places them, with each
model's bounds written into its `PackItem`, so where things go never depends on the files being there.

To redo them from the unzipped kits (F = Fantasy Props `Exports/glTF`, S = Sci-Fi Essentials `glTF`,
M = Modular SciFi `glTF`, R = `Assets/_Project/Resources/CC0`):

```
python3 pack_gltf.py fp $R/Fantasy $F/<Name>.gltf ...            # floor, wall and table pieces
python3 pack_gltf.py fp $R/Fantasy TURN=90 $F/Banner_1.gltf $F/Banner_2.gltf
python3 pack_gltf.py sf $R/SciFi $S/<Name>.gltf ...
python3 pack_gltf.py sm $R/SciFiModular $M/Columns/... $M/Props/... $M/Platforms/... \
    TURN=90 FLAT=0.12 $M/Walls/WallBand_Straight.gltf ...         # wall panels, front to +z, 12 cm deep
python3 pack_textures.py $R fp=$F,<Fantasy Textures> sf=$S,<SciFi Textures> sm=$M/Walls,$M/Props,$M/Columns,$M/Platforms,<Modular Textures>
```

The models used are the `.bytes` files in those folders. Left out on purpose: the kits' enemies, aliens and guns
(characters and weapons are other work), decals, and the carrot crates (150k vertices for a crate of carrots).
