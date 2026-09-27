#Requires -RunAsAdministrator
param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
try {
    Import-Module Hyper-V
    [pscustomobject]@{
        Ready = $true
        VMs = @(Get-VM | Select-Object Name,State,Generation)
        Switches = @(Get-VMSwitch | Select-Object Name,SwitchType)
        FreeMemoryMB = [math]::Floor((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1024)
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath
}
catch {
    @{ Ready = $false; Error = $_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath $OutputPath
    exit 1
}
