# Shared by the Unity launcher and its fast, Unity-free regression fixtures.
function Test-GreylineUnityResult {
    param(
        [AllowNull()][Nullable[int]]$ExitCode,
        [Parameter(Mandatory)][string]$SuccessMarker,
        [AllowEmptyString()][string]$LogText = '',
        [AllowEmptyString()][string]$ErrorText = ''
    )

    $reasons = [System.Collections.Generic.List[string]]::new()
    # Missing process evidence remains a failure, even when an earlier success marker exists.
    if ($null -eq $ExitCode) { $reasons.Add('process exit code unavailable') }
    elseif ($ExitCode -ne 0) { $reasons.Add("process exited with code $ExitCode") }
    if ($LogText -notmatch ('(?m)^' + [regex]::Escape($SuccessMarker) + '(?:\s|$)')) {
        $reasons.Add("missing success marker $SuccessMarker")
    }

    # Unity warnings and ordinary stderr output alone are not failures. Check both streams for
    # explicit compiler/runtime/assertion/QA failures, including errors after the success marker.
    $failurePattern = 'error CS[0-9]+|Assertion failed|PLAY_QA_FAILED|Aborting batchmode due to|' +
        '(?m)^(?:[A-Za-z_][\w]*\.)*[A-Za-z_][\w]*Exception:'
    foreach ($stream in @(@{ Name = 'stdout'; Text = $LogText }, @{ Name = 'stderr'; Text = $ErrorText })) {
        if ($stream.Text -match $failurePattern) {
            $reasons.Add("$($stream.Name) contains $($Matches[0])")
        }
    }
    [pscustomobject]@{ Passed = ($reasons.Count -eq 0); Reason = ($reasons -join '; ') }
}
