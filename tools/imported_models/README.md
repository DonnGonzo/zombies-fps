# Supplied model pipeline

The committed source art and generated prefabs are ready for Unity. Rebuilding is only necessary when changing source art or import rules.

1. Install Python with Pillow and WinRAR (the truck pack contains a nested RAR).
2. Run `python tools/imported_models/prepare.py D:\GitHub\Models` from the repository root. Archives remain untouched; extraction goes into ignored `Temp/model-import/source`.
3. Run Blender 4.2 with `--background --python tools/imported_models/convert_desert_eagle.py` to export the supplied Blender-only pistol.
4. Run `python tools/imported_models/materials.py` to prepare Standard shader textures, including metallic/smoothness masks and the truck's corrected normal map.
5. In Unity, select **Zombies FPS > Imported Models > Build prefabs and previews**. This normalizes orientation and scale, merges static geometry, seats the prison door and pistol magazine, bakes magazine movement, and preserves the hazmat skeleton and clips.
6. Run the `ZombiesFPS.Tests.EditMode` tests in Test Runner. ImportedModelTests covers all 14 prefabs, seven weapon builds, source animation assets and stable gun catalog IDs.

Source textures are limited to 2048 pixels. Generated map meshes are colliderless: the map retains its gameplay collision geometry. Original archive hashes and model paths are recorded in `sources.json`; material mappings are in `Assets/_Project/ImportedModels/materials.json`.

For automated use of an already-open Unity editor, atomically rename a text file containing `build`, `tests` or `review` to `Temp/model-import/request.txt`. Results and isolated room previews are written under `Temp/model-import`. The editor accepts requests only while idle outside Play Mode.
