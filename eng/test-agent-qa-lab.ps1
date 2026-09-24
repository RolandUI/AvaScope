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
            $capture = Call 'screenshot' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;outputPath=(Join-Path $run.root "cycle-$cycle.png");captureAfterRender=$true}
            if (-not (Test-Path -LiteralPath $capture.value.value.filePath)) { throw 'Public screenshot returned no artifact.' }
            if ($capture.value.value.readiness.application.status -ne 'ready') { throw 'The host did not declare application readiness.' }
            $null = Call 'run_workflow' @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
                @{action='select';selector=@{automationId='qa-table-tab'}})}}
            $rows = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;
                nodeType='Avalonia.Controls.DataGridRow';rendered=$true;maxDepth=32;maxResults=2}
            if (@($rows.value.value.matches).Count -lt 1) { throw 'The QA table has data but no rendered row controls; verify theme initialization.' }
            $table = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;automationId='qa-table';maxDepth=32;maxResults=2}
            if (@($table.value.value.matches).Count -ne 1) { throw 'The QA table was not uniquely discovered.' }
            $data = Call 'query_table' @{request=@{table=$table.value.value.matches[0].node.target;keyProperty='Id';limit=3}}
            if ($data.value.value.coverage.totalAvailableRows -ne 200 -or @($data.value.value.rows | Where-Object realized).Count -lt 1) {
                throw 'The QA table must expose both seeded data and realized cells.'
            }
            $null = Call 'screenshot' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;
                outputPath=(Join-Path $run.root "cycle-$cycle-table.png");captureAfterRender=$true}
            $null = & $entry -Operation Reset -RunDirectory $run.root
            $reset = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
            if ($reset.notifications -or $reset.toggleCount -ne 0 -or $reset.displayName -ne 'Ada') { throw 'Reset did not restore seeded state.' }
        }
        $null = Call 'run_workflow' @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
            @{action='invoke';selector=@{automationId='qa-load'}},
            @{action='wait_for_state';waitCondition=@{kind='application_ready'};timeoutMs=5000})}}
        $loaded = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
        if ($loaded.loadStatus -ne 'Loaded 200 records') { throw 'Declared readiness completed before the app finished loading.' }
        $null = & $entry -Operation Reset -RunDirectory $run.root
        $negative = Call 'inspect_node' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;nodeId='visual:does-not-exist'} -ExpectedFailure
        if (-not (Test-Path -LiteralPath $negative.evidence.responsePath)) { throw 'Failure evidence is missing.' }
        $sizeAction = @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
            @{action='invoke';selector=@{automationId='qa-size'}})}}
        $null = Call 'run_workflow' $sizeAction
        $fullHd = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
        if ($fullHd.requestedWidth -ne 1920 -or $fullHd.requestedHeight -ne 1080) { throw 'Full HD scene intent was overwritten by observed geometry.' }
        $fullHdGeometry = @{requestedWidth=$fullHd.requestedWidth;requestedHeight=$fullHd.requestedHeight;
            observedWidth=$fullHd.clientWidth;observedHeight=$fullHd.clientHeight;scale=$fullHd.renderScaling;
            fullHdObserved=($fullHd.clientWidth -eq 1920 -and $fullHd.clientHeight -eq 1080)}
        $null = Call 'run_workflow' $sizeAction
        $compact = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
        if ($compact.requestedWidth -ne 1120 -or $compact.requestedHeight -ne 800) { throw 'Size toggle did not restore compact intent.' }
        $editor = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;selector=@{automationId='qa-display-name'};maxDepth=32}
        if (@($editor.value.value.matches).Count -ne 1 -or -not $editor.value.value.coverage.complete) { throw 'Expected one complete editor selection.' }
        $textTarget = $editor.value.value.matches[0].target
        $read = Call 'edit_text' @{request=@{target=$textTarget;action='read'}}
        $edit = @{target=$textTarget;action='insert';requestId='native-text-insert';expectedRevision=$read.value.value.after.revision;
            text='X';policy=@{ownedEvidenceRoot=$run.root;allowedDesiredStates=@('text')}}
        for ($attempt=0; $attempt -lt 2; $attempt++) {
            $refused = Call 'edit_text' @{request=$edit} -ExpectedFailure
            if ($refused.value.error.code -ne 'invalid_mcp_arguments' -or $refused.value.error.details.dispatched -ne 'false' -or
                $refused.value.error.message -notlike '*start offset*') { throw 'Malformed insert did not return its structured pre-dispatch explanation.' }
        }
        $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
        if ($state.displayName -ne 'Ada' -or $state.textChanges -ne 0) { throw 'Malformed insertion changed app state.' }
        $edit.start = 1
        for ($attempt=0; $attempt -lt 2; $attempt++) {
            $changed = Call 'edit_text' @{request=$edit}
            if ($changed.value.value.after.text -ne 'AXda' -or -not $changed.value.value.verified -or
                $changed.value.value.replayed -ne ($attempt -eq 1)) { throw 'Valid insert or its exact replay failed.' }
        }
        $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
        if ($state.displayName -ne 'AXda' -or $state.textChanges -ne 1) { throw 'Independent journal did not confirm one insertion.' }
        $null = & $entry -Operation Reset -RunDirectory $run.root
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
        $null = Call 'run_workflow' @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
            @{action='select';selector=@{automationId='qa-windows-tab'}})}}
        # Check closed first: an open native popup intentionally makes the owner's Reset non-actionable.
        foreach ($popupOpen in @($false,$true)) {
            if ($popupOpen) {
                $null = Call 'run_workflow' @{request=@{sessionId=$run.sessionId;topLevelId=$run.topLevelId;steps=@(
                    @{action='invoke';selector=@{automationId='qa-open-popup'}})}}
            }
            $state = Get-Content (Join-Path $run.root 'qa-state.json') -Raw | ConvertFrom-Json
            if ($state.popupOpen -ne $popupOpen) { throw 'The app popup state does not match the query case.' }
            $flat = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;automationId='qa-close-popup';treeKind='logical';maxDepth=32}
            $structured = Call 'find_nodes' @{sessionId=$run.sessionId;topLevelId=$run.topLevelId;selector=@{automationId='qa-close-popup';treeKind='logical'};maxDepth=32}
            if (@($flat.value.value.matches).Count -ne 1 -or @($structured.value.value.matches).Count -ne 1 -or
                $flat.value.value.matches[0].node.nodeId -ne $structured.value.value.matches[0].node.nodeId -or
                -not $structured.value.value.coverage.complete) { throw 'Logical popup identity was duplicated or query coverage failed.' }
        }
        $results.Add(@{integration=$integration;backend=$run.observedBackend;scale=$run.renderScaling;status='passed';cycles=2;negativeFailurePreserved=$true;childCloseReopenVerified=$true;exactAutomationIdsVerified=$true;queryBoundsVerified=$true;selectionCoverageVerified=$true;textEditValidationVerified=$true;logicalPopupIdentityVerified=$true;applicationReadinessVerified=$true;tableRenderingVerified=$true;fullHdGeometry=$fullHdGeometry;root=$run.root})
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
