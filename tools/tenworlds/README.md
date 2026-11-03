# Ten Worlds pack

Ivan's "Ten Worlds" Unity environment pack (ten 14 m themed rooms as glTF, with 256 px texture sets). The
zip itself (about 100 MB) is too big for git and stays in the project files; only the pieces the map uses are
converted into `Assets/_Project/Resources/CC0/TenWorlds`.

Used, from every room but the parkour one (pieces from the hospital, quarantine, lecture hall, reactor and
mosque rooms are restyled medieval: their brushed steel draws as rusted iron, white paint as pale wood and
concrete as limestone, see `TextureKey` in `World/ZoneMap.Worlds.cs`):

| Room | Pieces | Where in the map |
|---|---|---|
| Gothic Church Chapel | ChurchPew, AltarDais, PipeOrgan, LanternSuspension (chandelier) | Chapel |
| Forbidden University Archive | one bay of the ArchiveStacks (Bookcase), ReadingTable, CelestialGlobe | Archives |
| Arcane Alchemy Catacomb | ArcanePylon, Worktable, OccultTapestry | Lantern Core lab, Chapel, Archives |
| Overgrown Garden Atrium | GardenBench, FountainColumn, RaisedPlanter | Courtyard, Greenhouse |
| Mosque Prayer Hall | ChandelierChain (TempleChandelier), MashrabiyaFrame (LatticeScreen) | Sunken Temple |
| University Lecture Hall | Desktop (WritingDesk), Seat (Stool), ProfessorStage (LecternStage) | Scriptorium |
| Neon Reactor Control | CoreLowerDais (FurnaceCore) | Alchemist's Furnace |
| Quarantine Containment Block | CellRear (GaolCell) | Gaol |
| Abandoned Hospital Ward | BedFrame (SickBed), MedicalCabinet (ApothecaryCabinet) | Plague Ward |

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
python3 split_props.py Models/04_Mosque_Prayer_Hall_Props.glb --export $O ChandelierChain,MashrabiyaFrame
BACK=-z python3 split_props.py Models/07_University_Lecture_Hall_Props.glb --export $O Desktop,Seat,ProfessorStage
python3 split_props.py Models/02_Neon_Reactor_Control_Props.glb --export $O CoreLowerDais
python3 split_props.py Models/09_Quarantine_Containment_Block_Props.glb --export $O CellRear
SLACK=0.15 python3 split_props.py Models/06_Abandoned_Hospital_Ward_Props.glb --export $O BedFrame,MedicalCabinet
# then rename each to the name in brackets in the table above
python3 pack_textures.py Textures $O
rm $O/Textures/{brushed_steel,white_paint,concrete,clean_glass}_*   # drawn with other textures, see above
```

The pack's GLBs carry no normals; `split_props.py` works out flat ones, so a re-export never comes out black.

Each model's size is written into the `PackItem` table in `World/ZoneMap.Rooms.cs`, so where things go never
depends on the files being there; update it if a model is re-exported at a different size.
