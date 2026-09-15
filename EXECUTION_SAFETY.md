# Student execution safety

For sequential AI testing, duplicate handling, saved reports and memory limitations, see [AI test queue](AI_TEST_QUEUE.md).

## Choosing an execution environment

**Isolated Windows VM (Hyper-V)** is the default, including for existing settings
that predate the execution-mode setting. Choose the default in LLM Settings under
Execution. Build, Run, and Test with AI use it. An unavailable VM never triggers a
local fallback.

**Local computer** remains available for trusted submissions on smaller machines.
Every local operation requires a fresh Yes/No warning, defaulting to No. Selecting
local mode does not suppress future warnings. The right-click **Test with AI
locally...** command uses local execution once without changing the saved default.
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
   dotnet publish 'Lab Feedback Runner/Lab Feedback Runner.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts/runner'
   ```

   A framework-dependent build also works if the matching .NET 10 runtime is
   installed in the guest. The self-contained publish avoids that prerequisite.
6. In LLM Settings > Execution select the VHDX, credential XML, published worker
   folder, and VM memory (default 4096 MB; allowed 2048-16384 MB). Save settings.

## Operation and cleanup

The app creates a fresh differencing disk and a uniquely named `LabFeedback-*` VM
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
`%LOCALAPPDATA%\LabFeedbackWPF\Runners`. Remove those only after confirming their
disposable VM is gone and the VHDX is detached. No existing user VM is targeted.

Runner input is capped at 1 GB/20,000 files per archive, guest protocol responses
at 1 MB, and reported output at 64,000 characters per stream. These limits are
resource controls, not proof of a student defect. Time budgets and runner-caused
termination are inconclusive; failed builds never fall back to stale binaries.

## Validation

The automated suite covers mode defaults, per-click local consent, no silent
fallback, bounded protocol parsing, submission packaging, and a worker integration
test that builds a synthetic console project and replies to its prompt without
LLM access. A real Hyper-V acceptance test additionally requires the prepared
template: verify fresh state on successive runs, no guest network adapter, live
prompt/reply, compiler errors, and cleanup after closing the host app. Worker tests
on the host do not validate the VM isolation boundary.
