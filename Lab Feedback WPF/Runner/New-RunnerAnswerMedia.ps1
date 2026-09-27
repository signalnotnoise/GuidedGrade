param([Parameter(Mandatory=$true)][string]$PrivateDirectory,
      [Parameter(Mandatory=$true)][string]$CredentialPath)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $CredentialPath) { throw 'Guest credentials already exist; refusing to replace them.' }
New-Item -ItemType Directory -Path $PrivateDirectory -Force | Out-Null
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$acl = New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
foreach ($principal in @($sid, [Security.Principal.SecurityIdentifier]'S-1-5-18')) {
    $rule = New-Object Security.AccessControl.FileSystemAccessRule($principal, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $PrivateDirectory -AclObject $acl
$random = New-Object byte[] 30
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($random)
$password = 'Lf!' + [Convert]::ToBase64String($random) + '9a'
$credential = New-Object Management.Automation.PSCredential('LAB-RUNNER\LabRunner', (ConvertTo-SecureString $password -AsPlainText -Force))
New-Item -ItemType Directory -Path (Split-Path -Parent $CredentialPath) -Force | Out-Null
$credential | Export-Clixml -LiteralPath $CredentialPath
$escaped = [Security.SecurityElement]::Escape($password)
$answerDirectory = Join-Path $PrivateDirectory 'Answer'
New-Item -ItemType Directory -Path $answerDirectory | Out-Null
$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
  <settings pass="windowsPE">
    <component name="Microsoft-Windows-International-Core-WinPE" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <SetupUILanguage><UILanguage>en-US</UILanguage></SetupUILanguage>
      <InputLocale>en-US</InputLocale><SystemLocale>en-US</SystemLocale><UILanguage>en-US</UILanguage><UserLocale>en-US</UserLocale>
    </component>
    <component name="Microsoft-Windows-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <UserData><AcceptEula>true</AcceptEula><FullName>Lab Feedback</FullName><Organization>Lab Feedback</Organization></UserData>
      <DiskConfiguration><Disk wcm:action="add"><DiskID>0</DiskID><WillWipeDisk>true</WillWipeDisk>
        <CreatePartitions>
          <CreatePartition wcm:action="add"><Order>1</Order><Type>EFI</Type><Size>260</Size></CreatePartition>
          <CreatePartition wcm:action="add"><Order>2</Order><Type>MSR</Type><Size>16</Size></CreatePartition>
          <CreatePartition wcm:action="add"><Order>3</Order><Type>Primary</Type><Extend>true</Extend></CreatePartition>
        </CreatePartitions>
        <ModifyPartitions>
          <ModifyPartition wcm:action="add"><Order>1</Order><PartitionID>1</PartitionID><Format>FAT32</Format><Label>System</Label></ModifyPartition>
          <ModifyPartition wcm:action="add"><Order>2</Order><PartitionID>3</PartitionID><Format>NTFS</Format><Label>Windows</Label><Letter>C</Letter></ModifyPartition>
        </ModifyPartitions>
      </Disk><WillShowUI>OnError</WillShowUI></DiskConfiguration>
      <ImageInstall><OSImage><InstallFrom><MetaData wcm:action="add"><Key>/IMAGE/INDEX</Key><Value>1</Value></MetaData></InstallFrom>
        <InstallTo><DiskID>0</DiskID><PartitionID>3</PartitionID></InstallTo><WillShowUI>OnError</WillShowUI>
      </OSImage></ImageInstall>
    </component>
  </settings>
  <settings pass="specialize">
    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"><ComputerName>LAB-RUNNER</ComputerName><TimeZone>Eastern Standard Time</TimeZone></component>
    <component name="Microsoft-Windows-Deployment" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <RunSynchronous><RunSynchronousCommand wcm:action="add"><Order>1</Order><Path>reg add HKLM\SYSTEM\CurrentControlSet\Control\BitLocker /v PreventDeviceEncryption /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand></RunSynchronous>
    </component>
  </settings>
  <settings pass="oobeSystem">
    <component name="Microsoft-Windows-International-Core" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"><InputLocale>en-US</InputLocale><SystemLocale>en-US</SystemLocale><UILanguage>en-US</UILanguage><UserLocale>en-US</UserLocale></component>
    <component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
      <OOBE><HideEULAPage>true</HideEULAPage><HideOnlineAccountScreens>true</HideOnlineAccountScreens><HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE><ProtectYourPC>3</ProtectYourPC></OOBE>
      <UserAccounts><LocalAccounts><LocalAccount wcm:action="add"><Name>LabRunner</Name><DisplayName>Lab Runner</DisplayName><Group>Administrators</Group><Password><Value>$escaped</Value><PlainText>true</PlainText></Password></LocalAccount></LocalAccounts></UserAccounts>
    </component>
  </settings>
</unattend>
"@
$xml | Set-Content -LiteralPath (Join-Path $answerDirectory 'Autounattend.xml') -Encoding UTF8
$image = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
$image.FileSystemsToCreate = 3
$image.VolumeName = 'LABANSWER'
$image.Root.AddTree($answerDirectory, $false)
$result = $image.CreateResultImage()
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class RunnerIsoWriter {
    public static void Save(object source, string path) {
        var stream = (IStream)source;
        var count = Marshal.AllocHGlobal(4);
        try {
            using (var output = File.Create(path)) {
                var buffer = new byte[65536];
                while (true) {
                    stream.Read(buffer, buffer.Length, count);
                    int read = Marshal.ReadInt32(count);
                    if (read == 0) break;
                    output.Write(buffer, 0, read);
                }
            }
        } finally { Marshal.FreeHGlobal(count); }
    }
}
'@
$media = Join-Path $PrivateDirectory 'Answer.iso'
[RunnerIsoWriter]::Save($result.ImageStream, $media)
Write-Output $media
