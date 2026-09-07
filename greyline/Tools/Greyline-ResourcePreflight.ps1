param(
    [ValidateSet('Git','Unity')]
    [string]$Operation = 'Git',
    [int]$MinimumFreeMemoryGB = 10,
    [int]$MinimumFreeDiskGB = 20
)

$ErrorActionPreference = 'Stop'
$unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
$gitProcesses = @(Get-Process git,git-lfs -ErrorAction SilentlyContinue)
$repoRoot = Split-Path -Parent $PSScriptRoot
$drive = [System.IO.Path]::GetPathRoot($repoRoot)
$freeDiskGB = [math]::Round(([System.IO.DriveInfo]::new($drive).AvailableFreeSpace) / 1GB, 1)

try {
    $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
    $freeMemoryGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
} catch {
    try {
        $availableMB = (Get-Counter '\Memory\Available MBytes' -ErrorAction Stop).CounterSamples[0].CookedValue
        $freeMemoryGB = [math]::Round($availableMB / 1024, 1)
    } catch {
        throw 'Preflight failed: available physical memory could not be measured. Do not start Unity or Git.'
    }
}

Write-Output "Operation=$Operation FreeMemoryGB=$freeMemoryGB FreeDiskGB=$freeDiskGB UnityProcesses=$($unityProcesses.Count) GitProcesses=$($gitProcesses.Count)"
if ($freeMemoryGB -lt $MinimumFreeMemoryGB) { throw "Preflight failed: free physical memory is below ${MinimumFreeMemoryGB}GB." }
if ($freeDiskGB -lt $MinimumFreeDiskGB) { throw "Preflight failed: free disk space is below ${MinimumFreeDiskGB}GB." }
if ($Operation -eq 'Git' -and $unityProcesses.Count -gt 0) { throw 'Preflight failed: Unity is running. Close Unity before Git worktree/checkout operations.' }
if ($Operation -eq 'Git' -and $gitProcesses.Count -gt 0) { throw 'Preflight failed: another Git process is running. Do not start a concurrent checkout/worktree operation.' }
if ($Operation -eq 'Unity' -and $unityProcesses.Count -gt 0) { throw 'Preflight failed: a Unity process is already running. Do not start a second Editor or batchmode process.' }
Write-Output 'Preflight passed.'
