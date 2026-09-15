# Run inside the dedicated guest, after Windows installation finishes.
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'LAB-RUNNER') { throw 'This script must run inside the dedicated LAB-RUNNER guest.' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$setupDirectory = 'C:\RunnerSetup'
New-Item -ItemType Directory -Path $setupDirectory -Force | Out-Null
$bootstrapper = Join-Path $setupDirectory 'vs_buildtools.exe'
Invoke-WebRequest 'https://aka.ms/vs/17/release/vs_buildtools.exe' -OutFile $bootstrapper -UseBasicParsing
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'Visual Studio installer signature verification failed.'
}
$installer = Start-Process -FilePath $bootstrapper -WindowStyle Hidden -ArgumentList '--quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended' -Wait -PassThru
if ($installer.ExitCode -notin @(0, 3010)) { throw "Build Tools installation failed: $($installer.ExitCode)" }
$dotnetScript = Join-Path $setupDirectory 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $dotnetScript -UseBasicParsing
& $dotnetScript -Channel '10.0' -InstallDir 'C:\BuildTools\dotnet' -NoPath
if (-not (Test-Path 'C:\BuildTools\dotnet\dotnet.exe')) { throw '.NET SDK installation failed.' }
$machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
if ('C:\BuildTools\dotnet' -notin ($machinePath -split ';')) {
    [Environment]::SetEnvironmentVariable('Path', "$machinePath;C:\BuildTools\dotnet", 'Machine')
}
[Environment]::SetEnvironmentVariable('DOTNET_ROOT', 'C:\BuildTools\dotnet', 'Machine')
$sdk = & 'C:\BuildTools\dotnet\dotnet.exe' --list-sdks
if (-not ($sdk -match '^10\.')) { throw 'The .NET 10 SDK was not detected.' }
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$cpp = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $cpp) { throw 'The C++ compiler workload was not detected.' }
[pscustomobject]@{ Sdk = $sdk; CppTools = $cpp; RebootRequired = ($installer.ExitCode -eq 3010) } |
    ConvertTo-Json | Set-Content (Join-Path $setupDirectory 'tools-status.json')
