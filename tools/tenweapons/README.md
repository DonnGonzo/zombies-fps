# TenWeapons pack

Ivan's "TEN ARMS + AEGIS" weapon pack: nine guns, the Aegis Shield and its four build parts. `pack/OBJ` is the
pack's Unity-ready OBJ + MTL export as delivered, with its `README.md` and `generate_models.py`. The pack's own C#
(a standalone demo using the old input API) isn't used; the game has its own guns, buys and shield.

`pack_guns.py` converts the OBJs into `Assets/_Project/Resources/Guns` (PRP1 meshes plus `guns.txt`), which
`Weapons/GunArt.cs` loads:

    python3 pack_guns.py

Each gun is scaled to a view-model length, made 25% narrower, and moved so its sight line is on
`GunModels.SightY`; optic housings and scope tubes go in a separate `_optic` mesh that hides while aiming.

| Game gun | Pack model |
|---|---|
| Rook-9 Carbine (starter) | 03 Atlas-5 Tactical Carbine |
| Hatchet .44 | 01 Harker-9 Service Pistol |
| Wren-7 SMG | 02 Sable-9 Compact SMG |
| Kestrel AR | 04 Kodiak-7 Battle Rifle |
| Brennan DMR | 06 Specter-308, shorter and in its own colours |
| Vanta .50 Sniper | 06 Specter-308 Sniper Rifle |
| Mauler 12 Shotgun | 05 Riot-12 Pump Shotgun |
| Talon LMG | 07 Grinder Light Machine Gun |
| Ion Lance | 09 Helix Plasma Carbine |
| Scrap Cannon | 10 Resonance Pulse Projector |
| Aegis Shield and its parts | 08 Aegis Shield, 08A-D armour plate, view module, power core, brace kit |
