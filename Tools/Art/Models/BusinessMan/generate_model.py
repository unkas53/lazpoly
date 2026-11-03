import os, math, json
import numpy as np
import trimesh
from trimesh.visual.material import PBRMaterial

OUT=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'exports')
os.makedirs(OUT,exist_ok=True)
scene=trimesh.Scene()
parts=[]
PAL={
 'skin':(221,146,100), 'skin_light':(239,171,123), 'skin_shadow':(181,109,76),
 'hair':(71,51,43), 'hair_light':(96,65,51), 'hair_dark':(47,38,34),
 'beard':(78,52,42), 'eye_white':(249,247,237), 'iris':(60,128,157), 'pupil':(26,33,37),
 'brow':(73,48,37), 'mouth':(74,36,37), 'teeth':(251,246,224),
 'jacket':(52,76,111), 'jacket_light':(68,94,129), 'jacket_dark':(37,57,85), 'lapel':(44,67,100),
 'shirt':(231,232,222), 'shirt_shadow':(200,211,213), 'jeans':(69,107,136),
 'jeans_light':(91,134,157), 'jeans_dark':(50,81,106), 'belt':(47,45,52),
 'shoe':(233,239,237), 'shoe_s':(198,207,213), 'sole':(252,251,241),
 'accent':(176,135,85), 'tablet':(88,105,119), 'tablet_dark':(55,67,78),
 'tablet_light':(142,158,166), 'screen':(41,53,68),
}

def add(name, mesh, color):
    c=PAL.get(color,color)
    rgba=np.array([*c,255],dtype=np.uint8)
    mesh.visual.vertex_colors=np.tile(rgba,(len(mesh.vertices),1))
    mesh.metadata['name']=name
    scene.add_geometry(mesh, node_name=name, geom_name=name)
    parts.append((name,mesh,c))
    return mesh

def icosphere(name,loc,scale,col,sub=2):
    m=trimesh.creation.icosphere(subdivisions=sub,radius=1)
    m.apply_scale(scale);m.apply_translation(loc)
    return add(name,m,col)

def loft(name, rings, col, sides=10, offset_angle=math.pi/sqrt(12) if False else 0):
    verts=[]; faces=[]
    for (cx,cy,z,rx,ry) in rings:
        for j in range(sides):
            a=2*math.pi*j/sides+offset_angle
            verts.append([cx+rx*math.cos(a),cy+ry*math.sin(a),z])
    for k in range(len(rings)-1):
        for j in range(sides):
            a=k*sides+j; b=k*sides+(j+1)%sides; c=(k+1)*sides+j; d=(k+1)*sides+(j+1)%sides
            faces.append([a,b,c]);faces.append([b,d,c])
    verts+=[list(rings[0][:3]),list(rings[-1][:3])]
    bidx=len(verts)-2;tidx=len(verts)-1
    for j in range(sides):
        faces.append([bidx,(j+1)%sides,j])
        u=(len(rings)-1)*sides
        faces.append([tidx,u+j,u+(j+1)%sides])
    return add(name,trimesh.Trimesh(vertices=verts,faces=faces,process=False),col)

def box(name,center,extents,col,rotation=None):
    m=trimesh.creation.box(extents=extents)
    if rotation:
        angle,axis=rotation
        m.apply_transform(trimesh.transformations.rotation_matrix(angle,axis))
    m.apply_translation(center)
    return add(name,m,col)

def tube(name, points, rads, col, sides=8):
    pts=np.array(points,dtype=float); vert=[];face=[]
    for i,p in enumerate(pts):
        tangent=pts[min(len(pts)-1,i+1)]-pts[max(0,i-1)]
        tangent/=np.linalg.norm(tangent)
        u=np.cross(tangent,[0,1,0])
        if np.linalg.norm(u)<.01:u=np.cross(tangent,[1,0,0])
        u/=np.linalg.norm(u)
        v=np.cross(tangent,u)
        r=rads[i]
        for j in range(sides):
            a=j*2*math.pi/sides
            vert.append(p+r*(math.cos(a)*u+math.sin(a)*v))
    for i in range(len(pts)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;c=(i+1)*sides+j;d=(i+1)*sides+(j+1)%sides
            face.extend([[a,b,c],[b,d,c]])
    vert.extend([pts[0],pts[-1]])
    for j in range(sides):
        face.append([len(vert)-2,(j+1)%sides,j]);j1=(len(pts)-1)*sides+j;j2=(len(pts)-1)*sides+(j+1)%sides
        face.append([len(vert)-1,j1,j2])
    return add(name,trimesh.Trimesh(vertices=vert,faces=face,process=False),col)

def panel(name, pts, col):
    # convex in xz plane or arbitrary polygon triangle fan
    pts=np.array(pts,float);center=np.mean(pts,axis=0)
    vs=[center.tolist()]+pts.tolist(); fs=[]
    for i in range(len(pts)):fs.append([0,i+1,(i+1)%len(pts)+1])
    m=trimesh.Trimesh(vertices=vs,faces=fs,process=False)
    if m.face_normals[:,1].mean()>0: m.invert()
    return add(name,m,col)

# World orientation: front of character is NEGATIVE Y, floor is Z=0.
# shoes: asymmetrical stance
for side,label in [(-1,'L'),(1,'R')]:
    fx=side*.119
    # padded sneaker sole, prominent forward toe
    icosphere(label+'_Shoe_sole',(fx,-.093,.057),(.102,.200,.048),'sole',2)
    icosphere(label+'_Sneaker',(fx,-.086,.102),(.094,.182,.062),'shoe',2)
    icosphere(label+'_Sneaker_toe',(fx,-.205,.089),(.091,.078,.048),'shoe_s',1)
    box(label+'_Sneaker_tongue',(fx,-.033,.141),(.084,.092,.014),'shoe_s',rotation=(-.21,[1,0,0]))
    # lace stitches
    for j in range(3):
        yy=-.083-(j*.028)
        tube(label+f'_Lace{j}',[(fx-.039,yy,.164-j*.008),(fx+.040,yy,.164-j*.008)],[.006,.006],'sole',6)
    # socks
    loft(label+'_Sock',[(fx,.028,.154,.059,.060),(fx,.034,.228,.056,.054)],'shirt',8)

# pants each leg with gentle straight-legged bends and darker back facet
for side,label in [(-1,'L'),(1,'R')]:
    x=side*.108
    loft(label+'_Denim',[
      (x,.034,.18,.062,.060),
      (x+side*.013,.041,.31,.068,.070),
      (x+side*.010,.037,.49,.073,.072),
      (x+side*.013,.045,.68,.080,.080),
      (x*.98,.051,.82,.092,.085),
      (x*.85,.049,.96,.107,.089),
    ],'jeans',9,offset_angle=0.10)
    # seam behind outsides
    tube(label+'_Denim_outer_seam',[(x+side*.059,.009,.245),(x+side*.071,.009,.5),(x+side*.080,.015,.76)],[.0035]*3,'jeans_light',6)
    # upper knee face patch small for richer appearance
    icosphere(label+'_Knee_fold',(x+side*.004,-.019,.505),(.057,.008,.027),'jeans_light',1)

# pelvis and belt
loft('Hips_denim',[(0,.062,.83,.193,.100),(0,.045,.955,.202,.111)],'jeans',12)
loft('Leather_belt',[(0,.041,.930,.209,.109),(0,.041,.964,.206,.109)],'belt',12)
box('Belt_buckle',(0,-.075,.944),(.041,.016,.035),'accent')

# jacket torso and undershirt
loft('Business_blazer',[
(0,.065,.86,.212,.117),
(0,.060,.98,.221,.137),
(0,.065,1.16,.245,.140),
(0,.055,1.32,.259,.147),
(0,.048,1.42,.239,.126),
(0,.049,1.47,.188,.107),
],'jacket',12)
# back collar
icosphere('Neck',(0,.055,1.455),(.076,.067,.105),'skin',1)
# white t-shirt triangle front panel, thin enough not overlap jacket
panel('White_Tshirt_front',[
 (-.120,-.099,.935),(.120,-.099,.935),(.135,-.098,1.235),
 (.125,-.093,1.386),(.070,-.105,1.425),(0,-.113,1.348),
 (-.069,-.105,1.425),(-.125,-.093,1.386),(-.135,-.098,1.235)
],'shirt')
# shirt V / neck collar with slight shadow
panel('Shirt_neck',[(-.072,-.118,1.428),(0,-.133,1.329),(.073,-.118,1.428),(0,-.107,1.438)],'shirt_shadow')
# jacket front two panels, with separated white shirt center
for side,label in [(-1,'L'),(1,'R')]:
    panel(label+'_Front_blazer',[ 
        (side*.108,-.109,.918), (side*.199,-.103,.927),
        (side*.217,-.089,1.264),(side*.203,-.105,1.384),
        (side*.127,-.141,1.345),(side*.079,-.140,1.217)],
        'jacket_light' if side == -1 else 'jacket')
    # collar pieces and pointed lapels
    panel(label+'_Lapel_outer',[
        (side*.209,-.115,1.426),(side*.128,-.152,1.373),
        (side*.102,-.159,1.296),(side*.194,-.147,1.202),
        (side*.151,-.148,1.335)],'lapel')
    panel(label+'_Lapel_fold',[
        (side*.213,-.124,1.432),(side*.127,-.158,1.373),
        (side*.149,-.166,1.343),(side*.236,-.114,1.400)
    ],'jacket_light')
    # flap pockets on lower blazer
    box(label+'_Blazer_pocket',(side*.158,-.084,1.018),(.092,.011,.025),'jacket_dark',rotation=(side*.035,[0,1,0]))
    box(label+'_Pocket_trim',(side*.159,-.093,1.028),(.095,.009,.009),'jacket_light')
    # little flap seam under pockets
    tube(label+'_Pocket_stitch',[(side*.111,-.101,1.003),(side*.205,-.089,1.005)],[.003,.003],'jacket_light',6)
# front buttons
for z,x in [(1.121,.072),(1.067,.075),(1.013,.076)]:
    icosphere(f'Jacket_button_{z}',(x,-.138,z),(.008,.005,.008),'accent',1)
# little chest pocket and folded cloth
box('Chest_pocket',(-.174,-.120,1.265),(.069,.01,.012),'jacket_dark',rotation=(.1,[0,1,0]))
panel('Pocket_square',[(-.190,-.139,1.282),(-.152,-.139,1.281),(-.160,-.142,1.295),(-.176,-.139,1.284)],'shirt')

# arms, folded towards tablet
for side,label in [(-1,'L'),(1,'R')]:
    shoulder=(side*.265,.024,1.404)
    elbow=(side*.314,-.044,1.172)
    wrist=(side*.171,-.342,1.105)
    icosphere(label+'_Shoulder',shoulder,(.088,.095,.087),'jacket',2)
    tube(label+'_Upper_sleeve',[shoulder,(side*.323,-.009,1.292),elbow],[.091,.085,.076],'jacket',9)
    icosphere(label+'_Elbow',elbow,(.080,.083,.086),'jacket',1)
    tube(label+'_Fore_sleeve',[elbow,(side*.270,-.195,1.131),(side*.203,-.300,1.115)],[.076,.065,.055],'jacket_light',9)
    tube(label+'_Cuff',[(side*.203,-.298,1.115),(side*.192,-.329,1.110)],[.055,.053],'shirt',8)
    icosphere(label+'_Palm',(side*.168,-.384,1.093),(.062,.044,.046),'skin',2)
    # thumb wraps front edge of the tablet
    tube(label+'_Thumb',[(side*.163,-.412,1.098),(side*.131,-.431,1.088),(side*.116,-.420,1.063)],[.023,.021,.016],'skin_light',7)
    # few visible fingers curled below tablet
    for j in range(3):
        dx=j*.018
        tube(label+f'_Finger{j}',[(side*(.174+dx),-.413,1.076-j*.006),(side*(.144+dx),-.435,1.061-j*.006)],[.012,.011],'skin',6)

# head and facial sculpture
# distinct jaw, face cheeks and ears
icosphere('Head_flesh',(0,.043,1.704),(.175,.143,.222),'skin',3)
icosphere('Chin',(0,-.005,1.560),(.133,.127,.087),'skin',2)
for side,label in [(-1,'L'),(1,'R')]:
    icosphere(label+'_Ear',(side*.174,.033,1.705),(.035,.026,.060),'skin_light',2)
    icosphere(label+'_Inner_ear',(side*.197,.009,1.704),(.008,.014,.026),'skin_shadow',1)
    icosphere(label+'_Cheek',(side*.099,-.070,1.634),(.074,.066,.070),'skin_light',2)

# beard around chin and sides, leaving cheekbones and smile visible
icosphere('Chin_beard',(0,-.029,1.526),(.136,.121,.081),'beard',2)
for side,label in [(-1,'L'),(1,'R')]:
    # side beard wraps lower jaw
    icosphere(label+'_Jaw_beard',(side*.108,-.075,1.565),(.053,.061,.090),'beard',2)
    icosphere(label+'_Sideburn',(side*.151,-.036,1.704),(.025,.031,.078),'hair',2)
    icosphere(label+'_Cheek_beard',(side*.130,-.095,1.618),(.033,.033,.053),'beard',1)

# facial features: big animated eyes; locate on front surface of face
for side,label in [(-1,'L'),(1,'R')]:
    icosphere(label+'_Eye_white',(side*.082,-.110,1.753),(.039,.026,.044),'eye_white',2)
    icosphere(label+'_Iris',(side*.080,-.134,1.753),(.019,.010,.023),'iris',2)
    icosphere(label+'_Pupil',(side*.080,-.143,1.753),(.012,.007,.017),'pupil',2)
    icosphere(label+'_Catchlight',(side*.073,-.151,1.764),(.005,.003,.006),'eye_white',1)
    tube(label+'_Brow',[(side*.040,-.102,1.821),(side*.087,-.114,1.832),(side*.129,-.097,1.819)],[.011,.015,.012],'brow',7)

# nose with a light faceted tip
icosphere('Nose_bridge',(0,-.113,1.696),(.033,.039,.062),'skin_light',2)
icosphere('Nose_tip',(0,-.146,1.665),(.041,.036,.029),'skin_light',2)
# black-smiling mouth, curved long arc, with a light teeth inset
panel('Smile_dark',[(-.101,-.148,1.629),(-.081,-.170,1.605),(-.034,-.172,1.590),(.036,-.172,1.591),(.081,-.170,1.606),(.102,-.147,1.629),(.073,-.150,1.605),(0,-.158,1.600),(-.072,-.150,1.605)],'mouth')
panel('Visible_teeth',[(-.069,-.173,1.602),(-.036,-.177,1.595),(0,-.178,1.593),(.039,-.177,1.595),(.070,-.171,1.603),(.051,-.174,1.607),(0,-.176,1.609),(-.048,-.173,1.608)],'teeth')
# small moustache sweeping around upper smile
for side,label in [(-1,'L'),(1,'R')]:
    tube(label+'_Moustache',[(side*.006,-.169,1.637),(side*.035,-.169,1.641),(side*.078,-.158,1.633)],[.013,.012,.009],'beard',7)

# hair cap and swept quiff (multiple irregular polygon spikes)
icosphere('Hair_base',(0,.055,1.895),(.182,.148,.098),'hair',2)
# shadowed hair sides above ears
for side,label in [(-1,'L'),(1,'R')]:
    icosphere(label+'_Hair_side',(side*.138,.063,1.853),(.070,.111,.101),'hair',1)
# prominent swept forelock; wedge roots to tips, thick geometric
loft('Top_quiff',[
 (-.018,-.001,1.897,.138,.097),
 (-.044,-.015,1.955,.151,.105),
 (-.066,-.041,2.012,.123,.082),
 (-.093,-.055,2.037,.071,.051),
],'hair',9)
# individual swoops to add asymmetry, not identical to reference
icosphere('Swept_hair_front',(-.060,-.080,1.955),(.117,.084,.049),'hair_light',1)
icosphere('Fringe_tip',(-.134,-.102,1.976),(.076,.045,.037),'hair',1)
for i in range(3):
    tube(f'Hair_ridge_{i}',[(.078-i*.055,-.079,1.959+i*.010),(-.023-i*.039,-.113,1.973+i*.009),(-.099-i*.028,-.109,1.966+i*.009)],
         [.014,.011,.006],'hair_light' if i==0 else 'hair_dark',5)

# Tablet lies between both hands, front-facing slate, slightly tilted backward
# Flat tablet slab, screen facing camera/front (-y)
box('Tablet_body',(0,-.378,1.141),(.335,.027,.226),'tablet',rotation=(-.12,[1,0,0]))
box('Tablet_screen',(0,-.394,1.144),(.303,.009,.193),'screen',rotation=(-.12,[1,0,0]))
box('Tablet_screen_glint',(-.087,-.401,1.198),(.101,.002,.055),'tablet_dark',rotation=(-.12,[1,0,0]))
icosphere('Tablet_camera',(0,-.402,1.241),(.005,.003,.005),'tablet_light',1)
# thumb caps at tablet front (layer depth)
for side,label in [(-1,'L'),(1,'R')]:
    icosphere(label+'_Thumb_on_front',(side*.151,-.409,1.093),(.021,.027,.021),'skin_light',1)

# GLB adheres to the glTF standard: Y UP, character faces +Z.
# OBJ keeps modeling convention Z UP / facing -Y for Blender and other 3D DCC tools.
gltf_scene=scene.copy()
gltf_scene.apply_transform(trimesh.transformations.rotation_matrix(-math.pi/2,[1,0,0]))
path=os.path.join(OUT,'LAZPOLY_BusinessMan.glb')
gltf_scene.export(path)

# Export a standard OBJ and matching MTL file with distinct colored materials.
objpath=os.path.join(OUT,'LAZPOLY_BusinessMan.obj')
mtlpath=os.path.join(OUT,'LAZPOLY_BusinessMan.mtl')
with open(mtlpath,'w') as mtl:
    mtl.write('# Original character materials.\n')
    for k,c in PAL.items():
        mtl.write(f'newmtl {k}\nKa {c[0]/255*0.2:.5f} {c[1]/255*0.2:.5f} {c[2]/255*0.2:.5f}\nKd {c[0]/255:.5f} {c[1]/255:.5f} {c[2]/255:.5f}\nKs 0.03 0.03 0.03\nNs 12.0\nd 1.0\n\n')
    # a part may use an explicit tuple color, currently all use palette
with open(objpath,'w') as f:
    f.write('# LAZPOLY Original Low-Poly Business Man; units: metres; Z UP.\nmtllib LAZPOLY_BusinessMan.mtl\n')
    voffset=1
    for name,mesh,c in parts:
        f.write(f'o {name}\nusemtl '+ next((k for k,v in PAL.items() if v==c),'jacket')+'\n')
        for x,y,z in mesh.vertices: f.write(f'v {x:.7f} {y:.7f} {z:.7f}\n')
        for face in mesh.faces:f.write('f '+' '.join(str(voffset+int(i)) for i in face)+'\n')
        voffset+=len(mesh.vertices)

import json
json.dump({'height_m':float(scene.bounds[1][2]-scene.bounds[0][2]),
 'dimensions_m':(scene.bounds[1]-scene.bounds[0]).tolist(), 'mesh_count':len(parts),
 'triangles':int(sum(len(m.faces) for _,m,_ in parts)),
 'glb_orientation':'Y up, faces +Z','obj_orientation':'Z up, faces -Y',
 'format':'GLB 2.0; OBJ+MTL; no skeleton or animations',
 'parts':[n for n,_,_ in parts]},open(os.path.join(OUT,'model_info.json'),'w'),indent=2,ensure_ascii=False)
print('Exported',path,os.path.getsize(path),'mesh parts',len(parts),'faces',sum(len(m.faces) for _,m,_ in parts),'bounds',scene.bounds,'glb bounds',gltf_scene.bounds)
