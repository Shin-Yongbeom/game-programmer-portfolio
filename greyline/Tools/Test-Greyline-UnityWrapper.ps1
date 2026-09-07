# Run with PowerShell. Exercises verdicts and real OS process handles without launching Unity.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Greyline-UnityResult.ps1"
$testCount = 0
function Assert-Verdict {
    param([string]$Name, [bool]$Expected, [AllowNull()][Nullable[int]]$Code,
        [string]$Log = 'GREYLINE_BUILD_OK', [string]$Stderr = '')
    $result = Test-GreylineUnityResult -ExitCode $Code -SuccessMarker 'GREYLINE_BUILD_OK' `
        -LogText $Log -ErrorText $Stderr
    if ($result.Passed -ne $Expected) { throw "$Name expected $Expected; got $($result.Passed): $($result.Reason)" }
    $script:testCount++
}

Assert-Verdict 'success' $true 0
Assert-Verdict 'missing exit is not success' $false $null
Assert-Verdict 'nonzero exit overrides marker' $false 7
Assert-Verdict 'missing marker' $false 0 ''
Assert-Verdict 'mentioning marker is not completion' $false 0 'Expected marker GREYLINE_BUILD_OK'
Assert-Verdict 'compiler error' $false 0 "file.cs(1): error CS1002: ; expected`nGREYLINE_BUILD_OK"
Assert-Verdict 'late assertion overrides success' $false 0 "GREYLINE_BUILD_OK`nAssertion failed"
Assert-Verdict 'QA failure overrides success' $false 0 "GREYLINE_BUILD_OK`nPRODUCTION_DISTRICT_PLAY_QA_FAILED"
Assert-Verdict 'runtime exception overrides success' $false 0 "GREYLINE_BUILD_OK`nNullReferenceException: object missing"
Assert-Verdict 'stderr failure overrides success' $false 0 'GREYLINE_BUILD_OK' 'Assertion failed'
Assert-Verdict 'warning is not failure' $true 0 "Warning: optional device missing`nGREYLINE_BUILD_OK" 'diagnostic output'
Assert-Verdict 'batch abort overrides success' $false 0 "GREYLINE_BUILD_OK`nAborting batchmode due to failure"
foreach ($marker in @('PRODUCTION_DISTRICT_PLAY_QA_OK', 'GREYLINE_BUILD_OK')) {
    $result = Test-GreylineUnityResult -ExitCode 0 -SuccessMarker $marker -LogText "$marker route=18/18"
    if (-not $result.Passed) { throw "Expected operation marker accepted: $marker" }
    $testCount++
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("GreylineWrapperTest-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    $shellPath = (Get-Process -Id $PID).Path
    foreach ($expectedCode in @(0, 7)) {
        $stdout = Join-Path $fixtureRoot "exit-$expectedCode.log"
        $stderr = Join-Path $fixtureRoot "exit-$expectedCode-stderr.log"
        $fixtureScript = Join-Path $fixtureRoot "exit-$expectedCode.ps1"
        Set-Content -LiteralPath $fixtureScript -Value "Start-Sleep -Milliseconds 100; Write-Output 'GREYLINE_BUILD_OK'; exit $expectedCode"
        $process = Start-Process -FilePath $shellPath -ArgumentList @('-NoProfile', '-File', ('"' + $fixtureScript + '"')) `
            -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
        # This is the same Windows PowerShell process lifecycle used by the launcher.
        $null = $process.Handle
        $process.WaitForExit()
        try {
            if ($null -eq $process.ExitCode -or $process.ExitCode -ne $expectedCode) {
                throw "Expected real process exit $expectedCode, got '$($process.ExitCode)'"
            }
            Assert-Verdict "real exit $expectedCode" ($expectedCode -eq 0) $process.ExitCode `
                ([string](Get-Content -LiteralPath $stdout -Raw)) ([string](Get-Content -LiteralPath $stderr -Raw))
        }
        finally { $process.Dispose() }
    }
}
finally {
    # Remove only our known fixture files; no recursive deletion or computed parent deletion.
    Get-ChildItem -LiteralPath $fixtureRoot -File | Remove-Item
    Remove-Item -LiteralPath $fixtureRoot
}
Write-Output "UNITY_WRAPPER_TESTS_OK cases=$testCount (no Unity launched)"
