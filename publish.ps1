$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
$project = Join-Path $root "D2RDropTracker\D2RDropTracker.csproj"
$publishRoot = Join-Path $root "publish"
$publishDirectory = Join-Path $publishRoot "D2RDropTracker-lite"
$zipPath = Join-Path $publishRoot "D2RDropTracker-lite.zip"

$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

if (Test-Path $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

& $dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishDirectory `
    -p:DebugType=None `
    -p:DebugSymbols=false

Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host ""
Write-Host "Lightweight package created: $sizeMb MB" -ForegroundColor Green
Write-Host $zipPath
