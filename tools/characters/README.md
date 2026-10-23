# Character models

`pack_characters.py` turns Ivan's Quaternius packs into `Assets/_Project/Resources/Characters/<Name>/`
(a binary skeleton + skinned mesh, and 1024 px JPG textures). Unzip both packs into one folder and run:

    python3 -m pip install numpy pillow
    python3 tools/characters/pack_characters.py <folder with the unzipped packs>

| Character | Pack | Licence | Used for |
|---|---|---|---|
| Imp (3 colours) | Bestiary - Dungeon Monsters Kit (Standard) | Quaternius Asset License | every Brute |
| Puglin (3 colours) | Bestiary - Dungeon Monsters Kit (Standard) | Quaternius Asset License | half the Runners |
| Ranger (green, brown, Watch blue) | Modular Character Outfits - Fantasy (Standard) | CC0 | Sergeant Brannock |

The packs ship without animations. Monsters are played by the zombie's hidden Mixamo rig and copied across
(`Characters/CharacterSkin.cs`, `Retarget`); Brannock walks and runs procedurally (`Characters/NpcGait.cs`).
The outfit pack has no heads (they come with Quaternius' Universal Base Characters), so Brannock gets a simple
face in code (`Characters/Npcs.cs`).

`gltfio.py` is a small glTF reader the packer uses.
