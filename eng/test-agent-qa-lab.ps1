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
            if ($cycle -eq 1) {
                $partial = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;
                    selector=@{automationId='qa-notifications'};maxDepth=($matches[0].path.Count-1)}
                if ($partial.value.value.coverage.complete) { throw 'Expected deliberately incomplete query coverage.' }
                $refused = Call 'ensure_state' @{request=@{target=$partial.value.value.matches[0].target;property='checked';desired=$true;requestId='partial-selection'}} -ExpectedFailure
                if ($refused.value.error.code -ne 'runtime_input_selection_incomplete' -or $refused.value.error.details.dispatched -ne 'false') {
                    throw 'Incomplete selection did not preserve its pre-dispatch coverage diagnostic.'
                }
                $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
                if ($state.notifications -or $state.toggleCount -ne 0) { throw 'Rejected incomplete selection changed app state.' }
                $complete = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;
                    selector=@{automationId='qa-notifications'};maxDepth=32}
                if (-not $complete.value.value.coverage.complete) { throw 'Expected complete query coverage.' }
                $changed = Call 'ensure_state' @{request=@{target=$complete.value.value.matches[0].target;property='checked';desired=$true;requestId='complete-selection'}}
                if ($changed.value.value.dispatchedOperations -ne 1 -or -not $changed.value.value.verified) { throw 'Complete selection did not toggle exactly once.' }
                $again = Call 'ensure_state' @{request=@{target=$complete.value.value.matches[0].target;property='checked';desired=$true;requestId='already-checked'}}
                if ($again.value.value.status -ne 'already_satisfied' -or $again.value.value.dispatchedOperations -ne 0) { throw 'Already-satisfied state dispatched input.' }
            } else {
                $null = Call 'input' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;action='toggle';targetNodeId=$matches[0].node.nodeId}
            }
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
        $legacy = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;automationId='qa-notifications';maxDepth=32}
        $query = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;selector=@{automationId='qa-notifications'};maxDepth=32}
        if (@($legacy.value.value.matches).Count -ne 1 -or @($query.value.value.matches).Count -ne 1) { throw 'Expected one notification control in each query.' }
        $legacyNode = @($legacy.value.value.matches)[0].node
        $queryNode = @($query.value.value.matches)[0].node
        if ($legacyNode.nodeId -ne $queryNode.nodeId -or
            ($legacyNode.bounds | ConvertTo-Json -Compress) -ne ($queryNode.bounds | ConvertTo-Json -Compress)) {
            throw 'Structured and legacy visual query bounds disagree.'
        }
        $topTarget = @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;targetKind='top_level';topLevelGeneration=$queryNode.target.topLevelGeneration}
        $geometry = Call 'pick_node' @{request=@{target=$topTarget}}
        $bounds = $queryNode.bounds
        $picked = Call 'pick_node' @{request=@{target=$topTarget;x=($bounds.x+$bounds.width/2);y=($bounds.y+$bounds.height/2);
            coordinateSpace='top_level_dip';expectedGeometryRevision=$geometry.value.value.geometry.revision}}
        if ($queryNode.nodeId -notin @($picked.value.value.hitPath.target.nodeId)) { throw 'Query bounds center missed the notification control.' }
        $null = Call 'run_workflow' @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
            @{action='select';selector=@{automationId='qa-identity-tab'}})}}
        foreach ($id in @('Example_Button_Key_a','Example_Button_Key_A','EXAMPLE_BUTTON_KEY_A')) {
            $found = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;selector=@{automationId=$id};maxDepth=32}
            $matches = @($found.value.value.matches)
            if ($id -ceq 'EXAMPLE_BUTTON_KEY_A') {
                if ($matches.Count -ne 0) { throw 'Wrong-case AutomationID unexpectedly matched.' }
                continue
            }
            if ($matches.Count -ne 1 -or $matches[0].node.automationId -cne $id) { throw 'Exact AutomationID did not select one identical ID.' }
            $null = Call 'run_workflow' @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
                @{action='invoke';selector=@{automationId=$id}})}}
            $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
            $expectedUpper = if ($id -ceq 'Example_Button_Key_a') { 0 } else { 1 }
            if ($state.lowercaseCount -ne 1 -or $state.uppercaseCount -ne $expectedUpper) { throw 'Exact-ID action affected the wrong control.' }
        }
        $null = & $entry -Operation Reset -RunDirectory $run.root
        $openChild = @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
            @{action='select';selector=@{automationId='qa-windows-tab'}},
            @{action='invoke';selector=@{automationId='qa-open-child'}})}}
        $null = Call 'run_workflow' $openChild
        $levels = Call 'list_top_levels' @{sessionId=$run.sessionId}
        $child = @($levels.value.value.topLevels | Where-Object title -eq 'AvaScope QA details')
        if ($child.Count -ne 1) { throw 'One open child window was expected.' }
        $close = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$child[0].id;automationId='qa-close-child';maxDepth=24;maxResults=4}
        $null = Call 'input' @{sessionId=$run.sessionId;topLevelId=$child[0].id;action='invoke';targetNodeId=$close.value.value.matches[0].node.nodeId}
        $levels = Call 'list_top_levels' @{sessionId=$run.sessionId}
        if (@($levels.value.value.topLevels).Count -ne 1) { throw 'A closed child remained registered.' }
        $null = Call 'run_workflow' $openChild
        $levels = Call 'list_top_levels' @{sessionId=$run.sessionId}
        $reopened = @($levels.value.value.topLevels | Where-Object title -eq 'AvaScope QA details')
        if ($reopened.Count -ne 1 -or $reopened[0].id -eq $child[0].id) { throw 'Reopened child did not have one fresh identity.' }
        $null = & $entry -Operation Reset -RunDirectory $run.root
        $levels = Call 'list_top_levels' @{sessionId=$run.sessionId}
        if (@($levels.value.value.topLevels).Count -ne 1) { throw 'Reset left a child window registered.' }
        $results.Add(@{integration=$integration;backend=$run.observedBackend;scale=$run.renderScaling;status='passed';cycles=2;negativeFailurePreserved=$true;childCloseReopenVerified=$true;exactAutomationIdsVerified=$true;queryBoundsVerified=$true;selectionCoverageVerified=$true;root=$run.root})
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
