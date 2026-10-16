$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    $auditOutput = 'Tools/Audit20261015/compile'
    New-Item -ItemType Directory -Path $auditOutput -Force | Out-Null
    $auditUnityData = 'C:/Program Files/Unity/Hub/Editor/6000.4.5f1/Editor/Data'
    $auditNames = @('Grimhand.Core','Grimhand.Battle','Grimhand.Expedition','Grimhand.Content','Grimhand.Persistence','Grimhand.Steam','Grimhand.Presentation','Grimhand.Battle.Tests')
    foreach ($auditName in $auditNames) {
        $auditResponse = "Library/Bee/artifacts/1900b0aE.dag/$auditName.rsp"
        $auditText = Get-Content $auditResponse -Raw
        foreach ($auditDependency in $auditNames) {
            $auditText = $auditText.Replace("Library/Bee/artifacts/1900b0aE.dag/$auditDependency.dll", "$auditOutput/$auditDependency.dll")
            $auditText = $auditText.Replace("Library/Bee/artifacts/1900b0aE.dag/$auditDependency.ref.dll", "$auditOutput/$auditDependency.ref.dll")
        }
        if ($auditName -eq 'Grimhand.Battle.Tests') {
            $auditText += "`n`"Assets/_Project/Tests/Battle/ConfirmedRulesRegressionTests.cs`"`n"
            $auditText += "`n`"Assets/_Project/Tests/Battle/CardPresentationConsistencyTests.cs`"`n"
        }
        Set-Content -LiteralPath "$auditOutput/$auditName.rsp" -Value $auditText -Encoding utf8
        & "$auditUnityData/NetCoreRuntime/dotnet.exe" "$auditUnityData/DotNetSdkRoslyn/csc.dll" "@$auditOutput/$auditName.rsp"
        if ($LASTEXITCODE -ne 0) { throw "Unity-compatible compilation failed: $auditName" }
        Write-Output "Compiled $auditName"
    }
} finally {
    Pop-Location
}
