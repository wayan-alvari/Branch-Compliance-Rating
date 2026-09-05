param([switch]$Sqlite, [int]$Port = 5094)
$ErrorActionPreference = 'Stop'
$branchRepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $branchRepoRoot
. ./scripts/Use-LocalSdk.ps1
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DemoMode__Enabled = 'true'
$env:Database__Initialize = 'true'
if ($Sqlite) {
    New-Item -ItemType Directory -Force -Path (Join-Path $branchRepoRoot 'data') | Out-Null
    $env:Database__Provider = 'Sqlite'
    $branchDemoDatabase = Join-Path $branchRepoRoot 'data/branch-compliance.db'
    $env:ConnectionStrings__DemoSqlite = "Data Source=$branchDemoDatabase"
}
else {
    $env:Database__Provider = 'MySql'
}
dotnet run --project src/BranchCompliance.Web --no-launch-profile --urls "http://localhost:$Port"
if ($LASTEXITCODE -ne 0) { throw 'Demo startup failed. Check the local configuration and prerequisites.' }
