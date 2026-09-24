#requires -Version 7.4
param(
    [ValidateSet('Native','Headless')][string]$Backend = 'Native',
    [switch]$SkipBuild,
    [string]$OutputDirectory,
    [switch]$TestExpiry
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$entry = Join-Path $PSScriptRoot 'agent-qa.ps1'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts/agent-qa/lifecycle-' + [Guid]::NewGuid().ToString('N')) }
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Lifecycle validation requires a fresh output directory.' }
$results = [Collections.Generic.List[object]]::new()

function Call([string]$ToolName, [hashtable]$Arguments, [switch]$ExpectedFailure) {
    $path = Join-Path $run.root ('arguments-' + [Guid]::NewGuid().ToString('N') + '.json')
    $Arguments | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path
    $called = & $entry -Operation Call -RunDirectory $run.root -Tool $ToolName -ArgumentsPath $path -AllowFailure:$ExpectedFailure | ConvertFrom-Json -Depth 100
    if ([bool]$called.success -eq $ExpectedFailure.IsPresent) { throw "Unexpected outcome for $ToolName" }
    return $called
}

foreach ($integration in @('Direct','Standalone')) {
    $runPath = Join-Path $root $integration.ToLowerInvariant()
    $run = $null
    try {
        $run = & $entry -Operation Start -Integration $integration -Backend $Backend -RunDirectory $runPath -SkipBuild:($SkipBuild -or $integration -eq 'Standalone') | ConvertFrom-Json
        for ($cycle = 1; $cycle -le 2; $cycle++) {
            $found = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;automationId='qa-notifications';maxDepth=24;maxResults=4}
            $matches = @($found.value.value.matches)
            if ($matches.Count -ne 1) { throw 'The seeded notification control was not uniquely discovered.' }
            $null = Call 'input' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;action='toggle';targetNodeId=$matches[0].node.nodeId}
            $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
            if (-not $state.notifications -or $state.toggleCount -ne 1) { throw 'The independent journal did not observe exactly one toggle.' }
            $capture = Call 'screenshot' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;outputPath=(Join-Path $run.root "cycle-$cycle.png")}
            if (-not (Test-Path -LiteralPath $capture.value.value.filePath)) { throw 'Public screenshot returned no artifact.' }
            $null = & $entry -Operation Reset -RunDirectory $run.root
            $reset = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
            if ($reset.notifications -or $reset.toggleCount -ne 0 -or $reset.displayName -ne 'Ada') { throw 'Reset did not restore seeded state.' }
        }
        $negative = Call 'inspect_node' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;nodeId='visual:does-not-exist'} -ExpectedFailure
        if (-not (Test-Path -LiteralPath $negative.evidence.responsePath)) { throw 'Failure evidence is missing.' }
        $results.Add(@{integration=$integration;backend=$run.observedBackend;scale=$run.renderScaling;status='passed';cycles=2;negativeFailurePreserved=$true;root=$run.root})
    }
    finally {
        if (Test-Path (Join-Path $runPath 'qa-run.json')) {
            $record = Get-Content (Join-Path $runPath 'qa-run.json') -Raw | ConvertFrom-Json
            if ($record.sessionId) { $null = & $entry -Operation Stop -RunDirectory $runPath }
        }
    }
}

if ($TestExpiry) {
    $runPath = Join-Path $root 'expiry'
    $run = & $entry -Operation Start -Integration Direct -Backend $Backend -RunDirectory $runPath -SkipBuild -HostDirectory (Join-Path $root 'direct/host') -LifetimeSeconds 30 | ConvertFrom-Json
    try {
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
        do {
            $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
            if (@($state.events | Where-Object action -eq 'window_closed').Count) { break }
            Start-Sleep -Milliseconds 250
        } while ([DateTimeOffset]::UtcNow -lt $deadline)
        if (-not @($state.events | Where-Object action -eq 'window_closed').Count) { throw 'The retained fixture did not close at lease expiry.' }
        $results.Add(@{integration='Direct';status='passed';case='lease_expiry';root=$run.root})
    }
    finally { $null = & $entry -Operation Stop -RunDirectory $runPath }
}
@{schemaVersion=1;mode='scripted_lifecycle_validation';agentExploration=$false;results=$results} |
    ConvertTo-Json -Depth 12 | Tee-Object -FilePath (Join-Path $root 'lifecycle-summary.json')
