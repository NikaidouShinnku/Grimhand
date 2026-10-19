param([string]$SourceRoot=(Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference='Stop'
Push-Location $SourceRoot
try {
    $auditRefs=@(Get-ChildItem "$PSHOME/ref" -Filter '*.dll' | ForEach-Object FullName)
    $auditSources=@(Get-ChildItem Assets/_Project/Scripts/Core,Assets/_Project/Scripts/Battle,Assets/_Project/Scripts/Expedition -Recurse -Filter '*.cs' | ForEach-Object FullName)
    $auditSources+=Join-Path $PSScriptRoot '../Audit20261015/CatalogSmoke.cs'
    $auditSources+=Join-Path $PSScriptRoot 'BalanceAudit.cs'
    $auditSources+=Join-Path $SourceRoot 'Assets/_Project/Scripts/Content/CardDescriptionCatalog.cs'
    Add-Type -Path $auditSources -ReferencedAssemblies $auditRefs
    [BalanceAudit20261017]::Run() | Set-Content (Join-Path $PSScriptRoot 'baseline-results.json') -Encoding utf8
    Write-Output 'Saved baseline-results.json'
} finally { Pop-Location }
