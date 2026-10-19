# Reactor_01 — Gritty sci-fi floor-to-ceiling reactor

**Actual 3D model:** 6.04 meters tall, roughly 1.96 meters wide, Y-up, meters. Approx. 30,272 triangles across 104 components. Built for environment decoration, not a rigged or destructible physics asset.

## Included
- `Models/Reactor_01.obj` and `.mtl` — easiest to import directly into Unity, with named component geometry and diffuse material definitions.
- `Models/Reactor_01.glb` — portable glTF binary, including vertex colors; Unity requires a glTF importer package (for example glTFast) for direct use.
- `Scripts/generate_reactor.py` — fully editable procedural model source; needs Python + numpy + trimesh.
- `Scripts/ReactorPulse.cs` — optional gentle plasma movement / emissive pulse.
- `Scripts/ReactorMaterialSetup.cs` — Unity Editor menu for assigning basic colored and emissive materials; must be placed in an `Editor` folder.

## Unity import (simplest: OBJ)
1. Extract this ZIP. Create `Assets/Reactor/Models`, `Assets/Reactor/Scripts`, and `Assets/Editor` in your Unity project.
2. Copy `Reactor_01.obj` and `Reactor_01.mtl` into `Assets/Reactor/Models`.
3. Copy `ReactorPulse.cs` into `Assets/Reactor/Scripts`, and `ReactorMaterialSetup.cs` into `Assets/Editor`.
4. Drag `Reactor_01.obj` into your scene. Ensure **Scale Factor = 1** (the model dimensions should be about 6m tall) and note Y-up is already used.
5. Select the reactor **instance** in Hierarchy and click **Tools > Reactor > Setup Materials on Selected Reactor**. The generated colors are an initial stylized/realistic base, not hand-painted photographic textures.
6. Select the `Glass` material under `Assets/ReactorMaterials` and set **Surface Type: Transparent** (URP/HDRP) or **Rendering Mode: Transparent** (Built-in). Adjust Alpha approximately 0.15–0.3.
7. Add `ReactorPulse` to the reactor root. Optionally create a Point Light with warm orange color near the glass chamber, then drag it to `Reactor Light` on the component. Keep it off the root if desired.
8. Add a modest Bloom post-process effect to your camera/volume to make the plasma glow. For URP, enable HDR and Bloom in your Volume and renderer/camera configuration. Save the assembled instance as a Prefab.

## Optional GLB import
Install a compatible glTF loader such as **glTFast** from Unity Package Manager. Import the GLB; don't import OBJ and GLB simultaneously for the same reactor. Some versions flatten/rename the node hierarchy, which can prevent the auto-animation from locating `Plasma_Blob_` nodes. OBJ is the recommended path for the included scripts.

## Notes and practical limitations
- **Render pipeline not selected:** editor material tool auto-detects URP, then HDRP, then Built-in; check assigned shader if your project has several pipelines installed. Glass transparency requires the manual step.
- Procedurally modeled meshes have no authored UV unwrap or photorealistic texture set; materials are solid-color PBR starting points. Add tiling grunge/normal maps in Unity or Blender to get a more realistic surface.
- The 30k-triangle, 104-mesh object is a **hero prop**, not optimized for hundreds of instances. Consider combining static meshes, GPU instancing, LODs and an optional simplified collider.
- Moving plasma blobs are separately modeled solid meshes. `ReactorPulse` animates them but does not simulate fluid. True volumetric fluid, refraction and destructibility require additional shader/VFX and gameplay work.
- Collider: Add a capsule or several box colliders manually to the reactor root; avoid a complex mesh collider for moving objects.
- Mesh origin is at (0,0,0) on the floor, so place at the ground and adjust overall scale to reach the ceiling.

## Regenerate
```
python -m pip install numpy trimesh
python Scripts/generate_reactor.py
```
