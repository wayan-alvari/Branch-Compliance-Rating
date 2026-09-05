# Dot-source this script from the repository root if using a repository-local SDK.
$branchRepoRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $branchRepoRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $branchRepoRoot '.nuget/packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $branchRepoRoot '.nuget/http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$branchToolTemp = Join-Path $branchRepoRoot '.local/tmp'
New-Item -ItemType Directory -Force -Path $branchToolTemp | Out-Null
$env:TEMP = $branchToolTemp
$env:TMP = $branchToolTemp
$branchLocalSdk = Join-Path $branchRepoRoot '.local/dotnet'
if (Test-Path -LiteralPath (Join-Path $branchLocalSdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $branchLocalSdk
    $env:PATH = "$branchLocalSdk;$env:PATH"
}
