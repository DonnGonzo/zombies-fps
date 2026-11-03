# TEN ARMS + AEGIS | Functional Unity FPS Weapon Pack (v1.0)

**14 actual exported 3D model files:** 10 primary game items (7 grounded conventional wall-buy weapons, 1 deployable shield, 2 sci-fi weapons), plus 4 separate collectible shield assembly parts.

**Game functionality provided as editable C# source:** FPS movement and aiming, weapon switching, wall-buy spending, reload and ammo reserves, hitscan damage, shotgun spread, plasma projectiles, area pulse bursts, target health and credits, collect-four-part shield build, held shield blocking, deployable shield cover, enemy prototype that can attack the shield, and an on-screen test HUD.

> These are fictional video-game items, not technical weapon blueprints. The hard-surface models are procedural game-resolution assets with PBR material parameters, **not** hand-sculpted photoreal AAA models. Code was inspected and mesh files loaded successfully with a GLB validator; a complete Unity editor/play-mode test has **not** been possible here. Unity 2022.3 LTS or Unity 6 recommended.

## Download contents

- `Assets/TenWeapons/Models/OBJ/`: Unity-native import path. **All OBJ and same-named MTL files must stay together.**
- `Assets/TenWeapons/Scripts/`: `TW_ArsenalPlayer.cs`, stats, health/damage, area/pulse effects, wall-buy, shield, prototype zombie behavior.
- `Assets/TenWeapons/Editor/TW_ArsenalEditor.cs`: Editor menu to build 24 materials, 14 prefabs and a fully playable demo range scene.
- `Models/GLB/`: GLB with embedded glTF PBR material factors. Import with glTFast/UniGLTF if preferred. No .FBX format is included.
- `Source/generate_models.py`: reproducible procedural model source (Python + trimesh + numpy + Pillow).
- `Previews/ALL_10_Asset_Overview.jpg`: preview sheet of all 10 main models.
- `Docs/`: step-by-step install and gameplay/integration guide.

## First run in Unity (no GLB importer needed)

1. **Back up your Unity project.** Open the existing project in Unity.
2. Unzip this pack. Copy the included **`Assets/TenWeapons`** folder into your project's `Assets` folder. Do not copy the whole ZIP as an asset.
3. Let Unity import the `.obj` meshes, `.mtl` material names, and `.cs` source.
4. For legacy keyboard/mouse controls, set **Edit > Project Settings > Player > Active Input Handling = Both** (or Input Manager (Old)). If Unity asks to restart, do it. The demo uses `UnityEngine.Input`, not the newer Input System.
5. At top toolbar, choose **TenWeapons > 4 - Build Everything (Recommended)**. It creates prefabs at `Assets/TenWeapons/Prefabs`, materials at `Assets/TenWeapons/Materials`, and a demo scene at `Assets/TenWeapons/Scenes/TenWeapons_PlayableTestRange.unity`.
6. Open the generated scene and press **Play**. If your project already has player/movement scripts, test in this standalone range first, then integrate one system at a time.
7. Move with WASD, look with mouse, aim with right-click, fire with left-click, reload with R, buy/collect using E. Press **1-7** for conventional guns, **8** for Helix, **9** for Pulse, **0** for assembled Aegis shield. The FPS mouse cursor unlocks with Esc.
8. Purchase guns by approaching the back wall displays. Collect the **four shield parts** at the center pedestals, then press 0 to equip and left-click to place the shield as solid cover. The example credits/HP HUD and moving prototype zombie let you test damage and blocking.

## Models (local geometry orientation)

- All weapon models: **Y up**, barrel points **+Z**, units = meters; grip near negative Y and stock near negative Z.
- Shield: centered at local X=0, stands from approximately local Y=0 to local Y=1.8m; its panel faces local positive Z.
- The editor demo creates correct display orientation, camera view model transforms, material assets and simple primitive colliders.
- All `.glb` models have separate named mesh components and PBR material factors, but no authored/baked photo-based albedo/normal map textures or skeletal reload animations. The special effects are realtime Unity C# VFX, not embedded animations.

## Models and approximate game balance

| Hotkey | Primary Model | Action | Magazine | Damage per hit | Example cost |
|---|---|---|---:|---:|---:|
| 1 | 01 Harker-9 Service Pistol | Semi-auto hitscan | 15 | 27 | $450 |
| 2 | 02 Sable-9 Compact SMG | Full-auto hitscan | 32 | 16 | $1,000 |
| 3 | 03 Atlas-5 Tactical Carbine | Full-auto hitscan | 30 | 24 | $1,450 |
| 4 | 04 Kodiak-7 Battle Rifle | Semi-auto hitscan | 20 | 43 | $1,850 |
| 5 | 05 Riot-12 Pump Shotgun | 10-pellet spread | 7 | 13 per pellet | $1,250 |
| 6 | 06 Specter-308 Precision Rifle | High-damage hitscan | 6 | 110 | $2,300 |
| 7 | 07 Grinder Light Machine Gun | Full-auto hitscan | 90 | 20 | $2,750 |
| 0 | 08 Aegis Shield | Collect 4 parts; block and deploy cover | — | — | Parts |
| 8 | 09 Helix Plasma Carbine | Moving cyan plasma projectiles | 40 | 40 | $3,200 |
| 9 | 10 Resonance Pulse Projector | AoE purple energy blast | 8 | 95 max | $4,200 |

All prices, cooldowns, HP values, fire rates and ammo caps are editable in `TW_Types.cs`. The first weapon is granted automatically; starting demo credits are $8,000.

## Aegis shield parts

- `08A_Aegis_Shield_Armor_Plate.glb/.obj`
- `08B_Aegis_Shield_View_Module.glb/.obj`
- `08C_Aegis_Shield_Power_Core.glb/.obj`
- `08D_Aegis_Shield_Brace_Kits.glb/.obj`

Pick up each unique part (four in total) with E in the playable range. This grants and assembles the deployable shield item. Right now assembly is a gameplay unlock (no in-world procedural construction animation); the four pickup models and completed shield model are separate.

## Important integration cautions

- This prototype uses the **old input API**. If your project uses only the new Input System, enable Both or port the input calls.
- Unity prefab generator **adds basic box colliders** for pickups/wall guns, and shield gameplay collider; adjust collider shape for your map.
- Runtime effect orbs and prototype enemies use primitives for gameplay tests; replace them with your game's own FX and zombie AI.
- `TW_ArsenalEditor.cs` must stay under an `Editor/` folder.
- `Models/GLB` is an extra export folder outside the `Assets` folder and does not need to be copied for the no-plugin workflow.
- Model materials are base PBR colors, roughness/metalness and emissive factors, not final AAA UV/PBR textures. You can add worn finishes, normal maps, audio and animation in a later polish pass.

## Dependency reference

**Unity:** No external package needed for native OBJ import and code. Only `UnityEngine`, `UnityEditor` (editor tool), Unity's **legacy Input Manager** controls.

**Python regeneration:** `pip install numpy trimesh Pillow` then `python Source/generate_models.py` from the extracted pack directory (regenerates `Models/OBJ`, `Models/GLB`, previews; copy regenerated OBJ/MTL into `Assets/TenWeapons/Models/OBJ` before pressing Generate Prefabs again).

**GLB import option:** Use a Unity package like **glTFast** if you prefer the optional GLB PBR files. The demo and prefab builder expect `.obj` files unless you edit the importer script to switch sources.
