import fs from 'node:fs/promises';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const dir='outputs/grimhand-text-review-20261019';
const d=JSON.parse(await fs.readFile(`${dir}/current-text.json`,'utf8'));
if(d.cards.length!==32||d.talents.length!==8)throw Error('Runtime export is not current');
const wb=Workbook.create();const col=n=>String.fromCharCode(65+n);
function sheet(name,heads,rows,widths){
 const s=wb.worksheets.add(name);s.showGridLines=false;
 s.getRange('A2').values=[[name]];s.getRange('A2').format.font={name:'Arial',size:15,bold:true};
 s.getRange(`A4:${col(heads.length-1)}${rows.length+4}`).values=[heads,...rows];
 const area=s.getRange(`A4:${col(heads.length-1)}${rows.length+4}`);
 area.format.font={name:'Arial',size:11,color:'#222222'};area.format.wrapText=true;area.format.verticalAlignment='center';
 s.getRange(`A4:${col(heads.length-1)}4`).format={fill:'#38303F',font:{name:'Arial',size:11,color:'#FFFFFF',bold:true},rowHeight:30,horizontalAlignment:'center'};
 widths.forEach((w,i)=>s.getRange(`${col(i)}4:${col(i)}${rows.length+4}`).format.columnWidth=w);
 rows.forEach((row,i)=>{
  const lines=Math.max(...row.map((v,j)=>String(v??'').split('\n').reduce((sum,t)=>sum+Math.max(1,Math.ceil([...t].reduce((n,c)=>n+(c.charCodeAt(0)>255?2:1),0)/(widths[j]-3))),0)));
  const r=s.getRange(`A${i+5}:${col(heads.length-1)}${i+5}`);r.format.rowHeight=Math.max(36,lines*19+14);if(i%2)r.format.fill='#F3F1F5';
 });
 s.getRange(`${col(heads.length-1)}5:${col(heads.length-1)}${rows.length+4}`).format.fill='#FFF2CC';
 s.freezePanes.freezeRows(4);return s;
}
sheet('角色与解锁',['条目','当前内容','修改意见'],[
 ['角色名称','裂斧',''],['档案馆标题',d.loreTitle,''],['档案馆故事',d.loreBody,''],
 ['解锁提示',d.unlock,''],['基础属性','HP 76，SPD 5；每级HP增加6，等级20时HP为190。',''],
 ['初始牌组','裂斧斩×3、铁臂架挡×2、反击重劈×1、压住怒火×1。',''],
 ['解锁规则','正式远征击败第20层首领后永久解锁。训练场、教程和跳层首领测试不计入。存档与Steam云存档共同保存解锁状态。',''],
 ['解锁前','档案馆只显示黑色剪影、问号及解锁条件，不开放故事。不能在军营选择裂斧，天赋祭坛没有第二页，局外卡包不会抽到裂斧卡牌。',''],
 ['天赋调整','每栏四项。保留余烬、钢铁怒意、踏阵、压住杀意、乘胜、凿甲、并肩、留有后招。删除血性、怒而不竭、收势护身。旧档已装备的删除项会卸下。',''],
 ['本次卡牌重设计','立稳脚步：防守后换至中排。横斧破阵：消耗1狂怒净化虚弱和易伤。破甲重劈：拆除一半护甲并将拆甲转为伤害。忍痛架挡：耗尽狂怒复制已有护甲。旋斧风暴：敌人越少越强，消耗任意数量狂怒统一增伤30%。',''],
 ['旋斧风暴人数规则','以敌方3人为基准，按结算时的存活人数计算。剩3人基础12，剩2人基础20，剩1人基础28；消耗至少1狂怒时整次伤害增加30%，沿用现有伤害取整规则，不按狂怒点数叠加。战旗生效后只抵扣费用，不触发该强化。',''],
 ['状态显示','燃尽战旗（包括下回合生效的等待期）、专注怒意、淬怒、留力再战、狂战之魂和死斗均有状态图标及人物悬停说明。战旗自动抵扣费用，不再显示开关。',''],
 ['战斗显示规范','六名玩家角色共用脚底基线、站立可见身高及名字/血量水平线，不按透明画布边缘或静态图点击框推算。GIF和静态动作分别使用对应像素比例，攻击和倒地保留动作幅度。',''],
 ['审阅方法','在黄色的修改意见栏写替换文字或说明。卡面数值会随实际效果变化并使用绿色或红色标记，不额外添加预计行。提示中的【动态内容】会替换成角色名称、数值或剩余次数。',''],
 ],[22,110,55]);
const upgradeSource=await fs.readFile('Assets/_Project/Scripts/Core/CardUpgradeCatalog.cs','utf8');
function upgrades(name){const escaped=name.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');const match=upgradeSource.match(new RegExp(`\\["${escaped}"\\]\\s*=\\s*new\\(\\)\\s*\\{([^}]+)`));if(!match)return '无升级项';const labels={MaxUpgrades:'最多升级次数',XpCostPerLevel:'每级经验',DamagePerLevel:'每级伤害增加',BlockPerLevel:'每级护甲增加',HealPerLevel:'每级治疗增加',VulnerablePerLevel:'每级易伤百分点增加',RespondMitigationPerLevel:'每级减伤百分点增加'};return match[1].split(',').map(x=>{const [k,v]=x.trim().split('=');return `${labels[k]??k} ${v?.trim()}`;}).join('；');}
sheet('卡牌文字',['卡牌ID','名称','费用','类型','稀有度','卡面原文','升级规则','修改意见'],d.cards.map(c=>[c.id,c.name,c.cost,({Attack:'攻击',Defense:'防御',Status:'状态'})[c.type],['白','绿','蓝','紫','橙'][c.rarity],c.description,upgrades(c.name),'']),[25,18,8,9,10,78,40,50]);
sheet('天赋文字',['天赋ID','栏位','解锁等级','名称','完整说明','修改意见'],d.talents.map(t=>[t.id,t.slot,t.level,t.name,t.description,'']),[30,8,11,18,105,50]);
const ui=d.keywords.map(k=>['关键词',k.id,k.name+'：'+k.description,'']);
const covered=new Set([...d.cards.flatMap(c=>[c.name,c.description]),...d.talents.flatMap(t=>[t.name,t.description]),...d.keywords.flatMap(k=>[k.name,k.description]),d.loreTitle,d.loreBody]);
const files=execFileSync('rg',['--files','Assets/_Project/Scripts'],{encoding:'utf8'}).trim().split(/\r?\n/);
const strings=[];
function extract(source,file){
 const pattern=/\$?"/g;let m;
 while((m=pattern.exec(source))){
  const interpolated=m[0].startsWith('$');let end=pattern.lastIndex,depth=0,inner=false;
  for(;end<source.length;end++){
   const c=source[end];if(c==='\\'){end++;continue;}
   if(interpolated&&depth>0){if(c==='"')inner=!inner;if(!inner){if(c==='{')depth++;if(c==='}')depth--;}continue;}
   if(c==='"')break;if(interpolated&&c==='{')depth++;
  }
  pattern.lastIndex=end+1;
  let value=source.slice(m.index+(interpolated?2:1),end).replace(/\\n/g,'\n').replace(/\\"/g,'"');
  if(value.includes('[0-9]')||value.includes('<color='))continue;
  if(interpolated){for(let i=0;i<8&&/\{[^{}]*\}/.test(value);i++){const next=value.replace(/\{[^{}]*\}/g,'【动态内容】');if(next===value)break;value=next;}}
  if(!/[\u3400-\u9fff]/.test(value)||covered.has(value)||value.length<2)continue;
  if(strings.some(r=>r[2]===value))continue;
  const locations={BerserkerBattlePanel:'战旗与弃牌回收窗口',BerserkerRules:'狂怒与卡牌效果提示',BerserkerSoulRules:'再战操作提示',BattleUiFormatters:'战斗状态与卡面提示',MinionTraitDisplayFormatter:'人物脚下状态',GameFlowController:'营地与出征提示',PsionicScryOverlayView:'灵能预知窗口',FelskullBattleChoiceView:'魔焰颅骨窗口',CardView:'卡牌标记'};
  strings.push([locations[path.basename(file,'.cs')]??(/Editor/.test(file)?'编辑器提示':'其他新增提示'),path.basename(file),value,'']);
 }
}
for(const file of files.filter(f=>/Berserker|PsionicScryOverlayView|FelskullBattleChoiceView/.test(f)&&!f.includes('Catalog'))){extract(await fs.readFile(file,'utf8'),file);}
const diff=execFileSync('git',['diff','--unified=0','--','Assets/_Project/Scripts'],{encoding:'utf8',maxBuffer:16*1024*1024,stdio:['ignore','pipe','ignore']});
const additions=new Map();let current='';
for(const line of diff.split(/\r?\n/)){
 if(line.startsWith('+++ b/'))current=line.slice(6);
 else if(current.endsWith('.cs')&&line.startsWith('+'))additions.set(current,(additions.get(current)??'')+line.slice(1)+'\n');
}
for(const [file,source] of additions)if(!/TalentCatalog|CharacterLoreCatalog/.test(file))extract(source,file);
strings.sort((a,b)=>a[1].localeCompare(b[1]));
sheet('界面与提示',['出现位置','条目','当前文字或动态模板','修改意见'],[...ui,...strings],[22,37,110,55]);
wb.recalculate();
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!',options:{useRegex:true,maxResults:20},summary:'error scan'})).ndjson);
for(const [name,range] of [['角色与解锁','A2:C10'],['卡牌文字','A4:H12'],['天赋文字','A4:F12'],['界面与提示','A4:D12']]){
 const png=await wb.render({sheetName:name,range,scale:1,format:'png'});await fs.writeFile(`${dir}/preview-${name}.png`,new Uint8Array(await png.arrayBuffer()));
}
const output=`${dir}/Grimhand_裂斧完整文案与机制修订.xlsx`;
await(await SpreadsheetFile.exportXlsx(wb)).save(output);
console.log(JSON.stringify({output,cards:d.cards.length,talents:d.talents.length,otherTexts:ui.length+strings.length}));
