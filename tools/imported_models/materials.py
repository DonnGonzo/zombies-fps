"""Build explicit PBR texture sets; roughness is inverted into Unity smoothness alpha."""
from pathlib import Path
import json
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Assets/_Project/ImportedModels'
# material key: albedo, normal, metallic, roughness, occlusion, emission
SETS = {
 'church-bench-2': {'default': ['Bench_2_Diffuse','Bench_2_Normal',None,None,'Bench_2_AO']},
 'desert-eagle-gun': {'default': ['Low_Desert_Eagle_Material_BaseColor','NormalMapOpenGL8bits','Low_Desert_Eagle_Material_Metallic-Low_Desert_Eagle_Material']},
 'gothic-window': {'default': ['TexturesCom_WindowCathedral0092_M']},
 'gun-m4a1': {'default': ['M4_D']},
 'gun-rhino-smg': {key: [f'RhinoSMG_low_{part}_BaseColor',f'RhinoSMG_low_{part}_Normal',f'RhinoSMG_low_{part}_Metallic',f'RhinoSMG_low_{part}_Roughness'] for key,part in [('Body','Body'),('Attachements','Attachements'),('HandGuard','HandGuard'),('Magazine','Mag')]},
 'heavy-pulse-machine-gun': {key: [albedo,normal,key+'_Metallic.tga',key+'_Roughness.tga',None,key+'_Emissive.tga'] for key,albedo,normal in [('Big_Part','6b06920a743440f29cfda275035215e2_RGB_Big_P','Big_Part_Normal_OpenGL.tga'),('Small_Part','94783237ec5b48adae11ecbf212b290b_RGB_Small','Small_Part1_Normal_OpenGL.tga')]},
 'hl2-beta-air-exchange-truck-remade': {'default': ['unknown_Base_Color','unknown_Normal_DirectX','unknown_Metallic','unknown_Roughness']},
 'hospital-bed': {'default': ['BaseMaterial_Albedo','BaseMaterial_Normal','BaseMaterial_Metallic',None,'BaseMaterial_AO']},
 'low-poly-9mm-hand-gun': {'default': ['1K_HandgunTXTR']},
 'submachine-gun': {'default': ['Tec_9_low_lambert1_BaseColor','Tec_9_sketchfab_normal','Tec_9_low_lambert1_Metallic','Tec_9_low_lambert1_Roughness']},
 'tommy-gun': {key:[prefix+'_BaseColor',prefix+'_Normal',prefix+'_Metallic',prefix+'_Roughness'] for key,prefix in [('Material #26','xport500_Material__26'),('Material #31','xport500_Material__31'),('Material #90','bullet_low_Material__90')]},
 'zombie-dans': {key:[key+'_Diffuse',key+'_Normal'] for key in ['head','top','bottom','bow','hair']},
 'zombie-hazmat': {'HazMatSuit':['HazMatSuit_BaseMap','HazMatSuit_Normal','HazMatMetallic','HazMatRoughness','HazMatAO'],'ZombieHead':['ZombieHead_BaseMap','HeadNorm',None,'ZombieHead_Roughness','ZombieHead_AO']},
 'zombie-pickup-truck': {'02- Default':['Zombie_Pickup_Interior_Diffuse.tga'],'03 - Default':['Zombie_Pickup_Chassis.tga'],'13 - Default':['Zombie_Pickup_Mod.tga']},
}

def main():
    manifest=[]
    for slug,sets in SETS.items():
        folder=DEST/slug/'Textures'
        for key,raw in sets.items():
            names=(raw+[None]*6)[:6]
            prefix=key.replace(' ','_').replace('#','')
            maps={}
            for kind,name in zip(['albedo','normal','metallic','roughness','ao','emission'],names):
                if not name: continue
                p=folder/(name+'.png')
                if not p.exists(): raise FileNotFoundError(p)
                maps[kind]=Image.open(p).copy()
            if 'normal' in maps and slug.startswith('hl2-'):
                r,g,b=maps['normal'].convert('RGB').split()
                maps['normal']=Image.merge('RGB',(r,ImageOps.invert(g),b))
            if 'metallic' in maps or 'roughness' in maps:
                size=maps['albedo'].size
                metal=maps.get('metallic',Image.new('L',size,0)).convert('L').resize(size)
                smooth=ImageOps.invert(maps['roughness'].convert('L').resize(size)) if 'roughness' in maps else Image.new('L',size,90)
                maps['mask']=Image.merge('RGBA',(metal,metal,metal,smooth))
            if slug=='church-bench-2':
                gloss=Image.open(folder/'Bench_2_Glossiness.png').convert('L')
                zero=Image.new('L',gloss.size,0)
                maps['mask']=Image.merge('RGBA',(zero,zero,zero,gloss))
            if slug=='zombie-dans' and key=='hair':
                # Character Creator ships hair coverage as its own map: it becomes the albedo's alpha (cutout).
                opacity=Image.open(folder/'hair_Opacity.png').convert('L').resize(maps['albedo'].size)
                maps['albedo']=Image.merge('RGBA',(*maps['albedo'].convert('RGB').split(),opacity))
            entry={'slug':slug,'key':key}
            for kind in ['albedo','normal','mask','ao','emission']:
                if kind not in maps: continue
                target=folder/(prefix+'_'+kind+'.png')
                maps[kind].save(target)
                entry[kind]=str(target.relative_to(ROOT)).replace('\\','/')
            manifest.append(entry)
    (DEST/'materials.json').write_text(json.dumps({'materials':manifest},indent=2)+'\n')

if __name__=='__main__': main()
