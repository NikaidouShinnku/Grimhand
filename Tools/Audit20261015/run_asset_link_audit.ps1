$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    Add-Type -Path (Join-Path $PSScriptRoot 'AssetLinkAudit.cs')
    [AssetLinkAudit]::Run()
} finally { Pop-Location }
