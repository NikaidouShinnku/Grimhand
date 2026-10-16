param([string]$SourceRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$auditWorkspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $SourceRoot
try {
    $auditNunit = (Get-ChildItem (Join-Path $auditWorkspace 'Library/PackageCache') -Recurse -Filter nunit.framework.dll | Select-Object -First 1).FullName
    Add-Type -Path $auditNunit
    $auditRefs = @(Get-ChildItem "$PSHOME/ref" -Filter '*.dll' | ForEach-Object FullName) + @($auditNunit)
    $auditTests = @(Get-ChildItem Assets/_Project/Tests/Battle -Filter '*.cs' | Where-Object {
        (Get-Content $_.FullName -Raw) -notmatch 'UnityEngine|UnityEditor|Grimhand.Content|Grimhand.Persistence|Grimhand.Presentation|CardV09TestHarness|CardDescriptionCatalog'
    } | ForEach-Object FullName)
    $auditSources = @(Get-ChildItem Assets/_Project/Scripts/Core,Assets/_Project/Scripts/Battle,Assets/_Project/Scripts/Expedition -Recurse -Filter '*.cs' | ForEach-Object FullName) + $auditTests + @((Join-Path $PSScriptRoot 'StandaloneTests.cs'))
    Add-Type -Path $auditSources -ReferencedAssemblies $auditRefs
    [StandaloneBattleTests]::Run()
} finally {
    Pop-Location
}
