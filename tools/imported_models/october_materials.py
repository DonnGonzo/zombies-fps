from pathlib import Path
import json
from PIL import Image
root=Path(__file__).resolve().parents[2]; base=root/'Assets/_Project/ImportedModels'
p=base/'materials.json'; data=json.loads(p.read_text()); new=[]
def add(slug,key,albedo=None,normal=None,emission=None):
 folder=base/slug/'Textures'
 def find(s):
  if not s:return None
  hits=[x for x in folder.iterdir() if x.suffix!='.meta' and x.stem.lower()==s.lower()] or [x for x in folder.iterdir() if x.suffix!='.meta' and x.name.lower()==s.lower()]
  return str(hits[0].relative_to(root)).replace('\\','/') if hits else None
 new.append(dict(slug=slug,key=key,albedo=find(albedo),normal=find(normal),emission=find(emission)))
add('a-slot-machine','default','Image_0')
add('animated-oak-trees','bark','Image_0','bark_normal')
add('animated-oak-trees','foliage','oak_color','oak_normal')
for key in ['Material.004','Material.005','Material.001','default']:
 add('gambling-table',key,'hazard_table3b' if key in ['Material.004','default'] else 'table_bits','hazard_table3_n' if key in ['Material.004','default'] else 'table_bits_n')
for key,stem in [('BodyMage','BodyLP_Body'),('Cloth2','ClothLP_Cloth'),('FaceGrp1','FaceGrpLP_FaceGrp'),('Props1','PropsLP_Props'),('Book','MagicBookReady_Book')]:
 add('stylized-old-mage-animated-character',key,stem+'_BaseColor',stem+'_Normal',stem+'_Emissive')
add('stylized-old-mage-animated-character','FX1','FXMage_(1)',emission='FXMage_(1)')
for key in ['Capsule','Supports','Glass']:
 stem='Capsula_Low_'+key
 add('sci-fi-capsule',key,stem+'_BaseColor',stem+'_Normal',stem+'_Emissive')
for key in ['Hologram_1','Hologram_2','Hologram_3']:
 add('sci-fi-capsule',key,'hexagon_keyboard_by_hxtremechaos_d865owr-f',emission='hexagon_keyboard_by_hxtremechaos_d865owr-f')
add('sci-fi-capsule','Halo','kisspng-light-green-white-point-halo-green',emission='kisspng-light-green-white-point-halo-green')
for key,im in [('Material.002','Image_1'),('Material.003','Image_0')]:add('wizard-spell',key,im,emission=im)
report=json.loads((root/'Temp/model-import/new-inspect.json').read_text())
for mat in next(x for x in report if x['slug']=='mp5-gun')['materials']:
 images=mat['images'];alb=next((s for s in images if 'Albedo' in s),None);norm=next((s for s in images if 'Normal' in s),None)
 add('mp5-gun',mat['name'],alb,norm)
slugs={x['slug'] for x in new}
data['materials']=[x for x in data['materials'] if x['slug'] not in slugs]+new
p.write_text(json.dumps(data,indent=2)+'\n')
# Keep runtime textures reasonably sized without altering originals.
for slug in slugs:
 for f in (base/slug/'Textures').glob('*'):
  try:
   with Image.open(f) as im:
    if max(im.size)>2048: im.thumbnail((2048,2048),Image.Resampling.LANCZOS);im.save(f)
  except OSError: pass
