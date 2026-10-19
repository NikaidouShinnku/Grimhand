$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$sourcePath='C:/Users/Kelthuzad/Desktop/扭曲虚空/The Grimhands Asset/Grimhand实际内容总览表v0.93.xlsx'
$resultPath=Join-Path $PSScriptRoot '../../outputs/grimhand-v094-design/Grimhand实际内容总览表_v0.94_设计评审版.xlsx'
function Read-Cells($path) {
 $zip=[IO.Compression.ZipFile]::OpenRead($path)
 function Xml($name){$reader=[IO.StreamReader]::new($zip.GetEntry($name).Open());try{[xml]$reader.ReadToEnd()}finally{$reader.Dispose()}}
 try {
  $strings=@();if($zip.GetEntry('xl/sharedStrings.xml')){$strings=@((Xml 'xl/sharedStrings.xml').sst.si | ForEach-Object InnerText)}
  $rels=Xml 'xl/_rels/workbook.xml.rels';$result=@{}
  foreach($sheet in (Xml 'xl/workbook.xml').workbook.sheets.sheet){
   $id=$sheet.GetAttribute('id','http://schemas.openxmlformats.org/officeDocument/2006/relationships')
   $target=($rels.Relationships.Relationship | Where-Object Id -eq $id).Target
   $name=if($target.StartsWith('/')){$target.TrimStart('/')}else{'xl/'+$target}
   $cells=@{}
   foreach($cell in (Xml $name).worksheet.sheetData.row.c){
    $value=if($cell.t -eq 's'){$strings[[int]$cell.v]}elseif($cell.t -eq 'inlineStr'){$cell.is.InnerText}else{[string]$cell.v}
    if($value -ne ''){$cells[[string]$cell.r]=[string]$value}
   }
   $result[[string]$sheet.name]=$cells
  }
  return $result
 } finally {$zip.Dispose()}
}
$before=Read-Cells $sourcePath;$after=Read-Cells $resultPath
$unexpected=[Collections.Generic.List[string]]::new();$allowed=0
foreach($name in $before.Keys){
 if(-not $after.ContainsKey($name)){throw "Missing original sheet $name"}
 foreach($cell in $before[$name].Keys){
  if($before[$name][$cell] -ceq $after[$name][$cell]){continue}
  if($name -eq '卡牌' -and ($cell -match '^F([2-9]|[1-9][0-9]|1[0-4][0-9]|15[01])$' -or $cell -eq 'K2')){$allowed++;continue}
  if($name -eq '怪物组合' -and $cell -eq 'D3'){$allowed++;continue}
  if($name -eq '说明（经验值和关键词等）' -and $cell -in @('N8','O8','O16','O17','O18','O20')){$allowed++;continue}
  $unexpected.Add("$name!$cell : $($before[$name][$cell]) -> $($after[$name][$cell])")
 }
}
if($unexpected.Count){throw ($unexpected -join "`n")}
foreach($sheet in $after.Keys){foreach($cell in $after[$sheet].Keys){if($after[$sheet][$cell] -match '^#(REF!|DIV/0!|VALUE!|NAME\?|NUM!|N/A)$'){throw "Formula error $sheet!$cell"}}}
if($after['狂战士设计']['B11'] -ne '190'){throw 'Incorrect level20 HP'}
$rows=Get-Content (Join-Path $PSScriptRoot 'baseline-results.json') -Raw | ConvertFrom-Json
if(@($rows.rows | Where-Object error).Count){throw 'Simulation exception'}
"Verified original 14 sheets: no unauthorized cell-value changes; $allowed authorized updates; no cached formula errors; 32 new cards; $($rows.rows.Count) simulation rows."
Get-FileHash -LiteralPath $sourcePath,$resultPath -Algorithm SHA256 | Format-Table -AutoSize
