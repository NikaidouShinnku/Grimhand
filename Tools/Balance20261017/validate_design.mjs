import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import {cards,talents,character} from './design.mjs';
let tests=0;const check=(name,fn)=>{fn();tests++;return name;};const names=[];
const rage={value:0,gains:0,seen:new Set(),turn:1};
function hit({card,hpLoss=1,enemy=true,attack=true,alive=true}){
 if(!alive||!enemy||!attack||hpLoss<=0||rage.seen.has(card)||rage.gains>=2)return;
 rage.seen.add(card);rage.gains++;rage.value=Math.min(4,rage.value+1);
}
function spend(max){const spent=Math.min(max,rage.value);rage.value-=spent;return spent;}
function next(){rage.turn++;rage.gains=0;rage.seen.clear();rage.value=Math.max(0,rage.value-1);}
names.push(check('32张卡ID和卡名唯一',()=>{assert.equal(cards.length,32);assert.equal(new Set(cards.map(c=>c.id)).size,32);assert.equal(new Set(cards.map(c=>c.name)).size,32);}));
names.push(check('所有卡费用、升级次数与经验有效',()=>cards.forEach(c=>{assert(Number.isInteger(c.cost)&&c.cost>=0&&c.cost<=8);assert(c.description.length>0);assert.equal(c.maxUpgrades===0,c.xp===0);}))); 
names.push(check('天赋均落在10级和两个槽位',()=>talents.forEach(t=>{assert(t.level>=1&&t.level<=10);assert([1,2].includes(t.slot));})));
names.push(check('20级HP190',()=>assert.equal(character.hp+19*character.hpPerLevel,190)));
names.push(check('护甲完全挡住不产怒',()=>{hit({card:1,hpLoss:0});assert.equal(rage.value,0);}));
names.push(check('自伤不产怒',()=>{hit({card:2,enemy:false});assert.equal(rage.value,0);}));
names.push(check('中毒灼烧不产怒',()=>{hit({card:3,attack:false});assert.equal(rage.value,0);}));
names.push(check('死亡不产怒',()=>{hit({card:4,alive:false});assert.equal(rage.value,0);}));
names.push(check('正常敌方攻击产怒',()=>{hit({card:5});assert.equal(rage.value,1);}));
names.push(check('同牌多段只产1怒',()=>{hit({card:5});hit({card:5});assert.equal(rage.value,1);}));
names.push(check('同回合自然受击最多2怒',()=>{hit({card:6});hit({card:7});assert.equal(rage.value,2);}));
names.push(check('下轮流失1并重置受击额度',()=>{next();assert.equal(rage.value,1);assert.equal(rage.gains,0);hit({card:5});assert.equal(rage.value,2);}));
names.push(check('狂怒不溢出4',()=>{rage.value=4;hit({card:8});assert.equal(rage.value,4);}));
names.push(check('消费不足取现有量且不为负',()=>{assert.equal(spend(9),4);assert.equal(rage.value,0);assert.equal(spend(2),0);}));
names.push(check('AOE整牌一次消费，不按目标重复扣',()=>{rage.value=4;const n=spend(4);const targets=[1,2,3].map(()=>12+3*n);assert.deepEqual(targets,[24,24,24]);assert.equal(rage.value,0);}));
names.push(check('多段整牌一次消费',()=>{rage.value=2;const n=spend(2);assert.deepEqual([6+2*n,6+2*n],[10,10]);assert.equal(rage.value,0);}));
names.push(check('消耗所有与最多消耗均有有限上限',()=>{for(let i=0;i<1000;i++){rage.value=Math.min(4,rage.value+1);spend(i%5);assert(rage.value>=0&&rage.value<=4);}}));
names.push(check('新牌没有0费可重复产能/抽牌',()=>assert(!cards.some(c=>c.cost===0))));
const results={status:'18项设计模型断言通过；此模型不是游戏实际实现，不证明完整平衡',count:tests,tests:names};
await fs.writeFile('outputs/grimhand-v094-design/design-model-checks.json',JSON.stringify(results,null,2));
console.log(JSON.stringify(results));
