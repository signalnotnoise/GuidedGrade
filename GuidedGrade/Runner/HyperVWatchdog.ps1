param([Parameter(Mandatory=$true)][Guid]$VmId, [Parameter(Mandatory=$true)][int]$OwnerPid)
$ErrorActionPreference = 'Stop'
Import-Module Hyper-V
$deadline = [DateTime]::UtcNow.AddMinutes(15)
do {
    Start-Sleep -Seconds 5
    $vm = Get-VM -Id $VmId -ErrorAction SilentlyContinue
    if ($null -eq $vm) { exit }
    $owner = Get-Process -Id $OwnerPid -ErrorAction SilentlyContinue
} while ($null -ne $owner -and [DateTime]::UtcNow -lt $deadline)
# The id came directly from New-VM; never search or delete by a user-supplied name.
if ($vm.Name -match '^GuidedGrade-[a-f0-9]{32}$') {
    Stop-VM -VM $vm -TurnOff -Force -ErrorAction SilentlyContinue
    Remove-VM -VM $vm -Force -ErrorAction SilentlyContinue
}
