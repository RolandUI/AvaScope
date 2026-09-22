#requires -Version 7.0
param(
    [Parameter(Mandatory=$true)][ValidateSet('headless','win32','x11','macos')][string]$Backend,
    [string]$CliAssembly = 'src/AvaScope.Cli/bin/Release/net10.0/avascope.dll',
    [string]$McpAssembly = 'src/AvaScope.Mcp/bin/Release/net10.0/AvaScope.Mcp.dll',
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repoRoot ('artifacts/platform-matrix/' + $Backend + '-' + [Guid]::NewGuid().ToString('N'))
$evidenceRoot = Join-Path $root 'evidence'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cli = [IO.Path]::GetFullPath($CliAssembly)
$mcp = [IO.Path]::GetFullPath($McpAssembly)
$client = Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$provider = Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider'
$hostOutput = Join-Path $root 'host'
$previousResponseArtifacts = $env:AVASCOPE_RESPONSE_ARTIFACT_DIR
$report = [ordered]@{
    requestedBackend=$Backend; status='running'; runtime=[System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
    nativeOsInput='unsupported'; nativeScreenCapture='unsupported'; renderMode='unknown'
    extensionLanes=@{xwayland='unsupported_until_separately_validated';wayland='unsupported_until_separately_validated'}
    runs=@(); diagnostics=@()
}
function Save-Report { [IO.File]::WriteAllText((Join-Path $root 'validation.json'), ($report | ConvertTo-Json -Depth 25)) }
function Assert-Condition([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Step([string]$Id,[string]$Action,[string]$Name,[string]$Alias='main') {
    $step=@{id=$Id;action=$Action;topLevelAlias=$Alias}
    if ($Name) { $step.selector=@{name=$Name} }
    return $step
}
function Wait-Text([string]$Id,[string]$Name,[string]$Text,[string]$Alias='main') {
    $step=Step $Id 'wait_for_state' $Name $Alias
    $step.waitCondition=@{kind='text';expected=$Text;valueType='string'}
    $step.timeoutMs=5000
    return $step
}
try {
    Assert-Condition (($Backend -ne 'win32' -or $IsWindows) -and ($Backend -ne 'x11' -or $IsLinux) -and ($Backend -ne 'macos' -or $IsMacOS)) 'The requested backend is unavailable on this host.'
    & dotnet publish (Join-Path $repoRoot 'samples/AvaScope.StandaloneHost/AvaScope.StandaloneHost.csproj') -c $Configuration -o $hostOutput --artifacts-path (Join-Path $root 'build') -p:EnableUiInspection=true *> (Join-Path $root 'build.log')
    Assert-Condition ($LASTEXITCODE -eq 0) 'External fixture build failed; see build.log.'
    Assert-Condition (@(Get-ChildItem $hostOutput -Filter 'AvaScope.*.dll').Count -eq 0) 'The host output contains AvaScope assemblies.'
    foreach ($adapter in @('cli','mcp')) {
        foreach ($failure in @($false,$true)) {
            $case = "$adapter-$(if ($failure) {'failure'} else {'success'})"
            $run = Join-Path $evidenceRoot $case
            $secret = 'matrix-private-' + [Guid]::NewGuid().ToString('N')
            $arguments=@((Join-Path $hostOutput 'StandaloneInspectionHost.dll'),'--exit-after-ms=120000')
            if ($Backend -eq 'headless') { $arguments += '--headless' }
            $steps=@(
                (Step 'environment' 'inspect' 'EnvironmentState'),
                (Step 'focus' 'focus' 'NameField'),
                (Wait-Text 'focused' 'FocusState' 'NameField focused'),
                (@{id='key';action='key_down';topLevelAlias='main';selector=@{name='NameField'};key='Left'}),
                (Step 'before' 'screenshot' ''),
                (Step 'open-child' 'invoke' 'OpenWindow'),
                (Wait-Text 'child-opened' 'Status' 'Child opened'),
                (Step 'child' 'screenshot' '' 'child'),
                (Step 'close-child' 'invoke' 'CloseChild' 'child'),
                (@{id='child-closed';action='wait_for_state';topLevelAlias='child';waitCondition=@{kind='top_level_closed'}}),
                (Step 'open-modal' 'invoke' 'OpenModal'),
                (Wait-Text 'owner' 'ModalState' 'Owner verified' 'modal'),
                (Step 'modal' 'screenshot' '' 'modal'),
                (Step 'close-modal' 'invoke' 'CloseModal' 'modal'),
                (@{id='modal-unregistered';action='wait_for_state';topLevelAlias='modal';waitCondition=@{kind='top_level_closed'}}),
                (Wait-Text 'modal-closed' 'Status' 'Modal closed'),
                (Step 'open-popup' 'invoke' 'OpenPopup'),
                (Wait-Text 'popup-opened' 'Status' $(if ($Backend -eq 'headless') {'Popup overlay opened'} else {'Popup window opened'})),
                (Step 'close-popup' 'invoke' 'ClosePopup'),
                (Wait-Text 'popup-closed' 'Status' 'Popup closed'),
                (Step 'resize' 'invoke' 'Resize'),
                (Wait-Text 'resized' 'LayoutState' '580x520'),
                (Step 'after' 'screenshot' '')
            )
            if ($failure) { $steps=@((Step 'environment' 'inspect' 'EnvironmentState'),(Wait-Text 'intentional-failure' 'Status' 'Impossible expected state')); $steps[1].timeoutMs=300 }
            $request=@{
                requestId=$case;outputDirectory=$run;terminateLaunchedProcess=$true;captureVisualTree=$true;workflowTimeoutMs=30000;allowDestructive=$true
                launch=@{
                    command='dotnet';argumentList=$arguments;manifestDirectory=(Join-Path $run 'manifests');outputDirectory=(Join-Path $run 'launch');timeoutMs=20000
                    environment=@{UI_INSPECTION_PROVIDER_PATH=$provider;AVASCOPE_PROFILE_TEST_SECRET=$secret}
                }
                topLevelAliases=@(@{alias='main';selector=@{title='Standalone inspection sample'}},@{alias='child';selector=@{title='Standalone child'}},@{alias='modal';selector=@{title='Standalone modal'}})
                steps=$steps
                evidence=@{
                    captureOnFailure=$true;includeScreenshot=$true;includeVisualTree=$true;includeActiveTopLevels=$true;exportReports=$true
                    reportDirectory=(Join-Path $run 'reports');treeDepth=4;maxSelectorCandidates=4
                    policy=@{
                        ownedEvidenceRoot=$evidenceRoot;redactedText=@($secret);excludedControlAutomationIds=@('native-matrix-sensitive')
                        allowedActions=@('inspect','focus','key_down','invoke','screenshot','wait_for_state');allowDestructiveActions=$true;retentionMaxOwnedRuns=4;writeActionAudit=$true
                    }
                }
            }
            if ($Backend -eq 'x11') { $request.x11Environment=@{mode='managed';windowManager=$true;sessionBus=$true} }
            $requestPath=Join-Path $root "$case-request.json"
            [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json -Depth 20))
            $env:AVASCOPE_RESPONSE_ARTIFACT_DIR=Join-Path $run 'response-artifacts'
            if ($adapter -eq 'cli') { $json=& dotnet $cli run-scenario --request $requestPath 2> (Join-Path $root "$case.stderr.log") }
            else { $json=& dotnet $client $mcp $requestPath (Join-Path $run 'manifests') 2> (Join-Path $root "$case.stderr.log") }
            $exitCode=$LASTEXITCODE
            [IO.File]::WriteAllText((Join-Path $root "$case-result.json"), ($json -join [Environment]::NewLine))
            $result=($json -join [Environment]::NewLine | ConvertFrom-Json -Depth 64).value
            if ($result.responseBudget.artifactPath) { $result=Get-Content -Raw $result.responseBudget.artifactPath | ConvertFrom-Json -Depth 64 }
            $report.runs += @{adapter=$adapter;deliberateFailure=$failure;status=$result.status;topLevels=$result.topLevels;cleanup=$result.cleanup;environment=$result.environment}
            Save-Report
            Assert-Condition ($exitCode -eq $(if ($failure) {1} else {0})) "$case exit code $exitCode; see result JSON."
            Assert-Condition ($result.status -eq $(if ($failure) {'failed'} else {'passed'})) "$case scenario did not reach its expected result."
            Assert-Condition ($result.topLevels[0].backend.backend -eq $Backend) "$case ran a different backend."
            Assert-Condition (-not (Get-Process -Id $result.launch.processId -ErrorAction SilentlyContinue)) "$case leaked the owned app."
            Assert-Condition (@(Get-ChildItem (Join-Path $run 'manifests') -Filter '*.json' -ErrorAction SilentlyContinue).Count -eq 0) "$case leaked a session manifest."
            if ($Backend -eq 'x11') {
                Assert-Condition ($result.environment.runtimeDirectoryRemoved -and $result.environment.status -eq 'closed') "$case leaked X11 resources."
                foreach ($helper in $result.environment.helpers) { Assert-Condition ($helper.exited -and -not (Get-Process -Id $helper.processId -ErrorAction SilentlyContinue)) "$case leaked a helper." }
            }
            foreach ($file in Get-ChildItem $run -Recurse -File | Where-Object Extension -in '.json','.jsonl','.md','.log','.xml') {
                Assert-Condition (-not ([IO.File]::ReadAllText($file.FullName).Contains($secret))) "$case evidence leaked the test secret."
                Assert-Condition ($file.Length -lt 2MB) "$case emitted an unbounded evidence file."
            }
            if ($failure) {
                $failed=@($result.workflow.steps | Where-Object status -ne 'passed')[-1]
                Assert-Condition ($null -ne $failed.failureEvidence) "$case lost deliberate failure evidence."
                Assert-Condition (@(Get-ChildItem $run -Recurse -Filter '*.png').Count -gt 0) "$case lost failure screenshot."
            }
            else {
                foreach ($step in $result.workflow.steps) {
                    if ($step.input) {
                        $route=if ($step.action -eq 'invoke') {'avalonia_automation_provider'} elseif ($step.action -eq 'key_down') {'avalonia_synthetic_key'} else {'avalonia_focus_api'}
                        Assert-Condition ($step.input.provenance.route -eq $route -and $step.input.provenance.backend.backend -eq $Backend) "$case lost actual input route."
                    }
                    if ($step.screenshot) { Assert-Condition ($step.screenshot.provenance.route -eq 'avalonia_render_target_bitmap' -and $step.screenshot.provenance.backend.backend -eq $Backend) "$case lost screenshot provenance." }
                }
                $after=($result.workflow.steps | Where-Object stepId -eq 'after').screenshot
                Assert-Condition ($after.pixelWidth -eq [Math]::Ceiling(580*$after.provenance.renderScaling) -and $after.pixelHeight -eq [Math]::Ceiling(520*$after.provenance.renderScaling)) "$case screenshot does not match resized client size and actual DPI."
                $report.runs[-1].visualContext=($result.workflow.steps | Where-Object stepId -eq 'environment').inspection
                $report.runs[-1].screenshot=$after
            }
        }
    }
    $report.status='passed'
}
catch { $report.status='failed';$report.diagnostics+= $_.Exception.Message;throw }
finally { $env:AVASCOPE_RESPONSE_ARTIFACT_DIR=$previousResponseArtifacts; Save-Report }
Write-Output "Shared external-provider $Backend CLI/MCP lifecycle, modal owner, focus, popup mode, resize/DPI, provenance, redacted failure and cleanup gate passed: $root"
