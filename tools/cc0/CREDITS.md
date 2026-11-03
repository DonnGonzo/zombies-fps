# CC0 map art

All files in `Assets/_Project/Resources/CC0` come from [Poly Haven](https://polyhaven.com) and are CC0
(public domain): no attribution is required, but credit is given here anyway.

## Surfaces (1k, packed by `pack_surfaces.py`)

| Game surface | Poly Haven texture | Author |
|---|---|---|
| Asphalt (streets) | cobblestone_floor_05 | Rob Tuytel |
| Concrete (walls, stairs) | castle_wall_varriation | Rob Tuytel |
| Brick | medieval_red_brick | Rob Tuytel |
| Tile (floors) | monastery_stone_floor | Amal Kumar |
| Plaster | medieval_wall_01 | Rob Tuytel |
| Wood | old_planks_02 | Rob Tuytel |
| Rust | rust_coarse_01 | Dimitrios Savva, Rico Cilliers |
| Metal | metal_plate_02 | Rob Tuytel |
| Dirt | mud_forest | eye-candy.xyz |
| Grass | sparse_grass | Amal Kumar |

`slug_albedo.jpg` is the diffuse map with half of its ambient occlusion baked in; `slug_nrh.jpg` holds the
OpenGL normal map in red/green and the displacement map in blue.

## Props (glTF 1k, converted by `pack_props.py`, textures at 512)

wine_barrel_01, wooden_crate_01, wooden_crate_02, wooden_bucket_01, WoodenTable_01, kite_shield,
wooden_lantern_01, wicker_basket_02, stone_fire_pit.

## Redoing it

Download a texture's Diffuse, nor_gl, Displacement and AO (1k jpg) into a folder as `slug_diff.jpg`,
`slug_nor.jpg`, `slug_disp.jpg`, `slug_ao.jpg`, then
`python3 pack_surfaces.py <folder> Assets/_Project/Resources/CC0/Surfaces <slug>...`.
For a prop, unzip its 1k glTF and run `python3 pack_props.py <gltf folder> Assets/_Project/Resources/CC0/Props/<slug> <slug> 512`.
New files need a .meta: albedo textures sRGB, `_nrh` linear, `_normal` as a normal map.

## Reactor (not CC0: made for this game)

`tools/reactor` holds Ivan's Reactor_01 asset as delivered. `pack_reactor.py` turns its OBJ into
`Resources/CC0/Reactor/*.bytes`, which `World/Reactor.cs` builds into the Lantern Core's reactor.

## Quaternius kits (CC0 1.0)

Fantasy Props MegaKit, Sci-Fi Essentials Kit and Modular SciFi MegaKit (standard free versions) by Quaternius,
https://quaternius.com, public domain (CC0 1.0). Converted by `tools/quaternius` into
`Resources/CC0/{Fantasy,SciFi,SciFiModular}` and `Resources/CC0/Quaternius/Textures`; see its README.

## Material-LIB (supplied by Ivan)

Stone, tile and pillar textures from the Material-LIB pack Ivan added to the project. `pack_matlib.py <Material-LIB dir>
Assets/_Project/Resources/CC0/Surfaces` packs them into the `matlib_*` surfaces (Ashlar, Mosaic, MosaicGrey, Fluted,
Masonry); height is made from the base colour since the pack has no height maps.
