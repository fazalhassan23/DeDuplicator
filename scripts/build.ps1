param([switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $dotnet = if (Test-Path '.tools/dotnet/dotnet.exe') { Join-Path $projectRoot '.tools/dotnet/dotnet.exe' } else { 'dotnet' }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/home'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
    & $dotnet restore DeDuplicator.slnx --configfile NuGet.Config
    if ($LASTEXITCODE) { throw 'Restore failed.' }
    & $dotnet build DeDuplicator.slnx -c Release --no-restore -m:1
    if ($LASTEXITCODE) { throw 'Build failed.' }
    & $dotnet test tests/DeDuplicator.Tests/DeDuplicator.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=safety.trx' --results-directory artifacts/test-results
    if ($LASTEXITCODE) { throw 'Tests failed.' }
    if ($Package) {
        & $dotnet publish src/DeDuplicator.App/DeDuplicator.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/DeDuplicator-win-x64
        if ($LASTEXITCODE) { throw 'Publish failed.' }
        Copy-Item README.md artifacts/DeDuplicator-win-x64
        Copy-Item docs, profiles -Destination artifacts/DeDuplicator-win-x64 -Recurse -Force
        Compress-Archive -Path artifacts/DeDuplicator-win-x64/* -DestinationPath artifacts/DeDuplicator-win-x64.zip -Force
    }
} finally { Pop-Location }
