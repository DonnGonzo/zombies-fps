"""TEN ARMS & AEGIS: Unity FPS weapon meshes. Non-operational fictional game models.
Usage: pip install trimesh numpy pillow; python Source/generate_models.py
Y-up, forward +Z, meters. OBJ + MTL, glTF/GLB PBR. Custom geometry procedural.
"""
from pathlib import Path
import math, random, numpy as np, trimesh
from PIL import Image, ImageDraw, ImageFont
from trimesh.transformations import rotation_matrix
R=Path(__file__).resolve().parents[1];OUT=R/'Models';COL=R/'Previews';COL.mkdir(exist_ok=True)
random.seed(12)
MATS={
 'ParkerizedSteel':((64,69,72,255),.86,.46,(0,0,0)),
 'BlackSteel':((29,33,37,255),.88,.38,(0,0,0)),
 'BrushedMetal':((116,122,127,255),.91,.31,(0,0,0)),
 'BluedSteel':((47,55,68,255),.81,.33,(0,0,0)),
 'Gunmetal':((56,58,63,255),.84,.42,(0,0,0)),
 'Polymer':((42,45,46,255),.08,.67,(0,0,0)),
 'SlatePolymer':((70,77,82,255),.05,.65,(0,0,0)),
 'SandCerakote':((152,137,110,255),.44,.58,(0,0,0)),
 'OliveCerakote':((79,87,67,255),.48,.58,(0,0,0)),
 'DarkWalnut':((90,52,32,255),.02,.57,(0,0,0)),
 'BirchWood':((146,109,68,255),.01,.55,(0,0,0)),
 'Rubber':((24,25,28,255),.02,.88,(0,0,0)),
 'Bronze':((145,103,61,255),.78,.35,(0,0,0)),
 'Brass':((199,164,80,255),.91,.32,(0,0,0)),
 'Copper':((175,92,60,255),.91,.34,(0,0,0)),
 'Warning':((221,165,52,255),.28,.58,(0,0,0)),
 'RedDetail':((169,41,49,255),.35,.49,(0,0,0)),
 'Optics':((19,36,42,255),.05,.14,(0,0,0)),
 'EmissiveCyan':((65,204,244,255),.25,.2,(0.12,0.87,1.0)),
 'EmissiveAmber':((251,150,62,255),.35,.21,(1.0,.42,.04)),
 'EmissiveViolet':((177,91,255,255),.25,.22,(.63,.23,1.0)),
 'EnergyGlass':((59,102,154,150),.15,.13,(.07,.18,.40)),
 'ShieldGlass':((80,152,177,145),.16,.22,(.04,.13,.21)),
 'WornPaint':((74,91,95,255),.39,.62,(0,0,0)),
}
def mat_col(key): return np.array(MATS[key][0],dtype=np.uint8)

def rounded_box(ext,bevel=.03):
    # Chamfer-edged cuboid: central box + 6 inset planes; analytic beveled cross sections
    x,y,z=np.array(ext)/2; b=min(bevel,.49*min(ext));
    # XY octagonal cross-section, +Z extruded; bevels corner edges of long body
    pts=[(-x+b,-y),(x-b,-y),(x,-y+b),(x,y-b),(x-b,y),(-x+b,y),(-x,y-b),(-x,-y+b)]
    vv=np.array([[a,c,zz] for zz in [-z,z] for a,c in pts],dtype=float)
    faces=[]
    for i in range(8): j=(i+1)%8;faces.extend([[i,j,8+j],[i,8+j,8+i]])
    for i in range(1,7):faces.append([0,i,i+1]);faces.append([8,8+i+1,8+i])
    return trimesh.Trimesh(vertices=vv,faces=faces,process=False)

class Model:
 def __init__(self,name):self.name=name;self.parts=[];self.i=0
 def add(self,name,mesh,mat):
  mesh=mesh.copy();mesh.visual.vertex_colors=np.tile(mat_col(mat),(len(mesh.vertices),1));self.parts.append((f'{self.i:03d}_{name}',mesh,mat));self.i+=1
 def box(self,name,ext,pos,mat,rot=None,bevel=.008):
  mesh=rounded_box(ext,bevel) if bevel else trimesh.creation.box(extents=ext)
  if rot: mesh.apply_transform(rotation_matrix(rot[0],rot[1]))
  mesh.apply_translation(pos);self.add(name,mesh,mat)
 def rod(self,name,a,b,r,mat,sections=12):
  a=np.array(a,float);b=np.array(b,float);v=b-a; l=float(np.linalg.norm(v))
  if l<0.00001:return
  m=trimesh.creation.cylinder(radius=r,height=l,sections=sections);m.apply_transform(trimesh.geometry.align_vectors([0,0,1],v/l));m.apply_translation((a+b)/2);self.add(name,m,mat)
 def cyl(self,name,r,length,pos,mat,axis='z',sections=18):
  m=trimesh.creation.cylinder(radius=r,height=length,sections=sections)
  if axis=='y':m.apply_transform(rotation_matrix(-math.pi/2,[1,0,0]))
  if axis=='x':m.apply_transform(rotation_matrix(math.pi/2,[0,1,0]))
  m.apply_translation(pos);self.add(name,m,mat)
 def cone(self,name,r,length,pos,mat,axis='z'):
  m=trimesh.creation.cone(radius=r,height=length,sections=18)
  if axis=='y':m.apply_transform(rotation_matrix(-math.pi/2,[1,0,0]))
  m.apply_translation(pos);self.add(name,m,mat)
 def sphere(self,name,pos,scale,mat,sub=2):
  m=trimesh.creation.icosphere(subdivisions=sub,radius=1);m.apply_scale(scale);m.apply_translation(pos);self.add(name,m,mat)
 def ring(self,name,Rr,rr,pos,mat,axis='z',segments=24,sides=6):
  verts=[];faces=[]
  for i in range(segments):
   t=i*2*math.pi/segments
   for j in range(sides):
    q=j*2*math.pi/sides;mr=Rr+rr*math.cos(q)
    if axis=='z':verts.append([mr*math.cos(t),mr*math.sin(t),rr*math.sin(q)])
    elif axis=='y':verts.append([mr*math.cos(t),rr*math.sin(q),mr*math.sin(t)])
  for i in range(segments):
   for j in range(sides):
    a=i*sides+j;b=((i+1)%segments)*sides+j;c=((i+1)%segments)*sides+(j+1)%sides;d=i*sides+(j+1)%sides
    faces.extend([[a,b,c],[a,c,d]])
  m=trimesh.Trimesh(vertices=verts,faces=faces,process=False);m.apply_translation(pos);self.add(name,m,mat)
 def rail(self,z1,z2,y=0.14,width=.17,teeth=9):
  self.box('Top_Picatinny', [width,.026,z2-z1],[0,y,(z1+z2)/2],'BlackSteel')
  for j in range(teeth):
   zz=z1+(j+.5)*(z2-z1)/teeth
   self.box(f'Rail_Slot_{j}',[width+.025,.018,.017],[0,y+.018,zz],'BrushedMetal')
 def screws(self,locs,mat='Brass',axis='x',r=.012):
  for i,p in enumerate(locs):self.cyl(f'HexScrew_{i}',r,.01,p,mat,axis=axis,sections=8)
 def ribbed_grip(self,p,sz,mat='Polymer',ribs=6,tilt=.18):
  self.box('Angled_Grip',sz,p,mat,rot=(tilt,[1,0,0]),bevel=.035)
  for i in range(ribs):
   y=p[1]-sz[1]*.39+i*(sz[1]*.78/max(1,ribs-1))
   self.box(f'Grip_Stipple_{i}',[sz[0]+.005,.008,sz[2]*.86],[p[0],y,p[2]],'Rubber',rot=(tilt,[1,0,0]))
 def optic(self,z,y=.21,kind='red_dot'):
  if kind=='scope':
   self.cyl('ScopeBody',.066,.5,[0,y,z],'BlackSteel',sections=18)
   self.cyl('ScopeBell',.092,.10,[0,y,z+.25],'Gunmetal')
   self.cyl('ScopeEyepiece',.079,.10,[0,y,z-.26],'Rubber')
   self.cyl('ScopeLens',.061,.005,[0,y,z+.306],'Optics')
   for oz in [z-.16,z+.15]:self.box('ScopeMount',[.095,.08,.05],[0,y-.095,oz],'BrushedMetal')
   self.cyl('ScopeDial',.042,.055,[.064,y+.055,z],'BrushedMetal',axis='x')
  else:
   self.box('Reflex_Housing',[.16,.15,.19],[0,y,z],'BlackSteel',bevel=.025)
   self.cyl('FrontLens',.062,.012,[0,y,z+.102],'Optics')
   self.cyl('LED_Reticle',.009,.014,[0,y,z+.113],'EmissiveAmber')
   self.box('MountPlate',[.20,.035,.23],[0,y-.095,z],'Gunmetal')
 def muzzle(self,z,r=.05,brake=True):
  self.cyl('Muzzle_Shroud',r+.014,.11,[0,0,z],'BlackSteel')
  self.cyl('Dark_Muzzle_Bore',r*.62,.006,[0,0,z+.059],'Rubber')
  if brake:
   for i in [-1,1]:self.box('Compensator_Port',[.015,.016,.042],[i*(r+.011),0,z],'BrushedMetal')
 def magazine(self,center,ext,mat='Polymer',ridges=6,tilt=.08):
  self.box('Magazine',ext,center,mat,rot=(tilt,[1,0,0]),bevel=.025)
  for i in range(ridges):
   y=center[1]-(i-ridges/2)*.017
   self.box(f'Magazine_Groove_{i}',[ext[0]+.008,.005,ext[2]*.85],[center[0],y,center[2]],'Rubber')
  self.box('Magazine_BasePlate',[ext[0]+.035,.017,ext[2]+.02],[center[0],center[1]-ext[1]/2-.006,center[2]],'BrushedMetal')
 def surface_markings(self,palette='RedDetail'):
  self.box('Factory_Stamp',[.19,.012,.037],[0,.084,.015],palette)
  self.box('Serial_Plate',[.16,.045,.009],[.147,-.016,.025],'BrushedMetal')
 def save(self):
  mtl=OUT/'OBJ'/f'{self.name}.mtl';obj=OUT/'OBJ'/f'{self.name}.obj';glb=OUT/'GLB'/f'{self.name}.glb'
  scene=trimesh.Scene();omaterials=set()
  with obj.open('w') as o:
   o.write(f'# {self.name} (Y-up meters; muzzle +Z)\nmtllib {self.name}.mtl\n');offset=1
   for name,mesh,mat in self.parts:
    o.write(f'o {name}\nusemtl {mat}\n');omaterials.add(mat)
    for v in mesh.vertices:o.write('v %.6f %.6f %.6f\n'%tuple(v))
    for tri in mesh.faces:o.write('f %d %d %d\n'%tuple(tri+offset))
    offset+=len(mesh.vertices)
    rgba,metal,rough,emi=MATS[mat]
    material=trimesh.visual.material.PBRMaterial(name=mat,baseColorFactor=list(rgba),metallicFactor=metal,roughnessFactor=rough,emissiveFactor=list(emi))
    gm=mesh.copy();gm.visual=trimesh.visual.TextureVisuals(material=material)
    scene.add_geometry(gm,node_name=name,geom_name=name)
  with mtl.open('w') as f:
   for k in sorted(omaterials):
    rgb,metal,rough,emi=MATS[k]
    f.write(f'newmtl {k}\nKd {rgb[0]/255:.4f} {rgb[1]/255:.4f} {rgb[2]/255:.4f}\nKs {metal:.3f} {metal:.3f} {metal:.3f}\nNs {max(5,1/rough**2):.2f}\nd {rgb[3]/255:.4f}\nillum 2\n\n')
  glb.write_bytes(scene.export(file_type='glb'))
  return len(self.parts),sum(len(part.faces) for _,part,_ in self.parts)

def gun_base(name,kind,main='Gunmetal',stock='Polymer',length=1.0):
 m=Model(name);g=length
 receiver=.35*g;fwd=.28*g;back=.16*g
 m.box('Machined_Upper_Receiver',[.19,.16,.36*g],[0,.034,0],main,bevel=.035)
 m.box('Lower_Receiver',[.157,.125,.24*g],[0,-.087,-.044*g],'BlackSteel',bevel=.025)
 m.box('Bolt_Cover',[.016,.058,.155*g],[.103,.065,.05*g],'BrushedMetal')
 m.box('Ejection_Port',[.012,.037,.088*g],[.114,.060,.075*g],'Rubber')
 m.cyl('Selector_Switch',.032,.018,[.106,-.016,-.078*g],'Brass','x',sections=12)
 m.box('Trigger_Guard',[.084,.034,.15*g],[0,-.19,-.005*g],'BlackSteel')
 m.rod('Trigger_Pin',[-.038,-.17,-.015*g],[-.038,-.21,.015*g],.010,'BrushedMetal')
 m.ribbed_grip((0,-.265,-.14*g),(.13,.25,.12*g),stock,tilt=.24)
 m.magazine((0,-.26,.092*g),(.136,.30,.098*g))
 m.cyl('Main_Barrel',.038,.43*g,[0,0,.26*g],'BlackSteel')
 m.cyl('Outer_Barrel_Shroud',.065,.33*g,[0,0,.25*g],main,sections=14)
 for i in range(7):
  zz=.115*g+i*.038*g
  m.box(f'Cooling_Vent_{i}',[.012,.025,.018*g],[.071,0,zz],'BlackSteel')
 m.muzzle(.50*g)
 m.rail(-.1*g,.38*g,.15,width=.18,teeth=9)
 m.screws([( .096,.049,-.12*g),(.096,.049,.13*g),(-.096,.049,-.12*g),(-.096,.049,.13*g)],'Brass')
 m.surface_markings()
 return m

def pistol():
 m=Model('01_Harker9_Service_Pistol')
 m.box('Polymer_Frame',[.157,.106,.28],[0,-.036,0],'Polymer',bevel=.025)
 m.box('Metal_Slide',[.158,.125,.38],[0,.093,.035],'Gunmetal',bevel=.03)
 m.cyl('Barrel',.019,.355,[0,.06,.08],'BlackSteel')
 m.cyl('Muzzle_Bore',.016,.007,[0,.06,.226],'Rubber')
 for i in range(5):
  m.box(f'Rear_Serration_{i}',[.005,.096,.009],[.079,.088,-.12+i*.020],'BrushedMetal')
 m.box('Front_Sight',[.035,.040,.036],[0,.17,.186],'BlackSteel')
 m.box('Rear_Sight',[.058,.036,.021],[0,.167,-.12],'BlackSteel')
 m.box('Ejection_Port',[.007,.043,.086],[.085,.106,.055],'BlackSteel')
 m.ribbed_grip((0,-.255,-.09),(.133,.34,.15),'Polymer',7,tilt=.16)
 m.box('Trigger_Guard',[.082,.025,.137],[0,-.141,.074],'BlackSteel')
 m.rod('Trigger',[0,-.092,.046],[0,-.153,.080],.008,'BrushedMetal')
 m.box('Magazine_Base',[.140,.023,.16],[0,-.435,-.12],'Rubber')
 m.cyl('Release',.018,.009,[.083,-.13,-.04],'Brass','x')
 m.box('Marking',[.090,.006,.023],[0,.158,.041],'Warning')
 return m

def smg():
 m=gun_base('02_Sable9_Compact_SMG','smg',main='BlackSteel',length=.78)
 m.box('Folding_Stock_Butt',[.04,.21,.11],[0,-.06,-.48],'Rubber')
 for x in [-.06,.06]:m.rod(f'Folding_Stock_Rail_{x}',[x,.03,-.18],[x,-.02,-.46],.012,'BrushedMetal')
 m.cyl('Suppressor',.065,.26,[0,0,.44],'BlackSteel')
 for i in range(9):m.ring(f'Suppressor_Ring_{i}',.066,.003,[0,0,.32+i*.024],'Gunmetal')
 m.box('Front_Grip',[.077,.115,.083],[0,-.135,.235],'Rubber')
 m.optic(-.01,.235)
 return m

def carbine():
 m=gun_base('03_Atlas5_Tactical_Carbine','rifle',main='OliveCerakote',stock='Polymer',length=1.1)
 m.cyl('Buffer_Tube',.033,.28,[0,0,-.37],'BlackSteel')
 m.box('Adjustable_Stock',[.19,.19,.23],[0,-.015,-.58],'OliveCerakote',bevel=.04)
 m.box('Recoil_Pad',[.20,.22,.026],[0,-.015,-.715],'Rubber')
 m.box('Cheek_Riser',[.14,.048,.14],[0,.11,-.57],'Rubber')
 m.optic(0,.26)
 m.box('Laser_Pod',[.065,.055,.15],[.11,.08,.24],'BlackSteel')
 m.cyl('Laser_Diode',.018,.008,[.11,.08,.33],'EmissiveAmber')
 return m

def battle():
 m=gun_base('04_Kodiak7_Battle_Rifle','battle',main='SandCerakote',stock='SandCerakote',length=1.22)
 m.box('Solid_Stock',[.20,.215,.29],[0,-.025,-.52],'SandCerakote',bevel=.045)
 m.box('EndPad',[.21,.24,.025],[0,-.027,-.67],'Rubber')
 m.optic(.10,.245,kind='scope')
 m.cyl('Flash_Hider',.048,.17,[0,0,.69],'BluedSteel')
 m.box('Forward_Bipod_Housing',[.16,.04,.11],[0,-.115,.37],'BrushedMetal')
 return m

def shotgun():
 m=Model('05_Riot12_Pump_Shotgun')
 m.box('Forged_Receiver',[.175,.16,.37],[0,.01,-.03],'BluedSteel',bevel=.035)
 m.cyl('Rifled_Game_Barrel',.035,.77,[0,.067,.42],'BlackSteel',sections=22)
 m.cyl('Magazine_Tube',.043,.58,[0,-.054,.32],'Gunmetal',sections=22)
 m.cyl('Tube_Cap',.051,.04,[0,-.054,.63],'Brass')
 m.box('Slide_Pump',[.15,.14,.26],[0,-.037,.38],'DarkWalnut',bevel=.035)
 for i in range(7):m.box(f'Pump_Texture_{i}',[.157,.016,.010],[0,.035,.27+i*.034],'BirchWood')
 m.box('Wood_Stock',[.17,.22,.38],[0,-.105,-.43],'DarkWalnut',rot=(-.055,[1,0,0]),bevel=.048)
 m.box('Butt_Pad',[.18,.24,.03],[0,-.11,-.63],'Rubber')
 m.ribbed_grip((0,-.22,-.14),(.13,.20,.13),'DarkWalnut',5)
 m.box('TriggerGuard',[.09,.024,.11],[0,-.175,0],'BlackSteel')
 m.rod('Front_Bead',[0,.10,.75],[0,.13,.75],.006,'Brass')
 m.cyl('Receiver_BreechPin',.025,.02,[.095,.045,-.06],'Brass','x')
 for x in [-1,1]:m.box('Sling_Mount',[.01,.046,.06],[x*.09,-.075,-.44],'BrushedMetal')
 return m

def marksman():
 m=Model('06_Specter308_Sniper_Rifle')
 m.box('Precision_Chassis',[.19,.18,.48],[0,.0,0],'SandCerakote',bevel=.04)
 m.box('Chassis_Underbody',[.15,.08,.35],[0,-.11,.02],'Gunmetal')
 m.cyl('Long_Fluted_Barrel',.033,.95,[0,.056,.67],'BluedSteel',sections=22)
 m.cyl('Muzzle_Brake',.062,.135,[0,.056,1.20],'BlackSteel')
 for i in range(4):m.box(f'Flute_{i}',[.008,.008,.48],[(1 if i%2 else -1)*.035,.055+(.015 if i>1 else -.015),.57],'BrushedMetal')
 m.box('Skeletonized_Stock',[.16,.16,.28],[0,-.02,-.45],'SandCerakote')
 m.box('Cheek_Rest',[.14,.045,.30],[0,.095,-.49],'Rubber')
 m.box('Butt',[.18,.22,.03],[0,-.01,-.66],'Rubber')
 for x in [-.058,.058]:m.rod(f'Stock_Strut_{x}',[x,-.07,-.15],[x,-.065,-.63],.013,'BrushedMetal')
 m.magazine((0,-.24,-.015),(.12,.28,.12),'Polymer',3)
 m.ribbed_grip((0,-.30,-.16),(.115,.25,.12),'Rubber',4)
 m.rail(-.20,.40,.20)
 m.optic(0,.325,kind='scope')
 for xx in [-.08,.08]:m.rod(f'Bipod_Leg_{xx}',[xx,-.05,.64],[xx*1.7,-.37,.70],.018,'BrushedMetal')
 return m

def lmg():
 m=Model('07_Grinder_Light_Machine_Gun')
 m.box('Heavy_Receiver',[.255,.24,.53],[0,.02,-.06],'Gunmetal',bevel=.035)
 m.box('Feed_Cover',[.31,.055,.43],[0,.20,-.03],'BrushedMetal')
 m.box('Charging_Handle',[.09,.07,.13],[.15,.03,-.13],'BlackSteel')
 m.cyl('Machine_Barrel',.048,.79,[0,.025,.59],'BlackSteel',sections=22)
 m.cyl('Heat_Shroud',.105,.59,[0,.025,.48],'ParkerizedSteel',sections=20)
 for i in range(8):
  zz=.20+i*.065
  for sx in [-1,1]:m.box(f'BarrelVent_{i}_{sx}',[.008,.048,.031],[sx*.108,.02,zz],'Rubber')
 m.muzzle(1.0,.06)
 m.box('Belt_Box',[.29,.32,.24],[-.04,-.27,.02],'OliveCerakote',bevel=.035)
 m.box('Feed_Handle',[.38,.045,.06],[-.03,.27,-.08],'BlackSteel')
 for i in range(9):m.cyl(f'Visible_Link_{i}',.022,.07,[-.14+i*.027,-.085,.16],'Brass',axis='y',sections=12)
 m.box('Hollow_Stock',[.22,.22,.40],[0,-.03,-.51],'Polymer')
 m.box('Stock_Pad',[.23,.235,.03],[0,-.03,-.73],'Rubber')
 m.ribbed_grip((0,-.30,-.2),(.15,.26,.14),'Rubber',5)
 m.rail(-.30,.4,.275,teeth=11)
 m.optic(.05,.36)
 for xx in [-.10,.10]:m.rod(f'HeavyBipod_{xx}',[xx,-.05,.73],[xx*1.6,-.42,.80],.022,'Gunmetal')
 return m

def scifi():
 m=Model('09_Helix_Plasma_Carbine')
 m.box('Ceramic_Core',[.21,.20,.56],[0,0,0],'WornPaint',bevel=.06)
 m.box('Upper_Housing',[.25,.11,.66],[0,.123,.07],'BluedSteel',bevel=.04)
 m.box('Under_Rail',[.16,.05,.54],[0,-.145,.04],'BlackSteel')
 m.cyl('Energy_Chamber',.091,.30,[0,.007,.20],'EnergyGlass',sections=30)
 m.cyl('Contained_Plasma',.055,.28,[0,.007,.20],'EmissiveCyan',sections=22)
 for i in range(7):
  zz=.08+i*.043
  m.ring(f'Containment_Coil_{i}',.095,.015,[0,.007,zz],'Bronze',segments=24)
 m.cyl('Emitter_Barrel',.074,.34,[0,0,.52],'Gunmetal',sections=24)
 m.ring('Output_Emitter',.104,.027,[0,0,.74],'BrushedMetal',segments=32)
 m.cyl('Emitter_Lens',.058,.01,[0,0,.752],'EmissiveCyan',sections=30)
 m.box('Folded_Stock',[.20,.19,.3],[0,-.05,-.47],'Gunmetal',bevel=.06)
 m.box('Shoulder_Gel',[.20,.21,.035],[0,-.06,-.635],'Rubber')
 m.ribbed_grip((0,-.28,-.15),(.13,.29,.15),'SlatePolymer',6)
 m.magazine((0,-.25,.14),(.15,.18,.15),'EnergyGlass',2)
 for sx in [-1,1]:
  m.rod(f'Copper_Power_Line_{sx}',[sx*.16,-.07,-.12],[sx*.17,-.05,.53],.014,'Copper')
  for z in [.02,.16,.30,.45]:m.box(f'Power_Indicator_{sx}_{z}',[.014,.025,.043],[sx*.167,.10,z],'EmissiveCyan')
 m.optic(-.07,.28)
 return m

def pulse():
 m=Model('10_Resonance_Pulse_Projector')
 m.box('Power_Receiver',[.27,.27,.62],[0,.0,-.09],'BlackSteel',bevel=.08)
 m.box('Shoulder_Brace',[.21,.25,.35],[0,-.03,-.47],'SlatePolymer',bevel=.05)
 m.box('ButtPad',[.22,.26,.03],[0,-.04,-.67],'Rubber')
 m.ribbed_grip((0,-.30,-.16),(.13,.29,.16),'SlatePolymer',6)
 for i in range(3):
  zz=.16+i*.15
  m.ring(f'Magnetic_Torus_{i}',.15-i*.012,.028,[0,.01,zz],'BrushedMetal',segments=32)
  m.ring(f'Coil_Glow_{i}',.13-i*.012,.008,[0,.01,zz+.018],'EmissiveViolet',segments=32)
  for sx in [-1,1]:m.rod(f'Coil_Rail_{i}_{sx}',[sx*.13,-.05,zz-.06],[sx*.13,-.05,zz+.08],.012,'Bronze')
 m.cyl('Pulse_Capacitor',.108,.50,[0,.01,.33],'EnergyGlass',sections=32)
 m.cyl('Purple_Plasma',.082,.47,[0,.01,.33],'EmissiveViolet',sections=28)
 m.cyl('Dish_Base',.185,.10,[0,.01,.68],'Gunmetal',sections=32)
 m.cyl('Dish_Reflector',.23,.045,[0,.01,.75],'BrushedMetal',sections=36)
 m.ring('Pulse_Mouth',.178,.025,[0,.01,.78],'EmissiveCyan',segments=32)
 for i in range(8):
  a=i*math.pi/4;m.rod(f'Emitter_Fin_{i}',[.11*math.cos(a),.01+.11*math.sin(a),.73],[.21*math.cos(a),.01+.21*math.sin(a),.80],.018,'Bronze')
 m.box('Reactor_Display',[.19,.048,.13],[0,.155,-.19],'EmissiveViolet',bevel=.012)
 m.box('Battery_Pack',[.18,.22,.20],[0,-.205,.045],'Gunmetal')
 for x in [-.06,.06]:m.cyl(f'BatteryCell_{x}',.04,.19,[x,-.225,.05],'EmissiveCyan','y')
 return m

def shield():
 m=Model('08_Aegis_Shield_Assembled')
 m.box('Armored_Plate',[1.43,1.78,.135],[0,.91,0],'BluedSteel',bevel=.11)
 m.box('Forward_Bumper',[1.51,.14,.17],[0,.18,.025],'BlackSteel')
 m.box('RimLeft',[.09,1.71,.20],[-.72,.91,0],'BrushedMetal')
 m.box('RimRight',[.09,1.71,.20],[.72,.91,0],'BrushedMetal')
 m.box('TopArmor',[1.29,.15,.20],[0,1.80,0],'Gunmetal')
 m.box('Viewing_Slit_Frame',[.78,.30,.11],[0,1.43,.108],'BlackSteel')
 m.box('BulletResistant_Viewport',[.68,.21,.035],[0,1.43,.177],'ShieldGlass')
 m.box('Power_Armor_Reinforcement',[.95,.16,.12],[0,.80,.15],'Gunmetal')
 m.box('Seal_Insignia',[.23,.23,.018],[0,.57,.112],'Warning')
 for sx in [-1,1]:
  for yy in [.28,.57,.94,1.71]:m.cyl(f'Armor_Bolt_{sx}_{yy}',.035,.015,[sx*.64,yy,.12],'Brass')
 for yy in [.48,.72]:m.box(f'Rear_ArmStrap_{yy}',[.44,.08,.08],[0,yy,-.14],'Rubber')
 m.box('Rear_Handle',[.08,.38,.12],[0,.9,-.20],'BlackSteel')
 m.box('Pressure_Lock',[.35,.25,.06],[0,1.09,-.12],'BrushedMetal')
 m.cyl('Power_Unit',.12,.15,[0,.33,-.15],'Bronze',axis='z')
 for sx in [-1,1]:
  m.box(f'PowerIndicator_{sx}',[.035,.33,.023],[sx*.50,1.03,.115],'EmissiveCyan')
 return m

def shield_part_plate():
 m=Model('08A_Aegis_Shield_Armor_Plate');m.box('MainArmor',[1.43,1.78,.13],[0,.91,0],'BluedSteel',bevel=.1)
 for x in [-.64,.64]:
  for y in [.35,.9,1.5]:m.cyl(f'Fastener_{x}_{y}',.035,.016,[x,y,.08],'Brass')
 return m

def shield_part_view():
 m=Model('08B_Aegis_Shield_View_Module');m.box('Bezel',[.83,.34,.10],[0,.14,0],'BlackSteel')
 m.box('ArmoredGlass',[.68,.21,.045],[0,.14,.075],'ShieldGlass')
 for x in [-.34,.34]:m.cyl(f'GlassBolt_{x}',.026,.017,[x,.14,.08],'Brass')
 return m

def shield_part_power():
 m=Model('08C_Aegis_Shield_Power_Core');m.cyl('Battery',.19,.35,[0,0,0],'Gunmetal','z',sections=24)
 m.ring('EnergyLoop',.21,.038,[0,0,.20],'EmissiveCyan',segments=32)
 for i in range(6):
  a=i*math.pi/3;m.box(f'Cell_{i}',[.06,.09,.05],[.15*math.cos(a),.15*math.sin(a),.08],'Bronze')
 return m

def shield_part_brace():
 m=Model('08D_Aegis_Shield_Brace_Kits');m.box('CrossBrace',[1.22,.11,.12],[0,0,0],'BrushedMetal')
 for sx in [-1,1]:m.rod(f'Rail_{sx}',[sx*.51,-.15,.03],[sx*.51,.30,.03],.05,'BlackSteel')
 m.box('Handle',[.14,.32,.12],[0,.0,-.09],'Rubber')
 return m

MODELS=[pistol(),smg(),carbine(),battle(),shotgun(),marksman(),lmg(),shield(),scifi(),pulse(),shield_part_plate(),shield_part_view(),shield_part_power(),shield_part_brace()]

def preview(model):
 # software isometric projection, sorted triangle fill, all shapes colored with diffuse shading
 W,H=930,570
 image=Image.new('RGB',(W,H),(17,22,30));draw=ImageDraw.Draw(image)
 # subtle background lines
 for yy in range(65,H,40):draw.line((38,yy,W-35,yy),fill=(27,36,45),width=1)
 # coordinate conversion: projection X and Z diagonal, Y vertical to show full 3D, but weapon front +Z mainly rightward
 # view direction angle from +X/+Y/ -Z; horizontal screen mainly Z
 vv=[]
 for _,m,_ in model.parts:
  if len(m.vertices):vv.append(m.vertices)
 arr=np.concatenate(vv,axis=0)
 # camera projection maps z mostly screen right, x also horizontal, y up
 proj=np.stack([arr[:,2]*.92+arr[:,0]*.65,arr[:,1]*-1.0+arr[:,0]*.28+arr[:,2]*.11],axis=1)
 lo=np.min(proj,axis=0);hi=np.max(proj,axis=0);sc=min((W-130)/max(.01,hi[0]-lo[0]),(H-170)/max(.01,hi[1]-lo[1]));sc=min(sc,700)
 cen=(hi+lo)/2
 faces=[]
 for _,m,k in model.parts:
  vv=m.vertices
  xy=np.stack([vv[:,2]*.92+vv[:,0]*.65,vv[:,1]*-1.0+vv[:,0]*.28+vv[:,2]*.11],axis=1)
  depth=vv[:,0]*.6+vv[:,2]*-.25+vv[:,1]*.45
  col=np.array(MATS[k][0][:3],float)
  for ids in m.faces:
   ids=np.asarray(ids)
   pts=xy[ids];d=float(np.mean(depth[ids]))
   p=vv[ids]
   n=np.cross(p[1]-p[0],p[2]-p[0]);ln=np.linalg.norm(n)
   if ln==0:continue
   n=n/ln
   bright=max(.42, min(1.25,.70+0.25*np.dot(n,np.array([.3,.7,.6]))))
   fill=tuple(np.clip(col*bright,0,255).astype(int))
   pix=[(int((t[0]-cen[0])*sc+W/2),int((t[1]-cen[1])*sc+H/2-5)) for t in pts]
   faces.append((d,pix,fill))
 for _,pp,cc in sorted(faces,key=lambda f:f[0]):draw.polygon(pp,fill=cc)
 try:
  font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',25)
  sub=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15)
 except OSError:font=ImageFont.load_default();sub=font
 draw.text((35,25),model.name.replace('_',' '),fill=(230,231,234),font=font)
 draw.text((38,H-50),'UNITY GAME ASSET  •  Y-UP  •  METERS  •  DETAILED MESH',fill=(131,156,177),font=sub)
 dest=COL/(model.name+'.png');image.save(dest,optimize=True)
 return image

if __name__=='__main__':
 for sub in ['GLB','OBJ']:(OUT/sub).mkdir(parents=True,exist_ok=True)
 thumbs=[]
 for m in MODELS:
  n,f=m.save();print(m.name,n,'parts',f,'triangles',flush=True)
  img=preview(m)
  if not m.name.startswith('08A_') and not m.name.startswith('08B_') and not m.name.startswith('08C_') and not m.name.startswith('08D_'):
   thumbs.append(img)
 w,h=530,330; sheet=Image.new('RGB',(w*2,h*5),(14,20,28))
 for i,im in enumerate(thumbs):sheet.paste(im.resize((w,h),Image.Resampling.LANCZOS),((i%2)*w,(i//2)*h))
 sheet.save(COL/'ALL_10_Asset_Overview.jpg',quality=93)
 print('Done',len(MODELS),'meshes')
