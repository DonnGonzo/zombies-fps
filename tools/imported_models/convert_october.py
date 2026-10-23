"""Run with Blender 4.2: stage the nine October model packs; retain animation and materials."""
import bpy,json,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[2]
slugs=['a-slot-machine','animated-oak-trees','gambling-table','geometrynodes-scifi-sphere','mp5-gun','sci-fi-capsule','stylized-old-mage-animated-character','wizard-spell']
for slug in slugs:
 stage=root/'Temp/model-import/source'/slug
 dest=root/'Assets/_Project/ImportedModels'/slug
 dest.mkdir(parents=True,exist_ok=True)
 shutil.copytree(stage/'textures',dest/'Textures',dirs_exist_ok=True) if (stage/'textures').exists() else None
 p=next((stage/'source').iterdir())
 if p.suffix=='.fbx':
  shutil.copy2(p,dest/'model.fbx'); continue
 if p.suffix=='.blend': bpy.ops.wm.open_mainfile(filepath=str(p))
 else:
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.import_scene.gltf(filepath=str(p))
 if slug=='animated-oak-trees':
  # One complete authored oak, not the three-tree showcase or helper spheres.
  keep={'Armature','Mesh_0','Mesh_1'}
  for o in list(bpy.data.objects):
   if o.name not in keep: bpy.data.objects.remove(o,do_unlink=True)
  for a in list(bpy.data.actions):
   if a.name!='WindSwayAnimation_Armature': bpy.data.actions.remove(a)
 if slug=='mp5-gun':
  for o in bpy.data.objects:
   # These nodes add the studio material setup; retain evaluated geometry when exporting.
   if o.type=='MESH':
    for mod in list(o.modifiers):
     if mod.type=='NODES' and not mod.node_group: o.modifiers.remove(mod)
 # Save packed GLB texture images with deterministic names for the material remapper.
 if p.suffix=='.glb':
  for im in bpy.data.images:
   if im.size[0]:
    im.filepath_raw=str(dest/'Textures'/(im.name+'.png')); im.file_format='PNG'; im.save()
 bpy.ops.object.select_all(action='DESELECT')
 for o in bpy.data.objects:
  if o.type in {'MESH','ARMATURE','EMPTY'}: o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(dest/'model.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},bake_anim=slug in ['animated-oak-trees','geometrynodes-scifi-sphere','wizard-spell'],bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=1,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
 print('EXPORTED',slug,flush=True)
