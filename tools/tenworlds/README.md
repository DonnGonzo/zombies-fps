# Ten Worlds pack

Ivan's "Ten Worlds" Unity environment pack (ten 14 m themed rooms as glTF, with 256 px texture sets). The
zip itself (about 100 MB) is too big for git and stays in the project files; only the pieces the map uses are
converted into `Assets/_Project/Resources/CC0/TenWorlds`.

Used, from the medieval-fitting rooms (the hospital, quarantine, lecture hall, reactor control, parkour and
mosque rooms don't suit the map's style):

| Room | Pieces | Where in the map |
|---|---|---|
| Gothic Church Chapel | ChurchPew, AltarDais, PipeOrgan, LanternSuspension (chandelier) | Chapel |
| Forbidden University Archive | one bay of the ArchiveStacks (Bookcase), ReadingTable, CelestialGlobe | Archives |
| Arcane Alchemy Catacomb | ArcanePylon, Worktable, OccultTapestry | Lantern Core lab, Chapel, Archives |
| Overgrown Garden Atrium | GardenBench, FountainColumn, RaisedPlanter | Courtyard, Greenhouse |

To redo them from the unzipped pack (`Models/` and `Textures/`):

```
O=Assets/_Project/Resources/CC0/TenWorlds
BACK=+z python3 split_props.py Models/05_Gothic_Church_Chapel_Props.glb --export $O ChurchPew
python3 split_props.py Models/05_Gothic_Church_Chapel_Props.glb --export $O AltarDais,LanternSuspension
SLACK=0.15 python3 split_props.py Models/05_Gothic_Church_Chapel_Props.glb --export $O PipeOrgan
python3 split_props.py Models/08_Forbidden_University_Archive_Props.glb --export $O ReadingTable,CelestialGlobe
CLIP=z,-4.6,-3.8 BACK=-z python3 split_props.py Models/08_Forbidden_University_Archive_Props.glb --export $O ArchiveStacks   # then rename to Bookcase.bytes
python3 split_props.py Models/10_Arcane_Alchemy_Catacomb_Props.glb --export $O ArcanePylon,OccultTapestry
BACK=+z python3 split_props.py Models/10_Arcane_Alchemy_Catacomb_Props.glb --export $O Worktable
python3 split_props.py Models/01_Overgrown_Garden_Atrium_Props.glb --export $O GardenBench,FountainColumn,RaisedPlanter
python3 pack_textures.py Textures $O
```

Each model's size is written into the `PackItem` table in `World/ZoneMap.Rooms.cs`, so where things go never
depends on the files being there; update it if a model is re-exported at a different size.
