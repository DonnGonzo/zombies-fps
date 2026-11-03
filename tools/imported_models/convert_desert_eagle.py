import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=root/'Temp/model-import/source/desert-eagle-gun/source/roof lights.blend'
bpy.ops.wm.open_mainfile(filepath=str(p))
for o in bpy.data.objects:
 print('OBJECT',o.name,o.type,list(o.dimensions),[m.name for m in o.data.materials] if o.type=='MESH' else '',flush=True)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
 if o.type=='MESH': o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/_Project/ImportedModels/desert-eagle-gun/model.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
