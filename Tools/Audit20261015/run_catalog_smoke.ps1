param([string]$SourceRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Push-Location $SourceRoot
try {
    $auditRefs = @(Get-ChildItem "$PSHOME/ref" -Filter '*.dll' | ForEach-Object FullName)
    $auditSources = @(Get-ChildItem Assets/_Project/Scripts/Core,Assets/_Project/Scripts/Battle -Recurse -Filter '*.cs' | ForEach-Object FullName) + @((Join-Path $PSScriptRoot 'CatalogSmoke.cs'))
    Add-Type -Path $auditSources -ReferencedAssemblies $auditRefs
    [CatalogSmoke]::Run()
    [CatalogSmoke]::RunEnginePaths()
    [CatalogSmoke]::RunBattles()
} finally {
    Pop-Location
}
