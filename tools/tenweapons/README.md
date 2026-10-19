# TenWeapons pack

Ivan's "TEN ARMS + AEGIS" weapon pack: nine guns, the Aegis Shield and its four build parts. `pack/OBJ` is the
pack's Unity-ready OBJ + MTL export as delivered, with its `README.md` and `generate_models.py`. The pack's own C#
(a standalone demo using the old input API) isn't used; the game has its own guns, buys and shield.

`build_guns.py` builds the game's guns, their attachments and the shield into `Assets/_Project/Resources/Guns`
(PRP1 meshes plus `guns.txt`), which `Weapons/GunArt.cs` loads. It needs numpy and trimesh:

    python3 build_guns.py

The pack's OBJs have parts that don't touch (stocks, grips, rails and sights float off the gun), so rather than
converting them, `build_guns.py` rebuilds each model from the shapes, sizes and materials in
`pack/generate_models.py` with every part placed against the one it hangs on. Each gun is scaled to a view-model
length, made 25% narrower, and moved so its sight line is on `GunModels.SightY`.

Every gun also gets its swappable parts as separate meshes (`slug_group.bytes`): `irons` (notch and glowing front
post) or the sniper's `builtin` scope, the standard `mag`, and one fitted model per shop attachment: `reddot`,
`scope`, `extmag`, `fastmag`, `grip`, `supp`, `heavy`, `ammo`, `auto`. The game shows the ones bought for the gun.
`guns.txt` gives each gun's muzzle, bounds, suppressor length and how far a heavy barrel moves the muzzle. New
meshes get a `.meta`; meshes no longer made are deleted.

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
