import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
import {character,cards,talents,risks,rules} from './design.mjs';
const out='outputs/grimhand-v094-design';
const source='C:/Users/Kelthuzad/Desktop/扭曲虚空/The Grimhands Asset/Grimhand实际内容总览表v0.93.xlsx';
const raw=JSON.parse(await fs.readFile('Tools/Balance20261017/baseline-results.json','utf8'));
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(source));
console.log('Imported original workbook');
const original=[];
for(let i=0;i<14;i++){const s=wb.worksheets.getItemAt(i);original.push({name:s.name,values:s.getUsedRange().values});}
const col=n=>{let s='';while(n){n--;s=String.fromCharCode(65+n%26)+s;n=Math.floor(n/26);}return s;};
const ownerNames={char_knight:'战士',char_mage:'法老',char_ranger:'恶魔',char_snake_queen:'毒蛇女王',char_lich_queen:'巫妖女王'};
function sheet(name,headers,rows,widths=[]){
 console.log('Writing '+name+' '+rows.length+' rows');
 const s=wb.worksheets.add(name);s.showGridLines=false;
 s.getRange('A2').values=[[name]];s.getRange('A2').format.font={name:'Arial',size:14,bold:true};
 const all=[headers,...rows];const r=s.getRange(`A4:${col(headers.length)}${all.length+3}`);r.values=all;
 r.format.font={name:'Arial',size:10};r.format.verticalAlignment='center';r.format.rowHeight=26;
 s.getRange(`A4:${col(headers.length)}4`).format={fill:'#30343B',font:{name:'Arial',size:10,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',rowHeight:28};
 widths.forEach((w,i)=>s.getRange(`${col(i+1)}4:${col(i+1)}${all.length+3}`).format.columnWidth=w);
 if(rows.length>18)s.freezePanes.freezeRows(4);
 return s;
}
const groups=[];
for(const floor of [1,10,25,45])for(const elite of [false,true]){
 const rr=raw.rows.filter(r=>r.floor===floor && r.elite===elite);if(!rr.length)continue;
 groups.push([floor,elite?'精英':'普通',rr.length,rr.filter(r=>r.outcome==='win').length,rr.filter(r=>r.deaths>0).length,null,null,
 floor===1?'初始牌组独立满血战':'等级1高层压力测试，不是正常成长难度']);
}
const summary=sheet('评估摘要',['楼层','遭遇类型','场数','胜场','有人倒下场数','独立战胜率','有人倒下比例','测试场景'],groups,[9,12,10,10,18,16,18,48]);
for(let i=0;i<groups.length;i++){
 const r=i+5;summary.getRange(`F${r}:G${r}`).formulas=[[`=D${r}/C${r}`,`=E${r}/C${r}`]];
}
summary.getRange(`F5:G${groups.length+4}`).setNumberFormat('0.0%');
summary.getRange('A15').values=[['范围：五角色10种三人组合；两策略；六种子轮换站位；排除教程；没有完整远征或Boss测试。']];
summary.getRange('A17').values=[['首层普通战与精英战分开看；高层结果不能据此认定后期过难。']];
summary.getRange('A19').values=[['全部新数值为提案。未修改运行时代码，也未宣称平衡已完成。']];
const chart=summary.charts.add('bar',[summary.getRange('B4:B6'),summary.getRange('F4:F6'),summary.getRange('G4:G6')]);
chart.title='首层独立战：胜率与倒下比例';chart.setPosition('J4','R17');
chart.xAxis={axisType:'textAxis',textStyle:{typeface:'Arial',fontSize:11}};
chart.yAxis={numberFormatCode:'0%',numberFormatSourceLinked:false,textStyle:{typeface:'Arial',fontSize:10}};
const proposal=sheet('平衡调整提案',['优先级','问题','当前机制与证据','建议（待审批）','来源','证据边界'],risks,[10,31,62,70,60,52]);
proposal.getRange(`C5:F${risks.length+4}`).format.wrapText=true;proposal.getRange(`A5:F${risks.length+4}`).format.rowHeight=86;
const newRows=[['角色',character.name],['定义ID',character.id],['定位',character.pitch],['初始HP',character.hp],['每级HP',character.hpPerLevel],['SPD',character.speed],['20级HP',null],['初始牌组','裂斧斩×3；铁臂架挡×2；反击重劈×1；压住怒火×1'],['狂怒上限',4],['受击获取','敌方攻击牌造成HP损失后+1；同一牌只触发一次；此来源每回合最多+2'],['狂怒流失','第2回合起回合开始流失1；战斗结束清零'],['排除来源','毒、烧、反伤、献祭、自伤、友伤、完全被护甲吸收'],['消耗时机','实际结算前一次性消耗；AOE与多段共享；最多消耗读取当时数量'],['状态与实现',character.status],['天赋规则','每个槽位只能选一个已解锁节点；不是全部节点同时生效']];
const hero=sheet('狂战士设计',['项目','内容'],newRows,[25,105]);
hero.getRange('B11').formulas=[['=B8+19*B9']];
hero.getRange('A23:B23').values=[['解锁等级 / 槽位 / 节点','效果（均为提案）']];
hero.getRange(`A24:B${23+talents.length}`).values=talents.map(t=>[`${t.level}级 / 槽${t.slot} / ${t.name}`,t.effect]);
hero.getRange(`B24:B${23+talents.length}`).format.wrapText=true;
hero.getRange(`A24:B${23+talents.length}`).format.rowHeight=42;
hero.getRange('D5:F5').values=[['局内等级','HP','SPD']];
hero.getRange('D5:F25').format.columnWidth=12;
for(let i=1;i<=20;i++){
 const r=i+5;hero.getRange(`D${r}`).values=[[i]];
 hero.getRange(`E${r}:F${r}`).formulas=[[`=$B$8+(D${r}-1)*$B$9`,'=$B$10']];
}
sheet('已确认规则',['范围','当前标准'],rules,[24,110]);
const sCards=wb.worksheets.getItem('卡牌');
wb.worksheets.getItem('怪物组合').getRange('D3').values=[['缩放：伤害每5层+1，护甲每5层+1，HP每层+1.5（第1层为起点，按已确认规则）']];
wb.worksheets.getItem('怪物组合').getRange('D3').format.wrapText=true;
wb.worksheets.getItem('怪物组合').getRange('D3').format.rowHeight=52;
const keywords=wb.worksheets.getItem('说明（经验值和关键词等）');
keywords.getRange('N8:O8').values=[['【应对状态：已移除】','不再存在应对状态机制。剑柄猛击、蛇信感知按当前普通出牌效果处理。']];
keywords.getRange('O16').values=[['回合开始时每层造成2伤害，先消耗护甲；不与中毒的无视护甲规则混同。']];
keywords.getRange('O17').values=[['每层减少1点SPD。结算中施加时不改变本轮既定排程，影响下一轮。']];
keywords.getRange('O18').values=[['每层增加1点SPD。结算中施加时不改变本轮既定排程，影响下一轮。']];
keywords.getRange('O20').values=[['每次伤害最多1点，不是整回合总共1点；巫妖明确天赋可替换为0伤害并治疗。']];
for(const r of [8,16,17,18,20]){keywords.getRange(`O${r}`).format.wrapText=true;keywords.getRange(`N${r}:O${r}`).format.rowHeight=42;}
const oldCards=sCards.getRange('A1:L151').values;let synced=0;
for(let i=1;i<oldCards.length;i++){
 const matches=raw.catalog.filter(t=>t.name===oldCards[i][1] && ownerNames[t.owner]===oldCards[i][0]);
 if(matches.length===1 && matches[0].description!=='未找到静态文案'){
   sCards.getRange(`F${i+1}`).values=[[matches[0].description]];synced++;
 }
}
sCards.getRange('K2').values=[['调试专用，不进入正式奖励池；旧表中加入初始牌组的说明已过时。']];
const start=152;
for(let i=0;i<cards.length;i++){
 const c=cards[i],row=start+i;
 sCards.getRange(`A${row}:L${row}`).copyFrom(sCards.getRange('A3:L3'),'all');
 sCards.getRange(`A${row}:L${row}`).values=[['裂斧（提案）',c.name,c.cost,c.type,'职业',c.description,c.rarity,c.maxUpgrades||'-',c.upgrade,c.xp?`${c.xp}/级`:'-',c.id,'未实装']];
 sCards.getRange(`F${row}`).format.wrapText=true;sCards.getRange(`A${row}:L${row}`).format.rowHeight=42;
}
const snapshots=sheet('当前卡牌快照',['定义ID','卡名','归属ID','费用','类型','当前静态文案','关键词'],raw.catalog.map(c=>[c.id,c.name,c.owner,c.cost,c.type,c.description,c.keywords]),[32,30,28,8,12,95,30]);
snapshots.getRange(`F5:F${raw.catalog.length+4}`).format.wrapText=true;snapshots.getRange(`A5:G${raw.catalog.length+4}`).format.rowHeight=42;
const encounterGroups=Map.groupBy(raw.rows,r=>r.floor+'|'+r.encounter);
const encounterRows=[...encounterGroups.values()].map(g=>[g[0].floor,g[0].encounter,g[0].elite,g.length,g.filter(x=>x.outcome==='win').length,g.filter(x=>x.deaths>0).length,g.reduce((s,x)=>s+x.turns,0)/g.length,g.reduce((s,x)=>s+x.remainingHp,0)/g.length]);
const simulation=sheet('模拟遭遇汇总',['楼层','组合ID','精英','场数','胜场','有人倒下场数','平均回合','平均剩余总HP'],encounterRows,[8,44,8,10,10,18,14,20]);
simulation.getRange(`G5:H${encounterRows.length+4}`).setNumberFormat('0.0');
let art=[];try{art=JSON.parse(await fs.readFile(`${out}/art-manifest.json`,'utf8'));}catch{}
sheet('素材与验收',['资源','文件或规格','状态','验收重点'],[
 ...art.map(a=>[a.kind,a.file,a.status,a.review]),
 ['静态姿态','Idle、Attack、Defense、Hit、Death，透明PNG','以包内实际文件为准','同一像素密度、头身比、斧头与服装一致；不能有裁切'],
 ['CardProfile','半身透明卡像；复用现有卡框','以实际文件为准','与角色脸、服饰和卡牌裁切一致'],
 ['待机动画','局部胸廓双关键帧；IdleAnimationFrames可重复停留','已有原型，待Unity验收','非全身平移；只有两张不同图，不冒充完整细腻动画'],
 ['狂怒图标','128×128候选；0—4由UI显示','已制作，待接入','与现有关键词图标实际显示尺寸核对'],
 ['音效','复用现有近战斩击、防御、受击；新配音非必要','复用方案，尚未游戏内验收','音量和节奏一致，无未经授权的新音源'],
 ['运行时接入','内容、进度、存档、预览、描述、战斗事件和立绘目录','未接入','批准后实施并新增边界测试'],
 ['原始工作簿','原14页保留；卡牌文案同步'+synced+'行并追加32张提案','原文件未覆盖','不把提案写成已测试内容']
],[22,86,39,70]);
await fs.mkdir(out,{recursive:true});
wb.recalculate();
console.log((await wb.inspect({kind:'region',sheetId:'评估摘要',range:'A4:H12',maxChars:3000,tableMaxCols:8})).ndjson);
for(const [name,range] of [['评估摘要','A2:R19'],['狂战士设计','A2:F34'],['平衡调整提案','A2:D8'],['卡牌','A152:J155'],['已确认规则','A2:B9'],['素材与验收','A2:D24'],['当前卡牌快照','A2:F8'],['模拟遭遇汇总','A2:H8'],['怪物组合','A3:D4'],['说明（经验值和关键词等）','N8:O20']]){
 const img=await wb.render({sheetName:name,range,scale:1,format:'png'});
 await fs.writeFile(`Tools/Balance20261017/${name}-preview.png`,new Uint8Array(await img.arrayBuffer()));
}
await (await SpreadsheetFile.exportXlsx(wb)).save(`${out}/Grimhand实际内容总览表_v0.94_设计评审版.xlsx`);
await fs.writeFile(`${out}/new-character.json`,JSON.stringify({character,cards,talents,rules},null,2));
const keys=Object.keys(raw.rows[0]);const csvEscape=v=>'"'+String(v??'').replaceAll('"','""')+'"';
await fs.writeFile(`${out}/battle-samples.csv`,'\ufeff'+[keys,...raw.rows.map(r=>keys.map(k=>r[k]))].map(r=>r.map(csvEscape).join(',')).join('\r\n'));
await fs.writeFile('Tools/Balance20261017/source-cell-snapshot.json',JSON.stringify(original));
console.log(JSON.stringify({rows:raw.rows.length,cardSnapshots:raw.catalog.length,newCards:cards.length,syncedDescriptions:synced}));
