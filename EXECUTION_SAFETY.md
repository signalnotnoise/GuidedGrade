# Student execution safety

For sequential AI testing, duplicate handling, saved reports and memory limitations, see [AI test queue](AI_TEST_QUEUE.md).

## Choosing an execution environment

**Isolated Windows VM (Hyper-V)** is the default, including for existing settings
that predate the execution-mode setting. Choose the default in Settings under Build and run on, or LLM Settings under
Execution. Build, Build and Run, Run, and Test with AI use it. An unavailable VM never triggers a
local fallback.

**Local computer** remains available for trusted submissions on smaller machines.
Both Settings windows expose Ask before each local build or run. Disabling it authorizes
local operations without repeated prompts; enabling it asks before each local operation.
Existing settings keep prompting until the preference is saved. The right-click **Test with AI
locally...** command uses local execution once without changing the saved environment or confirmation preference.
Build scripts and student programs then have your Windows permissions; staging a
copy is not isolation. Prefer running the app without administrator privileges for
local execution.

## Preparing Hyper-V

This requires a Windows host with Hyper-V and its PowerShell management module,
hardware virtualization, and an account permitted to manage Hyper-V/PowerShell
Direct. Feature installation may require administrator privileges and a restart;
the application does not change Windows features or elevate itself automatically.

1. Prepare a dedicated **Generation 2 Windows VM** with a local guest administrator
   account. Use a guest-only password, never your host or domain credentials.
2. Install the compilers/SDKs needed by the assignments (for example Visual Studio
   Build Tools with Desktop development with C++, Windows SDK, and .NET SDK).
   Cache required packages offline: the disposable guest has **no network adapter**.
   Keep the template free of personal files, credentials, shares, and automatic
   sign-ins. Do not use a domain-joined image or one requiring interactive BitLocker
   recovery. The template must be bootable without a VM-specific TPM.
3. Shut down the template VM and detach its disk or copy its VHDX while it is shut
   down. Keep this dedicated template detached and unchanged while tests run.
   Use a reasonably sized virtual disk and maintain enough host free space for its
   differencing disks. There is no per-run host disk quota.
4. In Windows PowerShell, under the same host account that will run the app,
   export a guest credential file (Windows encrypts the credential for that user
   and computer):

   ```powershell
   Get-Credential | Export-Clixml -LiteralPath 'C:\RunnerSetup\guest-credential.xml'
   ```

   Enter the **guest's** local account, e.g. `GUESTNAME\LabRunner`, and password.
   Keep this XML file outside submission and worker folders. Only its path is
   saved in the app's settings; no LLM keys or settings file are sent to the guest.
5. Publish the guest worker from this repository:

   ```powershell
   dotnet publish 'GuidedGrade.Runner/GuidedGrade.Runner.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts/runner'
   ```

   A framework-dependent build also works if the matching .NET 10 runtime is
   installed in the guest. The self-contained publish avoids that prerequisite.
6. In LLM Settings > Execution select the VHDX, credential XML, published worker
   folder, and VM memory (default 4096 MB; allowed 2048-16384 MB). Save settings.

## Operation and cleanup

The app creates a fresh differencing disk and a uniquely named `GuidedGrade-*` VM
for each operation. It removes all network adapters, allocates two virtual CPUs
with a CPU cap, and communicates through PowerShell Direct. It copies only a
submission archive and worker archive into the guest. No host folder is mapped.
The guest builds and runs code; the host calls the LLM and sends one console reply
at a time. Source review remains available even when isolation cannot start.

**Run** captures guest output for up to 90 seconds; **Test with AI** drives
interactive prompts through redirected stdin/stdout/stderr. This initial guest
transport does not automate GUI applications or programs requiring direct Win32
console APIs (such as console screen-buffer access). Use the warned local option
for those trusted submissions; absent captured output is not a grading defect.
The AI testing budget is 12 interactions/90 seconds plus
bounded transport calls. A separate watchdog turns off and removes the disposable
VM if its bridge dies or after a 15-minute hard lifetime. This watchdog is outside
the guest, so student code cannot disable it through ordinary guest permissions.
The template is not reset or deleted. Graceful cleanup removes the run directory;
after interruption, VM disk files may remain under
`%LOCALAPPDATA%\GuidedGrade\Runners`. Remove those only after confirming their
disposable VM is gone and the VHDX is detached. No existing user VM is targeted.
The first local-data access after the product rename moves the previous product
directory into `%LOCALAPPDATA%\GuidedGrade` when that destination is absent.
Saved guest credential and worker paths under that previous directory follow
their migrated files so existing Hyper-V execution settings remain valid.

Runner input is capped at 1 GB/20,000 files per archive, guest protocol responses
at 1 MB, and reported output at 64,000 characters per stream. These limits are
resource controls, not proof of a student defect. Time budgets and runner-caused
termination are inconclusive; failed builds never fall back to stale binaries.

## Validation

The automated suite covers mode defaults, saved and per-click local consent, run without compilation, no silent
fallback, bounded protocol parsing, submission packaging, and a worker integration
test that builds a synthetic console project and replies to its prompt without
LLM access. A real Hyper-V acceptance test additionally requires the prepared
template: verify fresh state on successive runs, no guest network adapter, live
prompt/reply, compiler errors, and cleanup after closing the host app. Worker tests
on the host do not validate the VM isolation boundary.

## Manual solution actions

Build compiles; Build and Run compiles then launches; Run launches existing output without compilation and reports when none exists. Local manual actions use the selected submission folder, preserving outputs for later Run actions. AI testing continues to stage source copies. VM Run uses prebuilt output uploaded from the selected folder; VM Build outputs are discarded with the disposable guest, so use Build and Run to compile and launch in one VM. Republish the runner after this update to enable the `resolve` worker command. VM runs capture output for up to 90 seconds; use Test with AI for automated console input.

## Native debug-database build failures

Native solution and C++ project builds use one MSBuild worker (/m:1). Solution builds preserve project intermediate directories rather than overriding all projects to the same IntDir; the existing output-directory override remains for native post-build dependency copies. This avoids GuidedGrade introducing shared compiler PDB/object paths. C1041 is reported as a build-file locking/configuration issue, not proof of incorrect source. External builds, sync tools, or conflicting project settings can still cause locks. Close competing builds/debuggers, retry from a local copy outside OneDrive when needed, and configure separate intermediate paths and /FS in the projects if the failure persists. LNK4099 about a third-party library PDB is a separate missing-debug-symbol warning; it is not suppressed. Student project files are not modified.

October 7 native launch-directory fix: ResolveWorkingDirectory first maps the launched executable name to a matching .sln project display name and its existing .vcxproj path before the filename heuristic. This handles Lab1.exe from CaveMatchingGame.vcxproj even when the solution output directory points under Practice. Direct project launches still use their project folder. Manual Run reports the full executable and working-directory paths. This fixes relative source/asset lookup for mapped projects; it does not establish that student initialization or graphics loops are correct.

Executable LNK1104 failures now include guidance to close the prior submission before rebuilding, since running executables and loaded DLLs can block linking and post-build copies. No processes are terminated automatically.
