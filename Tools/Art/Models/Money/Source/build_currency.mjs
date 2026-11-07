import * as T from 'three';
import {FontLoader} from 'three/addons/loaders/FontLoader.js';
import {TextGeometry} from 'three/addons/geometries/TextGeometry.js';
import fs from 'node:fs';
import path from 'node:path';
const out=path.resolve(process.argv[2]||'outputs/LazpolyCurrency_Unity');
const font=new FontLoader().parse(JSON.parse(fs.readFileSync(new URL('./font.json',import.meta.url))));
const mats=[];
function mat(name,color,metalness=0,roughness=.45,texture=false){const m=new T.MeshStandardMaterial({name,color,metalness,roughness});m.userData.texture=texture;if(texture){m.transparent=true;m.alphaTest=.03;m.depthWrite=false;}mats.push(m);return m;}
const gold=mat('Coin_Gold','#e6b54b',.82,.28),goldLight=mat('Raised_Gold','#ffe193',.72,.25),navy=mat('Lazpoly_Navy','#073a61',.15,.34),teal=mat('Lazpoly_Teal','#087e96',.2,.38),paper=mat('Note_Ivory','#f3ecd4',0,.82),ink=mat('Note_Blue','#124d69',0,.72),pale=mat('Note_Pale','#b7d7d3',0,.85),logo=mat('Lazpoly_Logo','#ffffff',0,.42,true);
function mesh(root,name,g,m,x=0,y=0,z=0){const o=new T.Mesh(g,m);o.name=name;o.position.set(x,y,z);root.add(o);return o;}
function box(root,name,x,y,z,w,h,d,m){return mesh(root,name,new T.BoxGeometry(w,h,d),m,x,y,z);}
function cyl(root,name,x,y,z,r,h,m,n=96){return mesh(root,name,new T.CylinderGeometry(r,r,h,n,1),m,x,y,z);}
function ring(root,name,r,t,y,m){const o=mesh(root,name,new T.TorusGeometry(r,t,8,96),m,0,y,0);o.rotation.x=Math.PI/2;return o;}
function text(root,s,x,y,z,size,m,back=false,depth=.002){const g=new TextGeometry(s,{font,size,depth,curveSegments:3,bevelEnabled:false});g.computeBoundingBox();g.translate(-(g.boundingBox.max.x+g.boundingBox.min.x)/2,-(g.boundingBox.max.y+g.boundingBox.min.y)/2,0);const o=mesh(root,'Text_'+s,g,m,x,y,z);o.rotation.x=back?Math.PI/2:-Math.PI/2;return o;}
function decal(root,name,x,y,z,w,h,back=false){const o=mesh(root,name,new T.PlaneGeometry(w,h),logo,x,y,z);o.rotation.x=back?Math.PI/2:-Math.PI/2;return o;}
function border(root,name,x,z,w,h,y,m,thick=.012){box(root,name+'_N',x,y,z-h/2,w,.0015,thick,m);box(root,name+'_S',x,y,z+h/2,w,.0015,thick,m);box(root,name+'_W',x-w/2,y,z,thick,.0015,h,m);box(root,name+'_E',x+w/2,y,z,thick,.0015,h,m);}
const coin=new T.Group();coin.name='Lazpoly_Coin_1L';
cyl(coin,'Coin_Body',0,.055,0,.65,.11,gold,96);
cyl(coin,'Front_Enamel',0,.113,0,.564,.007,navy);
cyl(coin,'Reverse_Enamel',0,-.003,0,.564,.007,navy);
ring(coin,'Front_Rim',.606,.019,.114,goldLight);ring(coin,'Back_Rim',.606,.019,-.004,goldLight);
ring(coin,'Front_InnerRing',.541,.005,.118,gold);ring(coin,'Back_InnerRing',.541,.005,-.008,gold);
for(let i=0;i<64;i++){const a=i/64*Math.PI*2;const o=box(coin,'Edge_Reed_'+i,Math.cos(a)*.646,.055,Math.sin(a)*.646,.012,.072,.018,goldLight);o.rotation.y=-a;}
for(let i=0;i<40;i++){const a=i/40*Math.PI*2;for(const y of [.116,-.006])cyl(coin,'Rim_Bead',Math.cos(a)*.577,y,Math.sin(a)*.577,.007,.006,goldLight,6);}
decal(coin,'Front_Logo',0,.123,-.035,.98,.495);
text(coin,'1 L',0,.125,.325,.15,goldLight);
text(coin,'L',0,-.014,0,.59,goldLight,true,.009);
text(coin,'LAZPOLY',0,-.013,.355,.093,goldLight,true);
text(coin,'1',0,-.013,-.355,.13,goldLight,true);
// Raise all meshes so the origin is on the lowest surface.
coin.children.forEach(o=>o.position.y+=.025);
const note=new T.Group();note.name='Lazpoly_Banknote_100L';
box(note,'Paper_Edge',0,.012,0,2.8,.024,1.36,paper);
for(const back of [false,true]){
 const y=back?-.0008:.0248,sg=back?-1:1;
 box(note,'Print_Base',0,y,0,2.73,.001,1.29,pale);
 box(note,'Ivory_Field',-.17,y+sg*.001,0,2.19,.001,1.13,paper);
 box(note,'Denomination_Panel',1.058,y+sg*.002,0,.40,.001,1.13,ink);
 border(note,'Outer_Frame',0,0,2.62,1.18,y+sg*.003,ink,.012);
 border(note,'Fine_Frame',0,0,2.55,1.11,y+sg*.003,teal,.004);
 // Fine geometric guilloche lines, authored as flat print geometry.
 for(let i=0;i<14;i++){
  const pts=[];for(let k=0;k<=120;k++){const a=k/120*Math.PI*2;const r=.20+.007*i+.018*Math.sin(a*12+i*.26);pts.push(new T.Vector3(-.94+Math.cos(a)*r,y+sg*.0035,Math.sin(a)*r));}
  const curve=new T.CatmullRomCurve3(pts,false);mesh(note,'Rosette',new T.TubeGeometry(curve,120,.0014,3,false),teal);
 }
 for(let row=0;row<7;row++)for(let col=0;col<12;col++){const o=box(note,'Security_Diamond',-.52+col*.107,y+sg*.0035,-.38+row*.122,.018,.001,.018,pale);o.rotation.y=Math.PI/4;}
 decal(note,'Note_Logo',-.035,y+sg*.007,0,1.61,.813,back);
 text(note,'100',1.053,y+sg*.005,-.28*sg,.19,paper,back,.0006);
 text(note,'L',1.053,y+sg*.005,.02,.20,goldLight,back,.0006);
 text(note,'100',1.053,y+sg*.005,.30*sg,.19,paper,back,.0006);
 text(note,'100',-.96,y+sg*.005,0,.19,ink,back,.0006);
 text(note,'LAZPOLY',-.09,y+sg*.005,-.477*sg,.072,ink,back,.0006);
 text(note,back?'OYUN PARASI':'YUZ LAZPOLY',-.08,y+sg*.005,.476*sg,.058,ink,back,.0006);
 text(note,'LP 000100',-.96,y+sg*.005,.46*sg,.033,ink,back,.0006);
}
note.children.forEach(o=>o.position.y+=.01);
const assets=[coin,note];
const png=fs.readFileSync(path.join(out,'Logo/Lazpoly_Logo.png'));
function rawParts(root){
 const lower=new T.Box3().setFromObject(root).min.y;root.children.forEach(o=>o.position.y-=lower);root.updateMatrixWorld(true);
 const groups=new Map();root.traverse(o=>{if(!o.isMesh)return;const g=o.geometry.clone();g.applyMatrix4(o.matrixWorld);const a=g.attributes,mi=mats.indexOf(o.material);let p=groups.get(mi);if(!p){p={name:root.name+'__'+mats[mi].name,group:root.name,material:mi,vertices:[],normals:[],uv:[],triangles:[]};groups.set(mi,p);}const base=p.vertices.length/3,ix=g.index?Array.from(g.index.array):Array.from({length:a.position.count},(_,i)=>i);const vv=Array.from(a.position.array);if(!vv.every(Number.isFinite))throw Error('Nonfinite geometry');p.vertices.push(...vv);p.normals.push(...Array.from(a.normal.array));p.uv.push(...(a.uv?Array.from(a.uv.array):Array(a.position.count*2).fill(0)));
 for(let i=0;i<ix.length;i+=3){const ids=ix.slice(i,i+3);if(ids.some(n=>n<0||n>=a.position.count))throw Error('Bad index');const v=ids.map(n=>new T.Vector3().fromArray(vv,n*3));if(v[1].clone().sub(v[0]).cross(v[2].clone().sub(v[0])).length()<1e-14)continue;p.triangles.push(...ids.map(n=>n+base));}});return [...groups.values()];
}
function writeGlb(root,parts){const chunks=[],views=[],accessors=[];let off=0;function append(b,target){const pad=Buffer.alloc((4-b.length%4)%4);const id=views.length;views.push({buffer:0,byteOffset:off,byteLength:b.length,...(target?{target}:{})});chunks.push(b,pad);off+=b.length+pad.length;return id;}
 function attribute(a,type,n,integer=false){const b=Buffer.alloc(a.length*4);a.forEach((v,i)=>integer?b.writeUInt32LE(v,i*4):b.writeFloatLE(v,i*4));const ac={bufferView:append(b,integer?34963:34962),componentType:integer?5125:5126,count:a.length/n,type};if(type==='VEC3'){ac.min=[Infinity,Infinity,Infinity];ac.max=[-Infinity,-Infinity,-Infinity];a.forEach((v,i)=>{ac.min[i%3]=Math.min(ac.min[i%3],v);ac.max[i%3]=Math.max(ac.max[i%3],v);});}accessors.push(ac);return accessors.length-1;}
 const meshes=parts.map(p=>({name:p.name,primitives:[{attributes:{POSITION:attribute(p.vertices,'VEC3',3),NORMAL:attribute(p.normals,'VEC3',3),TEXCOORD_0:attribute(p.uv.map((x,i)=>i%2?1-x:x),'VEC2',2)},indices:attribute(p.triangles,'SCALAR',1,true),material:p.material}]}));
 const iv=append(png);const json={asset:{version:'2.0',generator:'Lazpoly Currency'},scene:0,scenes:[{nodes:[0]}],nodes:[{name:root.name,children:parts.map((_,i)=>i+1)},...parts.map((p,i)=>({name:p.name,mesh:i}))],meshes,materials:mats.map(m=>({name:m.name,pbrMetallicRoughness:{baseColorFactor:[m.color.r,m.color.g,m.color.b,1],metallicFactor:m.metalness,roughnessFactor:m.roughness,...(m.userData.texture?{baseColorTexture:{index:0}}:{})},...(m.userData.texture?{alphaMode:'MASK',alphaCutoff:.03,doubleSided:false}:{})})),textures:[{source:0,sampler:0}],samplers:[{magFilter:9729,minFilter:9987,wrapS:33071,wrapT:33071}],images:[{bufferView:iv,mimeType:'image/png'}],buffers:[{byteLength:off}],bufferViews:views,accessors};
 let jb=Buffer.from(JSON.stringify(json));jb=Buffer.concat([jb,Buffer.alloc((4-jb.length%4)%4,32)]);const bb=Buffer.concat(chunks),h=Buffer.alloc(20),bh=Buffer.alloc(8);h.write('glTF');h.writeUInt32LE(2,4);h.writeUInt32LE(28+jb.length+bb.length,8);h.writeUInt32LE(jb.length,12);h.writeUInt32LE(0x4e4f534a,16);bh.writeUInt32LE(bb.length);bh.writeUInt32LE(0x004e4942,4);fs.writeFileSync(path.join(out,'Models',root.name+'.glb'),Buffer.concat([h,jb,bh,bb]));}
let all=[],stats=[];
for(const root of assets){const parts=rawParts(root);writeGlb(root,parts);let obj=['# LAZPOLY | Y up | units: game-board scale','mtllib LazpolyCurrency.mtl'],base=1;
 for(const p of parts){obj.push('o '+p.name,'usemtl '+mats[p.material].name);for(let i=0;i<p.vertices.length;i+=3)obj.push('v '+p.vertices.slice(i,i+3).join(' '));for(let i=0;i<p.uv.length;i+=2)obj.push('vt '+p.uv.slice(i,i+2).join(' '));for(let i=0;i<p.normals.length;i+=3)obj.push('vn '+p.normals.slice(i,i+3).join(' '));for(let i=0;i<p.triangles.length;i+=3)obj.push('f '+p.triangles.slice(i,i+3).map(x=>`${x+base}/${x+base}/${x+base}`).join(' '));base+=p.vertices.length/3;
  const q=JSON.parse(JSON.stringify(p));q.vertices=q.vertices.map((x,i)=>i%3===0?-x:x);q.normals=q.normals.map((x,i)=>i%3===0?-x:x);for(let i=0;i<q.triangles.length;i+=3)[q.triangles[i+1],q.triangles[i+2]]=[q.triangles[i+2],q.triangles[i+1]];all.push(q);}
 fs.writeFileSync(path.join(out,'Models',root.name+'.obj'),obj.join('\n'));const bounds=new T.Box3().setFromObject(root);stats.push({name:root.name,triangles:parts.reduce((s,p)=>s+p.triangles.length/3,0),size:bounds.getSize(new T.Vector3()).toArray(),min:bounds.min.toArray(),max:bounds.max.toArray()});
}
fs.copyFileSync(path.join(out,'Logo/Lazpoly_Logo.png'),path.join(out,'Models/Lazpoly_Logo.png'));
fs.writeFileSync(path.join(out,'Models/LazpolyCurrency.mtl'),mats.map(m=>'newmtl '+m.name+'\nKd '+[m.color.r,m.color.g,m.color.b].join(' ')+'\nKs 0.3 0.3 0.3\nNs 40\n'+(m.userData.texture?'map_Kd Lazpoly_Logo.png\nmap_d Lazpoly_Logo.png\n':'')+'\n').join(''));
const unity=path.join(out,'Assets/LazpolyCurrency');fs.mkdirSync(path.join(unity,'Data'),{recursive:true});fs.mkdirSync(path.join(unity,'Textures'),{recursive:true});fs.copyFileSync(path.join(out,'Logo/Lazpoly_Logo.png'),path.join(unity,'Textures/Lazpoly_Logo.png'));
fs.writeFileSync(path.join(unity,'Data/LazpolyCurrencyMeshData.json'),JSON.stringify({materials:mats.map(m=>({name:m.name,color:m.color.clone().convertLinearToSRGB().toArray(),metallic:m.metalness,roughness:m.roughness,texture:!!m.userData.texture})),parts:all}));
fs.writeFileSync(path.join(out,'Validation.json'),JSON.stringify({assets:stats,logo:'Embedded PNG with alpha in each GLB; separate PNG included.',coordinates:'Y up. Unity positions/normals/winding converted.',unityEditorTested:false},null,2));
console.log(JSON.stringify(stats,null,2));
