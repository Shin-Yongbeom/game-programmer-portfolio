param(
    [ValidateSet('Build','Play','Combat')]
    [string]$Operation = 'Build'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Greyline-UnityResult.ps1"
$astraProject = Split-Path -Parent $PSScriptRoot
$astraEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
& "$PSScriptRoot/Greyline-ResourcePreflight.ps1" -Operation Unity

$astraMethods = @{
    Build = 'Greyline.World.EditorTools.ProductionDistrictCombatBuilder.BuildFromCommandLine'
    Play = 'Greyline.World.EditorTools.ProductionDistrictPlayQA.RunFromCommandLine'
    Combat = 'Greyline.World.EditorTools.ProductionDistrictPlayQA.RunCombatFromCommandLine'
}
$astraMarkers = @{
    Build = 'ASTRA_PRODUCTION_BUILD_OK'
    Play = 'PRODUCTION_DISTRICT_PLAY_QA_OK'
    Combat = 'PRODUCTION_DISTRICT_PLAY_QA_OK'
}
$astraLogRoot = Join-Path $astraProject 'Logs'
New-Item -ItemType Directory -Force -Path $astraLogRoot | Out-Null
$astraStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$astraLog = Join-Path $astraLogRoot "astra-$Operation-$astraStamp.log"
$astraErr = Join-Path $astraLogRoot "astra-$Operation-$astraStamp-stderr.log"
$astraArguments = @('-batchmode','-force-d3d11','-projectPath',('"' + $astraProject + '"'),'-executeMethod',$astraMethods[$Operation],'-logFile','-')
if ($Operation -eq 'Build') { $astraArguments = @('-quit') + $astraArguments }

# GUI executables can return immediately when invoked with '&'. Redirect and wait on the actual
# process so a shell exit code or empty stdout cannot be mistaken for a verified Unity operation.
$astraProcess = Start-Process -FilePath $astraEditor -ArgumentList $astraArguments -WindowStyle Hidden `
    -RedirectStandardOutput $astraLog -RedirectStandardError $astraErr -PassThru
# Cache the OS handle while Unity is alive: Windows PowerShell can otherwise return a null
# ExitCode after WaitForExit even when Unity actually exited successfully.
$null = $astraProcess.Handle
Write-Output "UnityPid=$($astraProcess.Id) Operation=$Operation Log=$astraLog"
$astraProcess.WaitForExit()
$astraCode = $astraProcess.ExitCode
$astraResult = Test-GreylineUnityResult -ExitCode $astraCode -SuccessMarker $astraMarkers[$Operation] `
    -LogText ([string](Get-Content -LiteralPath $astraLog -Raw)) `
    -ErrorText ([string](Get-Content -LiteralPath $astraErr -Raw))
if (-not $astraResult.Passed) {
    throw "Unity $Operation failed: $($astraResult.Reason). Exit=$astraCode Log=$astraLog Stderr=$astraErr"
}
Write-Output "Unity $Operation verified. Exit=$astraCode Marker=$($astraMarkers[$Operation]) Log=$astraLog"
