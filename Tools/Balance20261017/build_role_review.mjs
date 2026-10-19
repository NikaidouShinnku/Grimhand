import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const dir='outputs/grimhand-v094-design/revision-02';
const d=JSON.parse(await fs.readFile(`${dir}/character-design.json`,'utf8'));
const wb=Workbook.create();
const col=n=>String.fromCharCode(64+n);
function tab(name,headers,rows,widths){
 const s=wb.worksheets.add(name);s.showGridLines=false;
 s.getRange('A2').values=[[name]];s.getRange('A2').format.font={name:'Arial',size:14,bold:true};
 const end=rows.length+4,last=col(headers.length);
 s.getRange(`A4:${last}${end}`).values=[headers,...rows];
 s.getRange(`A4:${last}${end}`).format.font={name:'Arial',size:11};
 s.getRange(`A4:${last}${end}`).format.verticalAlignment='center';
 s.getRange(`A4:${last}${end}`).format.wrapText=true;
 s.getRange(`A4:${last}4`).format={fill:'#36313D',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},rowHeight:30,horizontalAlignment:'center'};
 widths.forEach((w,i)=>s.getRange(`${col(i+1)}4:${col(i+1)}${end}`).format.columnWidth=w);
 rows.forEach((row,i)=>{const lines=Math.max(...row.map((v,j)=>String(v??'').split('\n').reduce((n,t)=>n+Math.max(1,Math.ceil([...t].reduce((a,c)=>a+(c.charCodeAt(0)>255?2:1),0)/(widths[j]-2))),0)));s.getRange(`A${i+5}:${last}${i+5}`).format.rowHeight=Math.max(32,lines*17+12);if(i%2===1)s.getRange(`A${i+5}:${last}${i+5}`).format.fill='#F4F1F5';});
 s.freezePanes.freezeRows(4);return s;
}
const hero=tab('角色与成长',['项目','设计内容'],[
 ['角色','裂斧／荒原狂战士，前排承压、反攻；单把双刃斧'],
 ['版本状态','第二轮设计评审。尚未实装，数值与新机制未通过Unity实战校准。'],
 ['初始HP',d.character.baseHp],['每级HP成长',d.character.hpPerLevel],['SPD',d.character.baseSpd],
 ['固有能力',`${d.character.innate.name}：${d.character.innate.description}`],
 ['天赋关系','固有能力不占天赋槽。两个天赋槽各选一个已解锁节点，不是整棵树同时生效。'],
 ['初始牌组',d.character.startingDeck.map(x=>`${d.cards.find(c=>c.id===x.id).name}×${x.count}`).join('，')],
 ['共同框架','三人共享牌组、手牌上限与能量；沿用现有经验曲线。无新增ATK、DEF或通用站位增伤。'],
 ['资源排除',d.character.innate.excluded.join('、')+'均不产生固有能力的狂怒。死亡及战斗结束清零。'],
 ['结算时机',d.character.innate.resolution],
 ['已有设计参考','现有橙卡通过改变护甲清除、消耗去向、费用与出牌安排建立构筑。本轮橙卡分别改变目标、手牌循环与队伍资源用途。'],
 ['来源','依据v0.93总览、此前确认的游戏规则及本轮角色重设计提案。旧橙卡碎山、不息攻势、先祖战歌已撤回。']
 ],[23,100]);
hero.getRange('D4:F4').values=[['局内等级','最大HP','SPD']];hero.getRange('D4:F24').format.columnWidth=13;hero.getRange('D4:F24').format.font={name:'Arial',size:11};hero.getRange('D4:F4').format.fill='#E3DDE7';
for(let i=1;i<=20;i++){const r=i+4;hero.getRange(`D${r}`).values=[[i]];hero.getRange(`E${r}:F${r}`).formulas=[[`=$B$7+(D${r}-1)*$B$8`,'=$B$9']];}
hero.getRange('B7:B9').format.fill='#FFF0CC';
tab('卡牌',['卡名','费用','类型','稀有度','卡面描述','每级提升','升级上限','每级经验','设计ID'],d.cards.map(c=>[c.name,c.cost,c.type,c.rarity,c.description,c.upgrade,c.maxUpgrades,c.xp,c.id]),[20,7,9,9,80,22,12,12,29]);
tab('天赋',['局外解锁等级','槽位','天赋名称','效果'],d.talents.map(t=>[t.level,t.slot,t.name,t.description]),[16,9,20,105]);
const mechanics=[
 ['承压生怒','敌方攻击实际扣HP才触发。同一敌牌的多段与AOE只计一次，基础获取每回合最多2点。狂怒总量最多4。','虚化实际扣1HP可触发；0伤害或治疗不能触发。不改变本轮SPD排程。'],
 ['整牌强化','实际执行时一次性消耗狂怒，覆盖整张牌所有目标与段数。','未写狂怒效果的牌不自动增伤。不能提前使用稍后可能受击得到的狂怒。'],
 ['不死不休：死斗','双方存活时，裂斧单体攻击可跨排，但只能选死斗目标。敌方能合法命中她的单体攻击优先选她。','不改变AOE、不追加应对、不覆盖Boss明确点名、不强制非法目标。死斗结束立即清除双方标记并恢复目标规则。'],
 ['不死不休：回收','任意队友击败死斗目标都可满足条件。战斗继续且裂斧存活时，下回合正常抽牌后，从真实弃牌堆选择最多3张自己的攻击牌。','按剩余手牌空位取回，不复制、不从消耗堆取牌、不跨战斗兑现。零费只在手中有效，打出或离手恢复。单体最终Boss没有战后回收收益。'],
 ['狂战之魂','裂斧非消耗攻击获得继承，建立固定连招手牌；代价是不能使用自己的防御牌。','不影响队友、遗物的保护。原消耗攻击照常消耗；同一实例同轮不重复执行。使用后临时减费照常恢复。受死亡、禁牌与手牌上限约束。'],
 ['燃尽战旗：构筑','从下回合起不再自然流失，狂怒改为队伍费用抵扣资源，每点抵1费，每牌最多抵2费。','原卡牌的狂怒强化停止，基础效果仍执行。狂怒不同时用于供能和强化。裂斧死亡清零，不保留供能。'],
 ['燃尽战旗：支付','选牌草稿显式开启狂怒抵扣，默认关闭。确认计划时按顺序预留并支付现有资源。','不追溯本轮已确认费用。X费不参与，0费不退款。不使用预期未来资源。取消草稿只释放预留，不生成新资源。'],
 ['燃尽战旗：天赋','费用抵扣算作通过该牌消耗狂怒，可以触发有次数限制的天赋，但不触发该牌自身的狂怒强化。','仅依赖狂怒且无基础效果的牌需说明不可用原因；费用支付不得挤占强制效果必需的资源后仍允许确认。'],
 ['淬怒','每回合首次通过卡牌把正数狂怒耗至0后，下回合正常抽牌后选择自己的弃牌堆一张防御牌加入手牌。','手满不加入。不复制、不免费支付，不回收消耗堆。与狂战之魂的自身防御禁用存在构筑冲突。'],
 ['撕开战线','伤害后目标仍存活、可换位且身后紧邻格有敌人时，交换双方位置。','无合法对象时只结算伤害，不凭空移动。'],
 ['卡牌预览','分别显示基础效果与当前狂怒强化。未来受击所得只能标为条件，不显示为保证伤害。','营地HP/SPD下方明确列固有能力，首次出战提供一次可关闭说明。'],
 ['规则兼容','AOE仅第一张合法玩家应对牌可应对。虚化每次伤害最多1，背水一战防住伤害致死。','灼烧先消耗护甲，中毒无视护甲。不同持续时间分别计算。临时费用打出或离手恢复。'],
 ['验证范围','当前只完成设计数据完整性检查。','旧五角色模拟不验证本角色。仍需验证狂怒结算、继承循环、共享资源预留、死亡清理、回收与手牌上限。']
];
tab('机制细则',['机制','规则与玩法','限制与交互'],mechanics,[26,80,88]);
tab('图标与界面',['标记或界面','视觉方案','显示与操作','制作状态'],[
 ...d.icons.map(i=>[i.name,i.visual,i.display,i.fileStatus.replace('已提供','本地已保存')]),
 ['狂怒反馈','拳头图标旁四格槽及0/4数字，不能仅依赖红色','区分获取、消耗、自然流失；悬停展示基础获取额度与下一次流失。','界面方案，未接入Unity'],
 ['选人与角色详情','与原角色同一布局和粗像素规格','详情列明承压生怒，选人写承压积怒、反攻。天赋仍为两个独立槽位。','界面方案'],
 ['卡牌与橙卡状态','沿用原卡框和稀有度，关键词可查看完整说明','继承与防御禁用必须同时可见，燃尽战旗需明确生效回合、预留量和可用量。','界面方案'],
 ['五姿态素材','idle、attack、hit、defend、defeat五张独立图','受击改为双脚着地扭身护肋、斧头垂后；保留其余四张和已确认造型。','图片仅本地保存，本Excel不附图；尚未Unity验收']
],[23,64,78,47]);
wb.recalculate();
if(d.cards.length!==32||d.talents.length!==11||new Set(d.cards.map(c=>c.id)).size!==32)throw Error('设计记录不完整');
console.log((await wb.inspect({kind:'region',sheetId:'角色与成长',range:'D23:F24',maxChars:1000})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!',options:{useRegex:true,maxResults:20},summary:'formula errors'})).ndjson);
for(const [sheet,range] of [['角色与成长','A2:F13'],['卡牌','A33:H36'],['天赋','A4:D9'],['机制细则','A4:C8'],['图标与界面','A4:D8']]){const p=await wb.render({sheetName:sheet,range,scale:1,format:'png'});await fs.writeFile(`${dir}/preview-${sheet}.png`,new Uint8Array(await p.arrayBuffer()));}
const output=`${dir}/Grimhand_狂战士设计_第二轮修订.xlsx`;
await(await SpreadsheetFile.exportXlsx(wb)).save(output);console.log(output);
