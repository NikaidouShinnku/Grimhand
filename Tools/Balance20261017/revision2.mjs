import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
import {cards as oldCards} from './design.mjs';
const output='outputs/grimhand-v094-design/revision-02';
const overrides={
 b_break_mountain:{id:'b_duel_to_end',name:'不死不休',cost:3,type:'状态',description:'【消耗】与一名敌人【死斗】。它被击败后，若战斗继续，下回合从弃牌堆取回最多3张自己的攻击牌，使其在手中时费用为0。',maxUpgrades:0,upgrade:'不可升级',xp:0},
 b_unbroken_assault:{id:'b_berserker_soul',name:'狂战之魂',cost:5,type:'状态',description:'【消耗】本场战斗中，裂斧的非消耗攻击牌获得【继承】，但她无法再使用防御牌。',maxUpgrades:0,upgrade:'不可升级',xp:0},
 b_ancestral_guard:{id:'b_burning_standard',name:'燃尽战旗',cost:4,type:'状态',description:'【消耗】从下回合起，本场战斗中狂怒不再流失，改为队伍的额外能量：每点可抵扣1费，每张牌最多抵扣2费。狂怒不再用于强化卡牌效果。',maxUpgrades:0,upgrade:'不可升级',xp:0},
 b_break_line:{description:'【前/中】造成19伤害。目标存活且能被换位时，与其身后一格的敌人交换位置。',upgrade:'伤害+2'},
 b_tempered_rage:{description:'【消耗】本场战斗中，每回合第一次通过卡牌耗尽狂怒后，下回合抽牌后，从自己的弃牌堆选择1张防御牌加入手牌。',maxUpgrades:0,upgrade:'不可升级',xp:0},
 b_last_reserve:{description:'获得14护甲。本回合结束时，留住手中未打出的裂斧攻击牌。'},
 b_axe_execute:{description:'【前/中】造成20伤害。击败目标后，下回合抽牌后，从自己的弃牌堆选择1张1费或以下的攻击牌加入手牌。',upgrade:'伤害+2'},
 b_two_hacks:{name:'回斧连斩'}
};
const cards=oldCards.map(c=>({...c,...(overrides[c.id]??{}),status:'第二轮提案，未实装/未通过角色实战验证'}));
const talents=[
 [1,1,'余烬','每场战斗开始时获得1点狂怒。'],
 [3,1,'钢铁怒意','成功应对敌方攻击且未损失HP，也能触发承压生怒；仍同牌一次、每回合由此最多2点，不重复获取受击奖励。'],
 [5,1,'血性','自身HP不高于50%时，承压生怒每次改为获得2点；每回合由此获得总量仍最多2点。'],
 [7,1,'踏阵','每回合首次从中/后排换到前排时获得1点狂怒。'],
 [9,1,'压住杀意','本回合未使用裂斧的攻击牌时，下回合开始不自然流失狂怒。'],
 [10,1,'怒而不竭','狂怒仅在偶数回合开始自然流失1点。'],
 [2,2,'收势护身','每回合首次通过非攻击牌消耗至少1点狂怒后，获得4护甲。'],
 [4,2,'乘胜','裂斧用实际消耗过狂怒的攻击牌击败敌人后，返还1点狂怒；每回合最多一次，整张AOE也最多一次。'],
 [6,2,'凿甲','每回合首次通过攻击牌消耗至少2点狂怒时，在该牌首次伤害前移除主目标6护甲；不按每段/每目标重复。'],
 [8,2,'并肩','每回合首次被其他友方实际治疗HP时，获得1点狂怒；不计自疗、溢出治疗，不重复计联动回调。'],
 [10,2,'留有后招','每回合首次通过卡牌把狂怒从正数耗至0后，下回合开始额外抽1张牌。费用抵扣也只计一次。']
].map(([level,slot,name,description])=>({level,slot,name,description}));
const character={id:'char_berserker',name:'裂斧',title:'荒原狂战士',baseHp:76,baseSpd:5,hpPerLevel:6,
 levels:Array.from({length:20},(_,i)=>({level:i+1,hp:76+6*i,spd:5})),
 startingDeck:[{id:'b_axe_cut',count:3},{id:'b_iron_guard',count:2},{id:'b_counter_cut',count:1},{id:'b_hold_anger',count:1}],
 innate:{id:'pressure_to_rage',name:'承压生怒',usesTalentSlot:false,description:'受到敌方攻击并损失HP后获得1点狂怒；同牌一次，每回合由此最多2点。上限4；第2回合起，每回合开始流失1点。',excluded:['完全护甲吸收','中毒','灼烧','反伤','献祭','自伤','友伤'],death:'清零',resolution:'强化消费在卡牌执行时发生一次，覆盖整牌所有段/目标；不能提前消费预期受击所得。'},
 playerFacingUI:{camp:'HP/SPD下方单列固有能力，不放进天赋槽',selection:'角色卡写明承压积怒、反攻定位',battle:'独立狂怒图标、4格计量槽、0/4数字；获取/消耗/流失有不同提示',cards:'基础伤害与当前怒强化分开预览，不保证未来受击所得',legendaryPayment:'燃尽战旗下回合生效，草稿中显式开启狂怒抵扣，确认时预留；X费不参加'},
 status:'第二轮设计提案。当前JSON不是Unity资产导入器；未修改运行时代码。'};
const icons=[
 {id:'rage',name:'狂怒',visual:'铁护手握拳＋两道红刻痕；有限资源，不能画成剑与增伤箭头',display:'角色下方0/4和四格槽',fileStatus:'已提供32/128像素候选及16/24/32尺寸对照；待Unity验收',file:'icons/rage.png'},
 {id:'duel',name:'死斗',visual:'两把对向斧或交叉锁环',display:'双方同标记；悬停写明合法目标限制和结束条件',fileStatus:'语义与规格方案，尚未制作'},
 {id:'berserker_soul',name:'狂战之魂',visual:'保留的攻击牌＋划掉的盾',display:'继承与自身防御禁用同时说明',fileStatus:'语义与规格方案，尚未制作'},
 {id:'burning_standard',name:'燃尽战旗',visual:'短红旗＋能量符号',display:'共享能量区显示狂怒抵扣模式、金额与预留量',fileStatus:'语义与规格方案，尚未制作'}
];
assert.equal(cards.length,32);assert.equal(new Set(cards.map(c=>c.id)).size,32);assert.equal(new Set(cards.map(c=>c.name)).size,32);
assert.equal(character.startingDeck.reduce((a,b)=>a+b.count,0),7);
assert(character.startingDeck.every(d=>cards.some(c=>c.id===d.id)));
assert.equal(character.levels.at(-1).hp,190);assert(talents.every(t=>t.level>=1&&t.level<=10&&[1,2].includes(t.slot)));
assert(cards.filter(c=>c.rarity==='橙').every(c=>c.maxUpgrades===0));
await fs.mkdir(output,{recursive:true});
await fs.writeFile(`${output}/character-design.json`,JSON.stringify({character,cards,talents,icons,verification:'仅数据完整性断言通过。旧版18项模型和4680场旧五角色模拟不验证本轮新角色机制。'},null,2));
const text=['# 第二轮完整卡池与天赋','', '本文件统一列出第二轮32张牌。它们仍是提案，复杂交互以同目录角色重设计文档为准，不是已实装卡牌。','',
 `基础：HP${character.baseHp}，SPD${character.baseSpd}；每级HP+6，20级HP190。固有能力：${character.innate.description}`,'',
 '## 卡牌','',...cards.flatMap(c=>[`### ${c.name}（${c.rarity}，${c.cost}费，${c.type}）`,'',c.description,'',`升级：${c.maxUpgrades?`最多${c.maxUpgrades}次，${c.upgrade}，${c.xp}XP/级`:'不升级'}。`,'']),
 '## 天赋','',...talents.flatMap(t=>[`### 槽位${t.slot}：${t.name}（局外${t.level}级）`,'',t.description,'']),
 '## 实现前必须验证','',
 '死斗的Boss点名优先级、目标死亡后清除限制、下轮弃牌回收与手牌上限；狂战之魂的继承/消耗/禁用防御、临时费用恢复；燃尽战旗的计划预留/撤销、X费排除、没有追溯改价、角色死亡；淬怒与其他回收效果不得复制同一实例。', '',
 '这些机制未进入游戏引擎，也未经过角色实战模拟。不得继承旧模型“通过”的标签。'];
await fs.writeFile(`${output}/完整卡池与天赋.md`,text.join('\n'));
console.log(JSON.stringify({cards:cards.length,talents:talents.length,hpAt20:190,startingDeck:7,legendary:cards.filter(c=>c.rarity==='橙').map(c=>c.name),validation:'data-integrity-only'}));
