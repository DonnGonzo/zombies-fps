# Dark Magic Lab Room Pack

A Unity-ready environment pack for a **medieval insane-scientist / dark-magic laboratory** built to accompany the **Reactor_01** asset.

## Included

### Combined scene
- `Models/DarkMagicLab_FullRoom.glb` — pre-built room with room shell, props, and spell aura already placed.

### Individual assets
- `RoomShell_MedievalLab` — floor, walls, ceiling, pillars, banners, central reactor dais, rune floor plates
- `Ritual_Altar`
- `Sacrifice_Slab`
- `ChainCluster_CeilingHooks`
- `JarSet_BodyParts`
- `MonsterHead_Trophy`
- `CandleCluster_Ritual`
- `BonePile_Skulls`
- `AlchemyDesk`
- `WallTorch_Gothic`
- `SpellAura_Activated`

Each individual asset is exported as `.glb`, `.obj`, and `.mtl`.

### Optional companion files
- `Optional_Reactor/Reactor_01.*` copied in for convenience if available.

### Unity scripts
- `Scripts/DarkMagicAura.cs` — activation / pulsing / spinning aura effect
- `Scripts/RitualFlicker.cs` — light flicker for torches and candles

## Style / Scale
- Theme: gritty realistic fantasy-horror / occult science room
- Scale: modeled in **meters**
- Room footprint: roughly **14m x 14m**, wall height approx **5.2m**

## Recommended Unity import workflow
1. Extract the ZIP.
2. In Unity, create folders like `Assets/DarkMagicLab/Models` and `Assets/DarkMagicLab/Scripts`.
3. Drag in either `DarkMagicLab_FullRoom.glb` for the full assembled room or the individual `.obj` / `.glb` assets if you want to build the room manually.
4. If Unity doesn’t import `.glb` natively in your setup, install **glTFast** from Package Manager and import the `.glb` files that way.
5. Drag `Scripts/DarkMagicAura.cs` and `Scripts/RitualFlicker.cs` into your project.
6. For the **spell activation moment**: place `SpellAura_Activated` at the trigger point, add a **Point Light**, attach `DarkMagicAura.cs`, set `startActive = false` if it should remain hidden until your event starts, then call `ActivateMoment()` from your trigger/cutscene/script.
7. For candles and torches: add a Point Light near flame meshes and attach `RitualFlicker.cs` to the light object.
8. If you use the optional reactor, place it on the central dais at scene origin.

## Notes
- These are **real procedural 3D models**, not images.
- Materials are lightweight starter materials using base colors, suitable for prototyping and later replacement with PBR textures.
- For best visuals, use bloom on emissive/magical elements and a transparent shader for glass jars.
