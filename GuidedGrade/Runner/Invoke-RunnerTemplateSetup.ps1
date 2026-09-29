#Requires -RunAsAdministrator
param([Parameter(Mandatory=$true)][string]$IsoPath,
      [Parameter(Mandatory=$true)][string]$StatusPath)
$ErrorActionPreference = 'Stop'
try {
    & (Join-Path $PSScriptRoot 'Setup-RunnerTemplate.ps1') -IsoPath $IsoPath |
        Set-Content -LiteralPath $StatusPath -Encoding UTF8
} catch {
    [pscustomobject]@{ Success = $false; Error = $_.Exception.Message; Location = $_.ScriptStackTrace } |
        ConvertTo-Json | Set-Content -LiteralPath $StatusPath -Encoding UTF8
    exit 1
}
