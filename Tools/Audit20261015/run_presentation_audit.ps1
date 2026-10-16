$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    $auditRefs = @(Get-ChildItem "$PSHOME/ref" -Filter '*.dll' | ForEach-Object FullName)
    $auditSources = @(Get-ChildItem Assets/_Project/Scripts/Core,Assets/_Project/Scripts/Battle,Assets/_Project/Scripts/Expedition -Recurse -Filter '*.cs' | ForEach-Object FullName)
    foreach ($auditName in @('CardDefinitionSO','EffectActionDefinition','CardDescriptionCatalog','CardVisualCatalogSO','BattleActionEffectCatalogSO','BossCharacterRules')) {
        $auditSources += (Resolve-Path "Assets/_Project/Scripts/Content/$auditName.cs").Path
    }
    foreach ($auditName in @('BattleUiFormatters','CardVisualResolver','CardFaceNumberFormatter','FootStatusIconAggregator','PresentationSnapshot','MinionTraitDisplayFormatter','BattleEventPlayback','BattleActionEffectResolver','CharacterTraitDisplayCatalog')) {
        $auditSources += (Resolve-Path "Assets/_Project/Scripts/Presentation/Battle/$auditName.cs").Path
    }
    $auditNunit = (Get-ChildItem Library/PackageCache -Recurse -Filter nunit.framework.dll | Select-Object -First 1).FullName
    Add-Type -Path $auditNunit
    $auditRefs += $auditNunit
    $auditSources += @('CatalogSmoke.cs','PresentationAuditStubs.cs','PresentationAudit.cs','CatalogContractAudit.cs','StandaloneTests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
    $auditSources += (Resolve-Path Assets/_Project/Tests/Battle/CardPresentationConsistencyTests.cs).Path
    Add-Type -Path $auditSources -ReferencedAssemblies $auditRefs
    [PresentationAudit]::Run()
    [StandaloneBattleTests]::Run()
} finally { Pop-Location }
