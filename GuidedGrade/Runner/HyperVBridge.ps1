$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$runnerVm = $null
$guestSession = $null
try {
    $config = [Console]::ReadLine() | ConvertFrom-Json
    Import-Module Hyper-V -ErrorAction Stop
    $credential = Import-Clixml -LiteralPath $config.credentialFile
    if ($credential -isnot [System.Management.Automation.PSCredential]) { throw 'The guest credential file is invalid.' }
    $parentDisk = Get-VHD -Path $config.baseDisk
    if ($parentDisk.Attached) { throw 'The template disk must be detached. Shut down its template VM first.' }
    $childDisk = Join-Path $config.runDirectory 'guest.vhdx'
    New-VHD -Path $childDisk -ParentPath $config.baseDisk -Differencing | Out-Null
    $runnerVm = New-VM -Name ('GuidedGrade-' + [Guid]::NewGuid().ToString('N')) -Generation 2 `
        -MemoryStartupBytes ([long]$config.memoryMb * 1MB) -VHDPath $childDisk -Path $config.runDirectory
    # Store the exact VM id, not a user-supplied VM name, for independent cleanup.
    $runnerVm.Id.ToString() | Set-Content -LiteralPath (Join-Path $config.runDirectory 'vm-id.txt')
    Set-VM -VM $runnerVm -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction TurnOff
    Set-VMProcessor -VM $runnerVm -Count 2 -Maximum 50
    Set-VMKeyProtector -VM $runnerVm -NewLocalKeyProtector
    Enable-VMTPM -VM $runnerVm
    Get-VMNetworkAdapter -VM $runnerVm | Remove-VMNetworkAdapter
    $watchdog = Join-Path $PSScriptRoot 'HyperVWatchdog.ps1'
    $watchdogArgs = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -VmId {1} -OwnerPid {2}' -f $watchdog, $runnerVm.Id, $PID
    Start-Process -FilePath "$PSHOME\powershell.exe" -ArgumentList $watchdogArgs -WindowStyle Hidden | Out-Null
    Start-VM -VM $runnerVm | Out-Null
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    do {
        try { $guestSession = New-PSSession -VMId $runnerVm.Id -Credential $credential -ErrorAction Stop }
        catch { if ([DateTime]::UtcNow -ge $deadline) { throw 'Could not sign in to the guest within three minutes. Check the template and guest credentials.' }; Start-Sleep -Seconds 2 }
    } until ($null -ne $guestSession)

    Invoke-Command -Session $guestSession -ScriptBlock {
        New-Item -ItemType Directory -Path 'C:\GuidedGrade' -Force | Out-Null
    }
    Copy-Item -LiteralPath $config.submissionArchive -Destination 'C:\GuidedGrade\submission.zip' -ToSession $guestSession
    Copy-Item -LiteralPath $config.workerArchive -Destination 'C:\GuidedGrade\worker.zip' -ToSession $guestSession
    Invoke-Command -Session $guestSession -ScriptBlock {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory('C:\GuidedGrade\submission.zip', 'C:\GuidedGrade\Submission')
        [IO.Compression.ZipFile]::ExtractToDirectory('C:\GuidedGrade\worker.zip', 'C:\GuidedGrade\Worker')
        $start = New-Object System.Diagnostics.ProcessStartInfo
        $start.FileName = 'C:\GuidedGrade\Worker\GuidedGrade.Runner.exe'
        $start.Arguments = '"C:\GuidedGrade\Submission"'
        $start.WorkingDirectory = 'C:\GuidedGrade\Worker'
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardInput = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
        $start.StandardInputEncoding = New-Object System.Text.UTF8Encoding($false)
        $script:worker = [Diagnostics.Process]::Start($start)
        $script:workerError = $script:worker.StandardError.ReadToEndAsync()
    }
    [Console]::WriteLine('{"ok":true,"result":{"ready":true}}')
    while ($null -ne ($line = [Console]::ReadLine())) {
        if ($line.Length -gt 65536) { throw 'Runner request is too large.' }
        $request = $line | ConvertFrom-Json
        if ($request.command -eq 'shutdown') { break }
        $reply = Invoke-Command -Session $guestSession -ArgumentList $line -ScriptBlock {
            param($requestLine)
            $script:worker.StandardInput.WriteLine($requestLine)
            $script:worker.StandardInput.Flush()
            $read = $script:worker.StandardOutput.ReadLineAsync()
            if (-not $read.Wait(120000)) { throw 'Guest worker timed out.' }
            $response = $read.Result
            if ($null -eq $response) { throw 'Guest worker exited unexpectedly. Verify its runtime dependencies.' }
            if ($response.Length -gt 1048576) { throw 'Guest response exceeds the protocol limit.' }
            return $response
        }
        [Console]::WriteLine([string]$reply)
    }
}
catch {
    [Console]::WriteLine((@{ ok = $false; error = $_.Exception.Message } | ConvertTo-Json -Compress))
}
finally {
    if ($null -ne $guestSession) { Remove-PSSession -Session $guestSession -ErrorAction SilentlyContinue }
    if ($null -ne $runnerVm) {
        # Only the disposable VM created by this invocation can be removed.
        Stop-VM -VM $runnerVm -TurnOff -Force -ErrorAction SilentlyContinue
        Remove-VM -VM $runnerVm -Force -ErrorAction SilentlyContinue
    }
}
