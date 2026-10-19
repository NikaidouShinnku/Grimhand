'use strict';
window.DelverWorld=(()=>{
const BIOMES={grass:{name:'翡翠草庭',color:'#577b4c',edge:'#9cca70',index:0},snow:{name:'霜白雪原',color:'#91afb6',edge:'#d7f3ff',index:1},volcano:{name:'熔火裂谷',color:'#59423a',edge:'#ff9b4c',index:2},demon:{name:'幽紫魔域',color:'#503b68',edge:'#cc9aff',index:3},blood:{name:'猩红血境',color:'#663c48',edge:'#f69d9f',index:4}};
const size=e=>({w:e?.w||e?.size||1,h:e?.h||e?.size||1});
const rect=e=>({...e,...size(e)});
function contains(e,x,y){const r=rect(e);return x>=r.x&&x<r.x+r.w&&y>=r.y&&y<r.y+r.h;}
function overlaps(a,b){a=rect(a);b=rect(b);return a.x<b.x+b.w&&b.x<a.x+a.w&&a.y<b.y+b.h&&b.y<a.y+a.h;}
function distance(a,b){a=rect(a);b=rect(b);return Math.max(0,a.x-b.x-b.w+1,b.x-a.x-a.w+1)+Math.max(0,a.y-b.y-b.h+1,b.y-a.y-a.h+1);}
function center(e){const s=size(e);return{x:e.x+s.w/2,y:e.y+s.h/2};}
function cells(e){const r=rect(e),out=[];for(let y=r.y;y<r.y+r.h;y++)for(let x=r.x;x<r.x+r.w;x++)out.push({x,y});return out;}
function biomeAt(x,y,w,h){const seeds=[['grass',.18,.25],['snow',.68,.13],['volcano',.85,.74],['demon',.12,.84],['blood',.49,.67]];let best='grass',d=Infinity;for(const[id,sx,sy]of seeds){const n=(x/w-sx)**2+(y/h-sy)**2;if(n<d){best=id;d=n;}}return best;}
// Allegiance is relative to the viewer, not whether a creature has any owner.
const RELATION_COLORS={friendly:'#76e39b',hostile:'#f07878',neutral:'#efcf70'};
function relation(entity,viewer){if(entity===viewer||entity.player&&entity.index===viewer?.index)return 'friendly';if(entity.boss)return 'hostile';if(!entity.player&&entity.owner>=0){if(entity.owner!==viewer?.index)return 'hostile';return entity.friendly?'friendly':entity.anger?.owner?'hostile':'neutral';}if(entity.neutral===true&&!entity.provoked)return 'neutral';return 'hostile';}
function relationColor(entity,viewer){return RELATION_COLORS[relation(entity,viewer)];}
function facing(a,b){const c=center(a),d=center(b),dx=d.x-c.x,dy=d.y-c.y;return Math.abs(dx)>=Math.abs(dy)?{x:dx<0?-1:1,y:0}:{x:0,y:dy<0?-1:1};}
function attackArea(a,b,depth=2,width=3){const dir=facing(a,b),cx=Math.floor(a.x+(size(a).w-1)/2),cy=Math.floor(a.y+(size(a).h-1)/2),out=[];for(let n=1;n<=depth;n++)for(let j=-Math.floor(width/2);j<=Math.floor(width/2);j++)out.push({x:cx+dir.x*n-dir.y*j,y:cy+dir.y*n+dir.x*j});return out;}
function visible(state,observer,pos,radius=8){
 if(!observer||observer.hp<=0||state.tiles[pos.y]?.[pos.x]?.void)return false;
 const a=center(observer),r=rect(pos),tx=Math.max(r.x+.5,Math.min(a.x,r.x+r.w-.5)),ty=Math.max(r.y+.5,Math.min(a.y,r.y+r.h-.5));
 const d=Math.hypot(tx-a.x,ty-a.y);if(d<=1.1)return true;if(d>radius)return false;
 const clouds=state.darkness||[];const inside=clouds.some(c=>Math.hypot(a.x-c.x-.5,a.y-c.y-.5)<=c.radius);if(inside&&d>2)return false;
 const steps=Math.ceil(Math.max(Math.abs(tx-a.x),Math.abs(ty-a.y))*3);
 for(let i=1;i<=steps;i++){const px=a.x+(tx-a.x)*i/steps,py=a.y+(ty-a.y)*i/steps,x=Math.floor(px),y=Math.floor(py);if(clouds.some(c=>Math.hypot(px-c.x-.5,py-c.y-.5)<=c.radius))return false;if(i<steps&&!contains(observer,x,y)&&!(x===Math.floor(tx)&&y===Math.floor(ty))&&(state.tiles[y]?.[x]?.wall||state.tiles[y]?.[x]?.void))return false;}
 return true;
}
return{BIOMES,facing,attackArea,relation,relationColor,RELATION_COLORS,size,rect,contains,overlaps,distance,center,cells,biomeAt,visible};
})();
