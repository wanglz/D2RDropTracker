$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

& (Join-Path $root ".dotnet\dotnet.exe") build `
    (Join-Path $root "D2RDropTracker\D2RDropTracker.csproj") `
    --configuration Release

Write-Host ""
Write-Host "构建完成。双击“启动D2R掉落统计器.bat”即可运行。" -ForegroundColor Green
