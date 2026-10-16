$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    $auditSources = @(Get-ChildItem Assets/_Project/Scripts/Core,Assets/_Project/Scripts/Battle -Recurse -Filter '*.cs' | ForEach-Object FullName) + @((Resolve-Path Tools/Audit20261015/Probe.cs).Path)
    Add-Type -Path $auditSources
    [AuditProbe20261015]::Main()
} finally {
    Pop-Location
}
