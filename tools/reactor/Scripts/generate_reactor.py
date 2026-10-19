"""Generate a 6m game-environment reactor in OBJ/MTL and GLB formats.
Dependencies: pip install numpy trimesh
Run: python generate_reactor.py
"""
from pathlib import Path
import math, numpy as np, trimesh
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Models';OUT.mkdir(exist_ok=True)
PARTS=[]
COLORS={
 'CharcoalMetal':(38,45,50,255),'Gunmetal':(71,78,82,255),
 'DarkSteel':(24,29,34,255),'ScratchedSteel':(110,116,117,255),
 'Copper':(113,66,40,255),'Rust':(100,49,32,255),
 'Glass':(68,145,152,65),'PlasmaOrange':(255,85,13,255),
 'PlasmaGold':(255,171,31,255),'WarningYellow':(206,157,41,255),
 'IndicatorCyan':(48,205,225,255),'Rubber':(17,19,23,255),
}
def add(name,mesh,mat):
 mesh=mesh.copy();mesh.visual.vertex_colors=np.tile(COLORS[mat],(len(mesh.vertices),1)); PARTS.append((name,mesh,mat))
def cyl(name,r,h,y,mat,sections=24,x=0,z=0):
 m=trimesh.creation.cylinder(radius=r,height=h,sections=sections);m.apply_transform(trimesh.transformations.rotation_matrix(-math.pi/2,[1,0,0]));m.apply_translation((x,y,z));add(name,m,mat)
def box(name,ext,xyz,mat):
 m=trimesh.creation.box(extents=ext);m.apply_translation(xyz);add(name,m,mat)
def ring(name,major,tube,y,mat,segments=48,sides=8):
 v=[];f=[]
 for i in range(segments):
  a=2*math.pi*i/segments
  for j in range(sides):
   b=2*math.pi*j/sides;rr=major+tube*math.cos(b)
   v.append([rr*math.cos(a),y+tube*math.sin(b),rr*math.sin(a)])
 for i in range(segments):
  for j in range(sides):
   a=i*sides+j;b=((i+1)%segments)*sides+j;c=((i+1)%segments)*sides+(j+1)%sides;d=i*sides+(j+1)%sides
   f.extend([[a,b,c],[a,c,d]])
 add(name,trimesh.Trimesh(vertices=v,faces=f,process=False),mat)
def rod(name,p,q,r,mat):
 p=np.array(p);q=np.array(q);d=q-p;h=np.linalg.norm(d)
 m=trimesh.creation.cylinder(radius=r,height=h,sections=10)
 T=trimesh.geometry.align_vectors([0,0,1],d/h);m.apply_transform(T);m.apply_translation((p+q)/2);add(name,m,mat)
def orb(name,xyz,scale,mat):
 m=trimesh.creation.icosphere(subdivisions=2,radius=1.0);m.apply_scale(scale);m.apply_translation(xyz);add(name,m,mat)
# 6 m tall, Y-up, origin floor center
cyl('Foundation_Base',.98,.16,.09,'DarkSteel',48)
cyl('Foundation_Bevel',.84,.14,.23,'Gunmetal',48)
cyl('Lower_Pedestal',.73,.56,.51,'CharcoalMetal',48)
cyl('Lower_Socket',.8,.15,.84,'ScratchedSteel',48)
cyl('Upper_Cap',.79,.22,5.25,'ScratchedSteel',48)
cyl('Upper_Pedestal',.73,.49,5.61,'CharcoalMetal',48)
cyl('Ceiling_Mount',.98,.17,5.955,'DarkSteel',48)
# Glass core visual cylinder: Unity glass material transparent preferred
cyl('Glass_Chamber',.58,4.13,3.04,'Glass',48)
cyl('Plasma_Inner_Glow',.43,3.89,3.04,'PlasmaOrange',32)
# structural rings
for i,y in enumerate([.92,1.02,2.04,3.04,4.04,5.04,5.15]):
 ring('Brace_Ring_%02d'%i,.66,.065,y,'Gunmetal')
 ring('Bright_Gasket_%02d'%i,.583,.021,y,'Copper')
# support columns and edge accents
for k in range(8):
 a=2*math.pi*k/8;x,z=.755*math.cos(a),.755*math.sin(a)
 rod('Vertical_Support_%02d'%k,[x,.9,z],[x,5.2,z],.045,'DarkSteel')
 if k%2==0:
  rod('Copper_Conduit_%02d'%k,[.84*math.cos(a),1.02,.84*math.sin(a)],[.84*math.cos(a),5.04,.84*math.sin(a)],.018,'Copper')
for k in range(12):
 a=2*math.pi*k/12
 for y in [.26,.79,5.35,5.82]:
  x,z=.73*math.cos(a),.73*math.sin(a)
  orb('Rivet_%02d_%02d'%(k,int(y*100)),[x,y,z],[.036,.036,.036],'ScratchedSteel')
# plasma globules distinct objects can be animated in Unity
for i,(y,x,z,s) in enumerate([(1.48,.08,0,.28),(2.21,-.1,.10,.23),(3.02,.12,-.03,.32),(3.86,-.04,-.11,.24),(4.57,.04,.11,.22)]):
 orb('Plasma_Blob_%02d'%i,[x,y,z],[s*.8,s*1.3,s*.8], 'PlasmaGold' if i%2 else 'PlasmaOrange')
# front facing control bracket on positive Z
box('Control_Mount',[.65,.72,.18],[0,1.43,.83],'DarkSteel')
box('Control_Screen',[.48,.29,.014],[0,1.58,.933],'IndicatorCyan')
for i in range(3):
 box('Control_Key_%d'%i,[.105,.085,.02],[-.17+i*.17,1.30,.937],'WarningYellow' if i==2 else 'Gunmetal')
box('Control_Status',[.4,.035,.025],[0,1.09,.939],'PlasmaOrange')
# warning plates
for x in [-.56,.56]:
 box('Warning_Plate_%s'%('L' if x<0 else 'R'),[.24,.11,.04],[x,.57,.56],'WarningYellow')
# cross supports to base
for i in range(8):
 a=2*math.pi*i/8
 rod('Lower_Angled_Brace_%d'%i,[.91*math.cos(a),.23,.91*math.sin(a)],[.7*math.cos(a),.88,.7*math.sin(a)],.065,'Gunmetal')
# export GLB scene, separate geometries and names
scene=trimesh.Scene()
for name,mesh,mat in PARTS:scene.add_geometry(mesh,geom_name=name,node_name=name)
(OUT/'Reactor_01.glb').write_bytes(scene.export(file_type='glb'))
# OBJ with custom explicit materials and individual object groups
with (OUT/'Reactor_01.obj').open('w') as o:
 o.write('# Reactor_01 Unity asset; Y-up, meters\nmtllib Reactor_01.mtl\n')
 offset=1
 for name,mesh,mat in PARTS:
  o.write(f'o {name}\nusemtl {mat}\n')
  for v in mesh.vertices:o.write('v %.6f %.6f %.6f\n'%tuple(v))
  for tri in mesh.faces:o.write('f %d %d %d\n'%tuple(tri+offset))
  offset+=len(mesh.vertices)
with (OUT/'Reactor_01.mtl').open('w') as o:
 for mat,col in COLORS.items():
  r,g,b,a=[v/255 for v in col];o.write(f'newmtl {mat}\nKd {r:.4f} {g:.4f} {b:.4f}\nd {a:.4f}\nillum 2\nNs 70\n\n')
print('Generated',len(PARTS),'meshes,',sum(len(m.faces) for _,m,_ in PARTS),'triangles')
