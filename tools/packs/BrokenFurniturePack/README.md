# Broken Furniture Pack — Unity FPS / Zombie Survival

Eight unique procedural damaged props + a pre-arranged combined room-clutter GLB scene. Real mesh geometry, not renders. Designed in meters, exported Y-up. Source files are editable Python/trimesh. Grimy vertex colors are simple placeholders, not photographic PBR textures.

## Included models
- Broken_Dining_Chair: broken leg, cracked back and scattered wood
- Collapsed_Table: missing/detached leg and damaged tabletop
- Smashed_Bookshelf: leaning shelves, books and splinters
- Torn_Sofa: cushions and exposed foam patch
- Broken_Waiting_Bench: split and angled seating
- Ransacked_Cabinet: bent open doors and loose pieces
- Makeshift_Barricade: planks, metal supports and bolts
- Splintered_Crate: separated top boards and fragments
- Combined_Room_Clutter.glb: all above arranged in a sample layout

## Unity import
1. Unzip the pack.
2. Drag the `Models` folder into your Unity project's `Assets/BrokenFurniture/` folder.
3. OBJ files can be dropped directly into Unity. GLB requires a glTF importer such as Unity's glTFast package, installed with Package Manager. If no glTF package is installed, use the OBJ files.
4. Drag individual OBJ models into the Scene view. The OBJ files include vertex colors, but Unity's default materials may not display them. Assign your own materials or import the GLB with a compatible glTF importer for preview colors.
5. Models use meters, Y-up, and are positioned near floor height. Set import scale to 1.
6. Add collider components as needed (BoxCollider preferred for basic static geometry). Example helper `Unity/AddStaticPropColliders.cs` can be attached to prop roots to generate approximate collider bounds for child renderers.
7. Mark stationary props as Static and bake occlusion/lighting as appropriate.

## Notes
These are original procedural game props intended as starter or blockout-to-mid-detail environment art. They do not include UV-unwrapped photo textures, detailed normal maps, LOD variants, physics joints, fractured break animations or gameplay scripts. Detached debris is incorporated as part of each asset mesh; it is not independently simulated by default. All assets use many individually named mesh parts in GLB.

## Editing or regenerating
Install Python packages `pip install trimesh numpy` and run `python Source/generate_pack.py`. The generator overwrites the packaged models. Customize sizes and colors in the source.
