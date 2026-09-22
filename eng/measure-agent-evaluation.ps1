#requires -Version 7.4
param(
    [Parameter(Mandatory)][string]$TracePath,
    [Parameter(Mandatory)][string]$OutcomesPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
foreach ($path in @($TracePath,$OutcomesPath)) {
    $file = Get-Item -LiteralPath $path
    if ($file.Length -gt 4MB -or $file.PSIsContainer) { throw 'Evaluation input must be a file of at most 4 MiB.' }
}
$trace = @(Get-Content -LiteralPath $TracePath | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json -AsHashtable })
$outcome = Get-Content -Raw -LiteralPath $OutcomesPath | ConvertFrom-Json -AsHashtable
if ($trace.Count -eq 0 -or $trace.Count -gt 2000 -or $outcome.schemaVersion -ne 1 -or $outcome.mode -ne 'agent_outcomes' -or
    -not $outcome.client -or -not $outcome.model -or -not $outcome.configuration -or -not $outcome.trials -or
    $outcome.trials.Count -gt 100) { throw 'A bounded agent trace and explicit client/model/configuration/trial provenance are required.' }
if (@($trace | Where-Object { $_.mode -ne 'agent_tool_capture' -or $_.schemaVersion -ne 1 -or
        -not $_.trialId -or -not $_.tool -or $_.requestFingerprint -notmatch '^[a-f0-9]{64}$' -or
        $_.retryDisposition -notin @('none','necessary','unnecessary','unknown') }).Count) {
    throw 'Deterministic conformance traces cannot be relabeled as measured agent outcomes.'
}
$ids = @($outcome.trials | ForEach-Object trialId)
if (@($ids | Select-Object -Unique).Count -ne $ids.Count -or @($trace | Where-Object { $_.trialId -notin $ids }).Count) {
    throw 'Every measured tool attempt must belong to exactly one declared trial.'
}
$trials = @()
foreach ($trial in $outcome.trials) {
    if ($trial.expectedClassification -notin @('none','environment','application') -or
        $trial.reportedClassification -notin @('none','environment','application','unknown') -or
        $trial.taskCompleted -isnot [bool] -or -not $trial.evidencePaths) { throw 'Each trial needs its known fixture classification, agent decision, completion flag and evidence paths.' }
    $calls = @($trace | Where-Object trialId -eq $trial.trialId)
    if ($calls.Count -eq 0) { throw 'An agent trial without measured tool calls is not a completed evaluation.' }
    $evidence = @()
    foreach ($path in $trial.evidencePaths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'A declared evidence file is missing.' }
        $evidence += @{path=[IO.Path]::GetFullPath($path);sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $unknownRetries = @($calls | Where-Object retryDisposition -eq 'unknown').Count
    # A repeated identical call in unchanged context needs an explicit retry judgment.
    for ($index = 1; $index -lt $calls.Count; $index++) {
        $previous = $calls[$index-1]; $current = $calls[$index]
        if ($current.tool -eq $previous.tool -and $current.requestFingerprint -eq $previous.requestFingerprint -and
            $current.contextRevision -eq $previous.contextRevision -and $current.retryDisposition -eq 'none') { $unknownRetries++ }
    }
    $correct = $trial.expectedClassification -eq $trial.reportedClassification
    $trials += @{
        trialId=$trial.trialId;taskCompleted=$trial.taskCompleted;classificationCorrect=$correct
        expectedClassification=$trial.expectedClassification;reportedClassification=$trial.reportedClassification
        toolCalls=$calls.Count;unnecessaryRetries=@($calls | Where-Object retryDisposition -eq 'unnecessary').Count
        unknownRetryJudgments=$unknownRetries;toolDurationMs=($calls.durationMs | Measure-Object -Sum).Sum
        transportFailures=@($calls | Where-Object { -not $_.transportCompleted }).Count;evidence=$evidence
    }
}
$configurationJson = $outcome.configuration | ConvertTo-Json -Depth 20 -Compress
$summary = @{
    schemaVersion=1;mode='agent_outcomes';client=$outcome.client;model=$outcome.model
    configurationSha256=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($configurationJson)))
    provenanceSource='explicit evaluator declaration';classificationSource='agent decision compared with declared seeded fixture'
    retryJudgmentSource='explicit per-call annotation; identical unexplained calls remain unknown'
    serverVersions=@($trace.serverVersion | Select-Object -Unique);trialCount=$trials.Count
    completedTrials=@($trials | Where-Object taskCompleted).Count
    correctlyClassifiedTrials=@($trials | Where-Object classificationCorrect).Count
    toolCalls=$trace.Count;trials=$trials
} | ConvertTo-Json -Depth 20
$fullOutput = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullOutput)) | Out-Null
[IO.File]::WriteAllText($fullOutput, $summary)
Write-Output "Measured $($trace.Count) AvaScope tool attempts across $($trials.Count) agent trials: $fullOutput"
