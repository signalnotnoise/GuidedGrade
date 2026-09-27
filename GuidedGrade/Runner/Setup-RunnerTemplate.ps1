#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$IsoPath,
    [string]$TemplateDirectory = 'C:\ProgramData\GuidedGrade\Template',
    [string]$VmName = 'GuidedGrade-Template',
    [string]$CredentialPath = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'GuidedGrade\runner-guest.xml')
)
$ErrorActionPreference = 'Stop'
Import-Module Hyper-V
$iso = (Resolve-Path -LiteralPath $IsoPath).Path
if ([IO.Path]::GetExtension($iso) -ne '.iso') { throw 'Supply a Windows installation ISO.' }
if ((Get-FileHash -LiteralPath $iso -Algorithm SHA256).Hash -ne 'A61ADEAB895EF5A4DB436E0A7011C92A2FF17BB0357F58B13BBC4062E535E7B9') {
    throw 'ISO hash does not match the selected Microsoft Windows 11 Enterprise 25H2 evaluation.'
}
if (Get-VM -Name $VmName -ErrorAction SilentlyContinue) {
    throw "A VM named '$VmName' already exists. It was not modified."
}
$directory = [IO.Path]::GetFullPath($TemplateDirectory)
$diskPath = Join-Path $directory 'Windows-Template.vhdx'
if (Test-Path -LiteralPath $diskPath) { throw "The template disk already exists: $diskPath. It was not modified." }
$switch = Get-VMSwitch -Name 'Default Switch' -ErrorAction SilentlyContinue
if ($null -eq $switch) { throw 'The Hyper-V Default Switch is unavailable. Configure a temporary setup network first.' }
$volume = Get-Volume -FilePath ([IO.Path]::GetPathRoot($directory))
if ($volume.SizeRemaining -lt 90GB) { throw 'At least 90 GB of host disk space is required for the template and installation.' }

New-Item -ItemType Directory -Path $directory -Force | Out-Null
$answerMedia = & (Join-Path $PSScriptRoot 'New-RunnerAnswerMedia.ps1') -PrivateDirectory (Join-Path $directory 'Private') -CredentialPath $CredentialPath
New-VHD -Path $diskPath -Dynamic -SizeBytes 80GB | Out-Null
$vm = New-VM -Name $VmName -Generation 2 -MemoryStartupBytes 4GB -VHDPath $diskPath -Path $directory -SwitchName $switch.Name
Set-VM -VM $vm -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction ShutDown
Set-VMProcessor -VM $vm -Count 2
Set-VMKeyProtector -VM $vm -NewLocalKeyProtector
Enable-VMTPM -VM $vm
Add-VMDvdDrive -VM $vm -Path $iso
$dvd = Get-VMDvdDrive -VM $vm
Set-VMFirmware -VM $vm -EnableSecureBoot On -FirstBootDevice $dvd
Add-VMDvdDrive -VM $vm -Path $answerMedia

$freeMemory = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory * 1KB
$started = $false
if ($freeMemory -ge 6GB) {
    Start-VM -VM $vm | Out-Null
    $started = $true
}
[pscustomobject]@{
    VMName = $vm.Name
    VMId = $vm.Id
    Disk = $diskPath
    CredentialPath = [IO.Path]::GetFullPath($CredentialPath)
    Started = $started
    NextStep = $(if ($started) { 'Install Windows, then provision build tools before sealing the template.' }
                 else { 'VM created but not started. Free at least 6 GB of RAM, then start it to install Windows.' })
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'setup-status.json')
Get-Content -LiteralPath (Join-Path $directory 'setup-status.json')
