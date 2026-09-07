param(
    [ValidateSet('Build','Play','Combat')]
    [string]$Operation = 'Build'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Greyline-UnityResult.ps1"
$unityProject = Split-Path -Parent $PSScriptRoot
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
& "$PSScriptRoot/Greyline-ResourcePreflight.ps1" -Operation Unity

$unityMethods = @{
    Build = 'Greyline.World.EditorTools.ProductionDistrictCombatBuilder.BuildFromCommandLine'
    Play = 'Greyline.World.EditorTools.ProductionDistrictPlayQA.RunFromCommandLine'
    Combat = 'Greyline.World.EditorTools.ProductionDistrictPlayQA.RunCombatFromCommandLine'
}
$unityMarkers = @{
    Build = 'GREYLINE_BUILD_OK'
    Play = 'PRODUCTION_DISTRICT_PLAY_QA_OK'
    Combat = 'PRODUCTION_DISTRICT_PLAY_QA_OK'
}
$unityLogRoot = Join-Path $unityProject 'Logs'
New-Item -ItemType Directory -Force -Path $unityLogRoot | Out-Null
$unityStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$unityLog = Join-Path $unityLogRoot "greyline-$Operation-$unityStamp.log"
$unityErr = Join-Path $unityLogRoot "greyline-$Operation-$unityStamp-stderr.log"
$unityArguments = @('-batchmode','-force-d3d11','-projectPath',('"' + $unityProject + '"'),'-executeMethod',$unityMethods[$Operation],'-logFile','-')
if ($Operation -eq 'Build') { $unityArguments = @('-quit') + $unityArguments }

# GUI executables can return immediately when invoked with '&'. Redirect and wait on the actual
# process so a shell exit code or empty stdout cannot be mistaken for a verified Unity operation.
$unityProcess = Start-Process -FilePath $unityEditor -ArgumentList $unityArguments -WindowStyle Hidden `
    -RedirectStandardOutput $unityLog -RedirectStandardError $unityErr -PassThru
# Cache the OS handle while Unity is alive: Windows PowerShell can otherwise return a null
# ExitCode after WaitForExit even when Unity actually exited successfully.
$null = $unityProcess.Handle
Write-Output "UnityPid=$($unityProcess.Id) Operation=$Operation Log=$unityLog"
$unityProcess.WaitForExit()
$unityCode = $unityProcess.ExitCode
$unityResult = Test-GreylineUnityResult -ExitCode $unityCode -SuccessMarker $unityMarkers[$Operation] `
    -LogText ([string](Get-Content -LiteralPath $unityLog -Raw)) `
    -ErrorText ([string](Get-Content -LiteralPath $unityErr -Raw))
if (-not $unityResult.Passed) {
    throw "Unity $Operation failed: $($unityResult.Reason). Exit=$unityCode Log=$unityLog Stderr=$unityErr"
}
Write-Output "Unity $Operation verified. Exit=$unityCode Marker=$($unityMarkers[$Operation]) Log=$unityLog"
