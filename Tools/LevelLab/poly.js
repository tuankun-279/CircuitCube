/* POLY-BEGIN */
// PolyShapes.cs + PolyTopology, step for step, so tile order, corner order and links match the game exactly.
// Shapes: tetra-n, octa-n, dodeca-2 (Kilominx), dodeca-3 (Megaminx), geo-1 (soccer ball), geo-2..4 (Goldberg GP(f,0)).
// Units: board units (a square tile is 2 wide). A cell: {ports, C, N, A, verts, face, shape, poly:true}.
const POLY=(()=>{
  const PHI=(1+Math.sqrt(5))/2,TRI_SIDE=2.6,TILE_INR=1.08,MEGA_CENTRE=0.42;
  const add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]],sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]],mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s];
  const dot=(a,b)=>a[0]*b[0]+a[1]*b[1]+a[2]*b[2],cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const len=a=>Math.sqrt(a[0]*a[0]+a[1]*a[1]+a[2]*a[2]),withLen=(a,l)=>mul(a,l/len(a)),lerp=(a,b,t)=>add(a,mul(sub(b,a),t));
  const centre=p=>{let c=[0,0,0];p.forEach(v=>c=add(c,v));return mul(c,1/p.length)};
  const inradius=p=>{const c=centre(p);let best=Infinity;for(let k=0;k<p.length;k++)best=Math.min(best,len(sub(mul(add(p[k],p[(k+1)%p.length]),0.5),c)));return best};
  function Builder(){
    const polys=[],faces=[],shapes=[];
    return {polys,
      add(poly,face,shape){const c=centre(poly);let n=[0,0,0];for(let i=0;i<poly.length;i++)n=add(n,cross(poly[i],poly[(i+1)%poly.length]));
        if(dot(n,c)<0)poly.reverse();polys.push(poly);faces.push(face);shapes.push(shape)},
      scaleTo(t){let sum=0;polys.forEach(p=>sum+=inradius(p));const s=t/(sum/polys.length);for(const p of polys)for(let k=0;k<p.length;k++)p[k]=mul(p[k],s)},
      build(){return topology(polys,faces,shapes)}};
  }
  function topology(polys,faces,shapes){
    const cells=[],links=[],owner=new Map();let r=0;
    const ek=(a,b)=>{const m=mul(add(a,b),0.5);return Math.round(m[0]*64)+","+Math.round(m[1]*64)+","+Math.round(m[2]*64)};
    polys.forEach((P,i)=>{const k=P.length;let c=[0,0,0];P.forEach(v=>{c=add(c,v);r=Math.max(r,len(v))});c=mul(c,1/k);
      let n=[0,0,0];for(let p=0;p<k;p++)n=add(n,cross(P[p],P[(p+1)%k]));n=mul(n,1/len(n));
      const m0=sub(mul(add(P[0],P[1]),0.5),c),ax=sub(m0,mul(n,dot(m0,n))),A=mul(ax,1/len(ax));
      cells.push({ports:k,C:c,N:n,A,verts:P,face:faces[i],shape:shapes[i],poly:true});links.push(P.map(()=>({cell:-1,back:-1})))});
    polys.forEach((P,i)=>P.forEach((v,p)=>{const key=ek(v,P[(p+1)%P.length]),o=owner.get(key);
      if(o){links[i][p]={cell:o[0],back:o[1]};links[o[0]][o[1]]={cell:i,back:p}}else owner.set(key,[i,p])}));
    return {cells,links,blocks:[],count:cells.length,poly:true,radius:r};
  }
  function icosa(){
    const v=[];for(const a of [-1,1])for(const b of [-1,1])v.push([0,a,b*PHI],[a,b*PHI,0],[b*PHI,0,a]);
    const faces=[],edge=(i,j)=>Math.abs(len(sub(v[i],v[j]))-2)<1e-6;
    for(let i=0;i<12;i++)for(let j=i+1;j<12;j++)for(let k=j+1;k<12;k++)if(edge(i,j)&&edge(j,k)&&edge(i,k))faces.push([i,j,k]);
    return {v,faces};
  }
  function around(pts,axis){
    const a=mul(axis,1/len(axis)),r=Math.abs(a[1])<0.9?[0,1,0]:[1,0,0];let u=cross(r,a);u=mul(u,1/len(u));const w=cross(a,u);
    return pts.map(p=>({p,k:Math.atan2(dot(p,w),dot(p,u))})).sort((x,y)=>x.k-y.k).map(x=>x.p);
  }
  function dodecaFaces(){
    const {v,faces}=icosa(),cen=faces.map(f=>mul(add(add(v[f[0]],v[f[1]]),v[f[2]]),1/3));
    return v.map((vert,i)=>{const pts=[];faces.forEach((f,k)=>{if(f.includes(i))pts.push(cen[k])});return around(pts,vert)});
  }
  function triangles(faces,n){
    const b=Builder();
    faces.forEach((F,f)=>{let [p0,p1,p2]=F;if(dot(cross(sub(p1,p0),sub(p2,p0)),add(add(p0,p1),p2))<0){const t=p1;p1=p2;p2=t}
      const e1=mul(sub(p1,p0),1/n),e2=mul(sub(p2,p0),1/n),G=(i,j)=>add(add(p0,mul(e1,i)),mul(e2,j));
      for(let i=0;i<n;i++)for(let j=0;i+j<n;j++){b.add([G(i,j),G(i+1,j),G(i,j+1)],f,"Triangle");if(i+j<n-1)b.add([G(i+1,j),G(i+1,j+1),G(i,j+1)],f,"Triangle")}});
    return b.build();
  }
  function tetra(n){const s=n*TRI_SIDE/(2*Math.sqrt(2)),A=[s,s,s],B=[s,-s,-s],C=[-s,s,-s],D=[-s,-s,s];return triangles([[A,B,C],[A,D,B],[A,C,D],[B,D,C]],n)}
  function octa(n){const d=n*TRI_SIDE/Math.sqrt(2),f=[];for(const x of [1,-1])for(const y of [1,-1])for(const z of [1,-1])f.push([[x*d,0,0],[0,y*d,0],[0,0,z*d]]);return triangles(f,n)}
  function kilominx(){const b=Builder();dodecaFaces().forEach((p,f)=>{const c=centre(p);for(let k=0;k<5;k++){const cur=p[k],next=p[(k+1)%5],prev=p[(k+4)%5];
    b.add([cur,lerp(cur,next,0.5),c,lerp(cur,prev,0.5)],f,"Kite")}});b.scaleTo(TILE_INR);return b.build()}
  function megaminx(){const b=Builder();dodecaFaces().forEach((p,f)=>{const c=centre(p),inner=p.map(q=>lerp(c,q,MEGA_CENTRE));b.add(inner.slice(),f,"Pentagon");
    for(let k=0;k<5;k++){const cur=p[k],next=p[(k+1)%5],prev=p[(k+4)%5],a=lerp(cur,next,1/3),bb=lerp(cur,next,2/3),bp=lerp(prev,cur,2/3);
      b.add([a,bb,inner[(k+1)%5],inner[k]],f,"MegaEdge");b.add([cur,a,inner[k],bp],f,"MegaCorner")}});b.scaleTo(TILE_INR);return b.build()}
  function soccer(){const {v,faces}=icosa(),b=Builder();let id=0;
    v.forEach((vi,i)=>{const pts=[];v.forEach((vj,j)=>{if(i!==j&&Math.abs(len(sub(vi,vj))-2)<1e-6)pts.push(lerp(vi,vj,1/3))});b.add(around(pts,vi),id++,"Pentagon")});
    faces.forEach(f=>{const A=v[f[0]],B=v[f[1]],C=v[f[2]];
      b.add(around([lerp(A,B,1/3),lerp(A,B,2/3),lerp(B,C,1/3),lerp(B,C,2/3),lerp(C,A,1/3),lerp(C,A,2/3)],add(add(A,B),C)),id++,"Hexagon")});
    b.scaleTo(TILE_INR);return b.build()}
  function goldberg(f){
    if(f<2)return soccer();const {v,faces}=icosa(),R=len(v[0]),corners=new Map(),order=[];
    const key=q=>Math.round(q[0]*1e4)+","+Math.round(q[1]*1e4)+","+Math.round(q[2]*1e4);
    for(const t of faces){const A=v[t[0]],B=v[t[1]],C=v[t[2]];
      const P=(i,j)=>withLen(add(add(A,mul(sub(B,A),i/f)),mul(sub(C,A),j/f)),R);
      const tri=(a,b,c)=>{const cen=withLen(mul(add(add(a,b),c),1/3),R);for(const q of [a,b,c]){const k=key(q);let e=corners.get(k);if(!e){e={p:q,around:[]};corners.set(k,e);order.push(k)}e.around.push(cen)}};
      for(let i=0;i<f;i++)for(let j=0;i+j<f;j++){tri(P(i,j),P(i+1,j),P(i,j+1));if(i+j<f-1)tri(P(i+1,j),P(i+1,j+1),P(i,j+1))}}
    const b=Builder();let id=0;for(const k of order){const e=corners.get(k);b.add(around(e.around,e.p),id++,e.around.length===5?"Pentagon":"Hexagon")}
    b.scaleTo(TILE_INR);return b.build();
  }
  function build(type,n){
    if(type==="tetra")return tetra(n);if(type==="octa")return octa(n);
    if(type==="dodeca")return n===3?megaminx():kilominx();if(type==="geo")return goldberg(n);return null;
  }
  return {build,tetra,octa,kilominx,megaminx,soccer,goldberg};
})();
/* POLY-END */
if(typeof module!=="undefined")module.exports=POLY;
