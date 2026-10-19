import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
const source='C:/Users/Kelthuzad/Desktop/扭曲虚空/The Grimhands Asset/Grimhand实际内容总览表v0.93.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(source));
console.log((await wb.inspect({kind:'workbook,sheet,table',maxChars:3500,tableMaxRows:2,tableMaxCols:4})).ndjson);
const image=await wb.render({sheetName:'卡牌',range:'A1:J8',scale:1,format:'png'});
await fs.writeFile('Tools/Balance20261017/source-preview.png',new Uint8Array(await image.arrayBuffer()));
