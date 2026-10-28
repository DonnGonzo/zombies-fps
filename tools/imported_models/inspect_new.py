import bpy,json,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
report=[]
for slug in ['a-slot-machine','animated-oak-trees','gambling-table','geometrynodes-scifi-sphere','mp5-gun','wizard-spell']:
 p=next((root/'Temp/model-import/source'/slug/'source').iterdir())
 if p.suffix=='.blend': bpy.ops.wm.open_mainfile(filepath=str(p))
 else:
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.import_scene.gltf(filepath=str(p))
 report.append(dict(slug=slug,objects=[dict(name=o.name,type=o.type,dimensions=list(o.dimensions),materials=[m.name if m else '' for m in o.data.materials] if o.type=='MESH' else [],modifiers=[m.type for m in o.modifiers]) for o in bpy.data.objects],materials=[dict(name=m.name,images=[n.image.name for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]) for m in bpy.data.materials if m.use_nodes],actions=[a.name for a in bpy.data.actions]))
(root/'Temp/model-import/new-inspect.json').write_text(json.dumps(report,indent=2))
