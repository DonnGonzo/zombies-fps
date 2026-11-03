# Ivan's model packs

`BrokenFurniturePack` and `DarkMagicLabPack` are kept here as delivered (the lab's copy of the reactor is left
out: it's in `tools/reactor`). The game doesn't import these files directly. `pack_obj.py` converts each OBJ
into `Assets/_Project/Resources/CC0/<Pack>/<Model>.bytes`, which `World/ZoneMap.Rooms.cs` places in the rooms:

    python3 pack_obj.py BrokenFurniturePack/Models/Torn_Sofa.obj ../../Assets/_Project/Resources/CC0/BrokenFurniture/Torn_Sofa.bytes

It prints the model's bounds; copy them into the PackItem table in ZoneMap.Rooms.cs if a model changes size.
The lab's RoomShell and full-room scenes aren't used (the map has its own walls), and the packs' scripts are
redone in code (Flicker, SpellCircle in ZoneMap.Rooms.cs).
