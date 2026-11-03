"""Procedural damaged furniture pack. Units in meters, Y-up for Unity via export conversion."""
import math, os, random
import numpy as np
import trimesh
from trimesh.transformations import rotation_matrix
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..'))
OUT=os.path.join(ROOT,'Models');os.makedirs(OUT,exist_ok=True)
wood=(92,58,39,255);wood2=(125,82,52,255);wood_edge=(61,42,32,255);metal=(75,80,80,255);metal2=(111,112,105,255);fabric=(72,80,76,255);fabric2=(101,88,78,255);foam=(166,139,95,255);dark=(40,41,41,255)
class Asset:
 def __init__(self,name):self.name=name;self.parts=[];self.count=0
 def add(self,mesh,color):
  mesh.visual.vertex_colors=np.tile(np.array(color,dtype=np.uint8),(len(mesh.vertices),1));mesh.metadata['name']=f'part_{self.count:03d}';self.count+=1;self.parts.append(mesh)
 def box(self,center,size,color,tilt=0,axis=(0,1,0)):
  m=trimesh.creation.box(extents=size);m.apply_transform(rotation_matrix(tilt,axis));m.apply_translation(center);self.add(m,color)
 def tube(self,a,b,radius,color,sections=10):
  d=np.subtract(b,a);m=trimesh.creation.cylinder(radius=radius,height=float(np.linalg.norm(d)),sections=sections);m.apply_transform(trimesh.geometry.align_vectors([0,0,1],d));m.apply_translation((np.array(a)+b)/2);self.add(m,color)
 def chip(self,c,size,color,rot=.25):self.box(c,size,color,tilt=rot)
 def export(self):
  # authored Z up; make Y up for Unity
  conv=rotation_matrix(-math.pi/2,(1,0,0))
  scene=trimesh.Scene()
  for i,m in enumerate(self.parts):
   m=m.copy();m.apply_transform(conv);scene.add_geometry(m,node_name=f'{self.name}_piece_{i:03}',geom_name=f'{self.name}_piece_{i:03}')
  glb=os.path.join(OUT,self.name+'.glb');scene.export(glb)
  # OBJ complementary format; flatten with vertex colors where supported
  joined=trimesh.util.concatenate([p.copy() for p in self.parts]);joined.apply_transform(conv)
  joined.export(os.path.join(OUT,self.name+'.obj'))
  return scene

def chair():
 a=Asset('Broken_Dining_Chair');a.box((0,0,.45),(.52,.50,.09),wood)
 for x,y in [(-.21,-.19),(.21,-.19),(-.21,.19)]:a.box((x,y,.225),(.065,.065,.45),wood2)
 a.box((.27,.30,.21),(.06,.06,.4),wood2,tilt=.55,axis=(1,0,0))
 for z in [.74,.97]:a.box((0,.22,z),(.57,.065,.08),wood)
 a.box((-.23,.22,.77),(.07,.07,.64),wood2)
 a.box((.25,.22,.82),(.07,.07,.60),wood2,tilt=.1)
 a.chip((.34,.10,.04),(.33,.09,.06),wood_edge,.63);a.chip((-.37,.34,.03),(.19,.09,.04),wood2,-.37)
 return a

def table():
 a=Asset('Collapsed_Table');a.box((0,0,.71),(1.45,.78,.09),wood)
 a.box((-.48,-.28,.35),(.085,.085,.7),wood2);a.box((-.48,.28,.35),(.085,.085,.7),wood2)
 a.box((.52,-.28,.38),(.09,.09,.73),wood2,tilt=.19,axis=(0,1,0))
 a.box((.69,.36,.18),(.10,.1,.68),wood_edge,tilt=1.0,axis=(0,1,0))
 for x,y,t in [(.52,.04,.25),(.7,-.13,-.4),(.66,.34,.53)]:a.chip((x,y,.665),(.34,.18,.025),wood_edge,t)
 a.box((.64,.25,.70),(.38,.32,.05),wood2,tilt=-.22)
 a.chip((1.0,.35,.035),(.58,.08,.05),wood2,.5)
 return a

def bookshelf():
 a=Asset('Smashed_Bookshelf');a.box((0,0,.06),(1.05,.38,.12),wood_edge)
 a.box((-.51,0,.94),(.1,.4,1.82),wood);a.box((.51,0,.81),(.10,.4,1.58),wood,tilt=.11,axis=(0,1,0))
 a.box((0,-.15,1.77),(1.05,.11,.08),wood2,tilt=-.13,axis=(0,1,0))
 for z in [.47,.98]:a.box((0,0,z),(.99,.36,.07),wood2)
 a.box((.11,0,1.4),(.94,.35,.07),wood2,tilt=.12,axis=(0,1,0))
 a.box((-.1,.17,.91),(.8,.05,1.65),wood_edge)
 for i in range(7):
  x=-.35+i*.105;a.box((x,-.04,.58),(.075,.20,.18+i%3*.055),[wood2,fabric2,wood][i%3],tilt=.1*i,axis=(0,1,0))
 for i in range(5):a.chip((-.85+i*.22,.54,.035),(.22,.08,.045),wood2,.3*i)
 return a

def sofa():
 a=Asset('Torn_Sofa');a.box((0,0,.35),(1.87,.83,.45),fabric)
 a.box((0,.36,.82),(1.85,.18,.96),fabric)
 for x in [-.91,.91]:a.box((x,0,.58),(.19,.90,.63),fabric2)
 for x,t in [(-.56,.10),(.02,-.05),(.60,.24)]:a.box((x,-.12,.61),(.59,.62,.17),fabric2,tilt=t)
 a.box((.46,.23,.87),(.49,.06,.26),foam,tilt=.26,axis=(0,1,0))
 for i in range(5):a.box((.28+i*.115,.145,.91),(.08,.04,.025),dark,tilt=.3)
 for x in [-.7,.7]:a.box((x,0,.09),(.08,.08,.18),wood_edge)
 a.box((1.02,-.31,.065),(.46,.20,.09),fabric,tilt=.4)
 return a

def bench():
 a=Asset('Broken_Waiting_Bench');a.box((-.43,0,.49),(.95,.43,.08),wood2);a.box((.47,.04,.40),(.96,.43,.08),wood2,tilt=-.22,axis=(1,0,0))
 for x in [-.76,.72]:a.box((x,0,.25),(.085,.085,.49),metal)
 a.box((-.02,.22,.78),(1.74,.065,.14),wood);a.box((-.02,.22,1.03),(1.74,.065,.13),wood)
 for x in [-.76,.76]:a.box((x,.20,.72),(.08,.08,.71),metal2)
 a.chip((.18,-.57,.042),(.5,.10,.07),wood2,.42)
 a.chip((1.02,-.33,.03),(.26,.08,.05),wood_edge,-.55)
 return a

def cabinet():
 a=Asset('Ransacked_Cabinet');a.box((0,0,.68),(1.18,.52,1.36),metal)
 a.box((0,-.265,.07),(1.13,.045,.12),metal2)
 a.box((-.3,-.3,.76),(.55,.045,.64),metal2,tilt=.28,axis=(0,0,1))
 a.box((.29,-.24,.78),(.53,.05,.66),metal2,tilt=-.38,axis=(0,0,1))
 a.box((0,-.56,1.14),(.90,.52,.07),metal)
 a.box((.1,-.48,.91),(.82,.42,.08),dark)
 for x in [-.34,.34]:a.tube((x,-.34,.80),(x+.05,-.39,.89),.019,wood_edge)
 for i in range(4):a.chip((-.55+i*.33,-.91,.025),(.22,.15,.035),metal2,.2*i)
 return a

def barricade():
 a=Asset('Makeshift_Barricade');a.box((0,.04,.39),(1.9,.12,.80),wood_edge)
 for i,(z,ang) in enumerate([(.15,.07),(.43,-.16),(.71,.20),(.98,-.08)]):
  a.box((0,-.13,z),(2.12,.13,.19),wood2 if i%2 else wood,tilt=ang,axis=(0,1,0))
 for x in [-.8,-.1,.66]:a.box((x,.02,.58),(.09,.17,1.12),metal,tilt=.08*x,axis=(0,1,0))
 for x,z in [(-.72,.12),(.72,.5),(.2,.82)]:a.tube((x,-.26,z),(x,-.40,z+.04),.033,dark)
 for i in range(5):a.chip((-.85+i*.43,-.65,.03),(.25,.08,.05),wood_edge,(i-2)*.23)
 return a

def crate():
 a=Asset('Splintered_Crate');a.box((0,0,.31),(.75,.65,.62),wood_edge)
 for z in [.1,.29,.49]:
  for y in [-.34,.34]:a.box((0,y,z),(.81,.035,.155),wood2)
 for x in [-.39,.39]:a.box((x,0,.33),(.04,.68,.65),wood)
 a.box((-.20,0,.65),(.37,.73,.06),wood2,tilt=-.28,axis=(0,1,0))
 a.box((.27,.02,.82),(.39,.73,.07),wood2,tilt=.42,axis=(0,1,0))
 for i in range(4):a.chip((-.5+i*.29,-.6,.025),(.21,.05,.035),wood_edge,.3*i)
 return a

if __name__=='__main__':
 assets=[f() for f in (chair,table,bookshelf,sofa,bench,cabinet,barricade,crate)]
 for a in assets:
  a.export();print(a.name,len(a.parts))
 combined=trimesh.Scene()
 # create pre-arranged room clutter set with rotated+translated instances
 placements=[(-2.2,-1.6,0),(-.5,-1.6,0),(1.3,-1.4,0),(-2.2,.6,0),(.15,.9,0),(2,.6,0),(-1.0,2.2,0),(1.7,2.1,0)]
 for a,(x,y,z) in zip(assets,placements):
  for j,m in enumerate(a.parts):
   m=m.copy();m.apply_translation([x,y,z]);m.apply_transform(rotation_matrix(-math.pi/2,(1,0,0)))
   combined.add_geometry(m,node_name=f'{a.name}_{j:03}')
 combined.export(os.path.join(OUT,'Combined_Room_Clutter.glb'))
 print('DONE')
