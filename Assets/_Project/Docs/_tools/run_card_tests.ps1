# Grimhand v0.9 卡牌行为测试 — 一键运行（需关闭 Unity Editor）
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$Py = Join-Path $PSScriptRoot "run_card_behavior_batch.py"

Write-Host "=== Grimhand 卡牌行为测试 (238张) ===" -ForegroundColor Cyan
Write-Host "项目: $Root"
Write-Host "请先关闭 Unity Editor，否则 batchmode 会失败。"
Write-Host ""

python $Py @args
exit $LASTEXITCODE
