$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

& (Join-Path $root ".dotnet\dotnet.exe") run `
    --project (Join-Path $root "D2RDropTracker.Tests\D2RDropTracker.Tests.csproj") `
    --configuration Release
