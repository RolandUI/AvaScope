param(
    [Parameter(Mandatory = $true)]
    [string]$CliAssembly,
    [ValidateSet("Cli", "Mcp")]
    [string]$Surface = "Cli",
    [string]$McpAssembly,
    [string]$McpScenarioClientAssembly,
    [string]$Configuration = "Release",
    [ValidateRange(2, 8)]
    [int]$RepeatCount = 2
)

$ErrorActionPreference = "Stop"

function Assert-WorkflowCondition {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-WorkflowScenario {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Request,
        [Parameter(Mandatory = $true)]
        [string]$RequestPath,
        [Parameter(Mandatory = $true)]
        [bool]$ExpectSuccess
    )

    $requestJson = $Request | ConvertTo-Json -Depth 24
    Assert-WorkflowCondition (-not [regex]::IsMatch($requestJson, '"(?:x|y|nodeId|topLevelId|waitMs)"\s*:')) `
        "The complex workflow request must not persist coordinates, runtime ids, or fixed sleeps."
    $requestJson | Set-Content -LiteralPath $RequestPath -Encoding UTF8

    if ($Surface -eq "Mcp") {
        $manifestDirectory = $Request.launch.manifestDirectory
        $output = (& dotnet $fullMcpScenarioClientAssembly $fullMcpAssembly $RequestPath $manifestDirectory | Out-String).Trim()
    }
    else {
        $output = (& dotnet $fullCliAssembly run-scenario --request $RequestPath | Out-String).Trim()
    }
    $exitCode = $LASTEXITCODE
    $response = $output | ConvertFrom-Json -Depth 32
    if ($ExpectSuccess) {
        Assert-WorkflowCondition ($exitCode -eq 0) "The complex workflow scenario failed with exit code $exitCode. Output: $output"
        Assert-WorkflowCondition ($response.success -and $response.value.status -eq "passed") "The complex workflow scenario did not pass. Output: $output"
    }
    else {
        Assert-WorkflowCondition ($exitCode -ne 0) "The intentional failure scenario unexpectedly returned success. Output: $output"
        Assert-WorkflowCondition ((-not $response.success) -and $response.value.status -eq "failed") "The intentional failure did not preserve a failed scenario result. Output: $output"
    }

    return $response
}

function New-Policy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Secret
    )

    return [ordered]@{
        ownedEvidenceRoot = $evidenceRoot
        redactedText = @($Secret)
        redactedAutomationIds = @("workflow-sensitive-token")
        excludedControlAutomationIds = @("workflow-sensitive-token")
        allowedActions = @(
            "drag",
            "invoke",
            "custom_actions",
            "custom_action",
            "inspect",
            "assert_state",
            "screenshot",
            "wait_for_node",
            "wait_for_state",
            "if",
            "retry_until",
            "use_fragment"
        )
        allowedCustomActions = @("workflow.commit")
        allowGestures = $true
        allowDestructiveActions = $true
        retentionMaxOwnedRuns = 1
        writeActionAudit = $true
        networkUpload = $false
    }
}

function New-Launch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RunDirectory,
        [Parameter(Mandatory = $true)]
        [string]$Secret,
        [Parameter(Mandatory = $true)]
        [bool]$OptionalUi
    )

    return [ordered]@{
        projectPath = $projectPath
        configuration = $Configuration
        framework = "net10.0"
        noBuild = $true
        manifestDirectory = (Join-Path $RunDirectory "manifests")
        outputDirectory = (Join-Path $RunDirectory "launch")
        environment = [ordered]@{
            AVASCOPE_COMPLEX_OPTIONAL = $(if ($OptionalUi) { "1" } else { "0" })
            AVASCOPE_COMPLEX_SECRET = $Secret
        }
        timeoutMs = 15000
    }
}

function New-Evidence {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RunDirectory,
        [Parameter(Mandatory = $true)]
        [string]$Secret
    )

    return [ordered]@{
        captureOnFailure = $true
        includeScreenshot = $true
        includeVisualTree = $true
        includeActiveTopLevels = $true
        includeSelectorCandidates = $true
        exportReports = $true
        reportDirectory = (Join-Path $RunDirectory "reports")
        treeDepth = 5
        maxSelectorCandidates = 8
        policy = New-Policy -Secret $Secret
    }
}

function New-SuccessRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RunDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RequestId,
        [Parameter(Mandatory = $true)]
        [string]$Secret,
        [Parameter(Mandatory = $true)]
        [bool]$OptionalUi
    )

    $note = "release-$RequestId"
    return [ordered]@{
        requestId = $RequestId
        build = [ordered]@{
            projectPath = $projectPath
            configuration = $Configuration
            framework = "net10.0"
            noRestore = $true
            arguments = @("--nologo")
            timeoutMs = 120000
        }
        launch = New-Launch -RunDirectory $RunDirectory -Secret $Secret -OptionalUi $OptionalUi
        outputDirectory = $RunDirectory
        timelinePath = (Join-Path $RunDirectory "timeline.md")
        captureAfterEachStep = $true
        allowDestructive = $true
        isolateState = $true
        isolatedStateDirectory = (Join-Path $RunDirectory "state")
        terminateLaunchedProcess = $true
        workflowTimeoutMs = 30000
        maxDepth = 16
        topLevelAliases = @(
            [ordered]@{ alias = "main"; selector = [ordered]@{ title = "AvaScope Complex Workflow"; kind = "Window" } },
            [ordered]@{ alias = "details"; selector = [ordered]@{ title = "AvaScope Complex Details"; kind = "Window" } }
        )
        variables = [ordered]@{ note = $note }
        fragments = @(
            [ordered]@{
                name = "standard-gesture"
                parameters = @()
                steps = @(
                    [ordered]@{
                        id = "drag-standard"
                        action = "drag"
                        topLevelAlias = "main"
                        selector = [ordered]@{ automationId = "workflow-standard-slider"; actionable = $true }
                        direction = "end"
                        durationMs = 80
                    },
                    [ordered]@{
                        id = "wait-standard-value"
                        action = "wait_for_state"
                        topLevelAlias = "main"
                        selector = [ordered]@{ automationId = "workflow-standard-slider"; rendered = $true }
                        waitCondition = [ordered]@{ kind = "value"; expected = "100"; comparison = "equals"; valueType = "number" }
                        timeoutMs = 3000
                        pollIntervalMs = 25
                    }
                )
            },
            [ordered]@{
                name = "assert-details"
                parameters = @("expected")
                steps = @(
                    [ordered]@{
                        id = "assert-details-text"
                        action = "assert_state"
                        topLevelAlias = "details"
                        selector = [ordered]@{ automationId = "workflow-details-status"; rendered = $true }
                        assertProperty = "Text"
                        expected = '${expected}'
                    }
                )
            }
        )
        steps = @(
            [ordered]@{
                id = "wait-main-ready"
                action = "wait_for_node"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-main-status"; rendered = $true }
                waitCondition = [ordered]@{ kind = "exists" }
                timeoutMs = 5000
                pollIntervalMs = 25
            },
            [ordered]@{ id = "run-standard-gesture"; action = "use_fragment"; fragment = "standard-gesture" },
            [ordered]@{
                id = "drag-custom-control"
                action = "drag"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-drag-source"; rendered = $true }
                destinationSelector = [ordered]@{ automationId = "workflow-drop-target"; rendered = $true }
                durationMs = 80
            },
            [ordered]@{
                id = "retry-async-drag-state"
                action = "retry_until"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-main-status" }
                waitCondition = [ordered]@{ kind = "text"; expected = "Card delivered" }
                maxAttempts = 8
                retryDelayMs = 25
                steps = @(
                    [ordered]@{
                        id = "inspect-drag-state"
                        action = "inspect"
                        topLevelAlias = "main"
                        selector = [ordered]@{ automationId = "workflow-main-status" }
                    }
                )
            },
            [ordered]@{
                id = "optional-ui-branch"
                action = "if"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-optional-state" }
                waitCondition = [ordered]@{ kind = "text"; expected = "available" }
                then = @(
                    [ordered]@{
                        id = "invoke-optional-ui"
                        action = "invoke"
                        topLevelAlias = "main"
                        selector = [ordered]@{ automationId = "workflow-optional-action"; actionable = $true }
                    }
                )
                else = @(
                    [ordered]@{
                        id = "inspect-required-ui"
                        action = "inspect"
                        topLevelAlias = "main"
                        selector = [ordered]@{ automationId = "workflow-main-status" }
                    }
                )
            },
            [ordered]@{
                id = "discover-application-action"
                action = "custom_actions"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-action-target"; actionable = $true }
            },
            [ordered]@{
                id = "commit-application-action"
                action = "custom_action"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-action-target"; actionable = $true }
                customActionName = "workflow.commit"
                customActionParameters = [ordered]@{ note = '${note}' }
                verify = [ordered]@{
                    condition = [ordered]@{ kind = "text"; expected = 'Commit ready: ${note}' }
                    selector = [ordered]@{ automationId = "workflow-details-status"; rendered = $true }
                    topLevelAlias = "details"
                    timeoutMs = 5000
                    pollIntervalMs = 25
                    captureBefore = $true
                    captureAfter = $true
                    captureScreenshots = $true
                }
            },
            [ordered]@{
                id = "finalize-workflow"
                action = "invoke"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-finalize"; actionable = $true }
                verify = [ordered]@{
                    condition = [ordered]@{ kind = "text"; expected = 'Workflow complete: ${note}' }
                    selector = [ordered]@{ automationId = "workflow-details-status"; rendered = $true }
                    topLevelAlias = "details"
                    timeoutMs = 5000
                    pollIntervalMs = 25
                    captureBefore = $true
                    captureAfter = $true
                }
            },
            [ordered]@{
                id = "verify-final-fragment"
                action = "use_fragment"
                fragment = "assert-details"
                arguments = [ordered]@{ expected = 'Workflow complete: ${note}' }
            },
            [ordered]@{
                id = "capture-final"
                action = "screenshot"
                topLevelAlias = "main"
                screenshotPath = (Join-Path $RunDirectory "final.png")
            }
        )
        evidence = New-Evidence -RunDirectory $RunDirectory -Secret $Secret
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fullCliAssembly = [System.IO.Path]::GetFullPath($CliAssembly)
if (-not (Test-Path -LiteralPath $fullCliAssembly -PathType Leaf)) {
    throw "AvaScope CLI assembly was not found: $fullCliAssembly"
}
if ($Surface -eq "Mcp") {
    if ([string]::IsNullOrWhiteSpace($McpAssembly) -or [string]::IsNullOrWhiteSpace($McpScenarioClientAssembly)) {
        throw "MCP validation requires McpAssembly and McpScenarioClientAssembly."
    }

    $fullMcpAssembly = [System.IO.Path]::GetFullPath($McpAssembly)
    $fullMcpScenarioClientAssembly = [System.IO.Path]::GetFullPath($McpScenarioClientAssembly)
    if (-not (Test-Path -LiteralPath $fullMcpAssembly -PathType Leaf)) {
        throw "AvaScope MCP assembly was not found: $fullMcpAssembly"
    }

    if (-not (Test-Path -LiteralPath $fullMcpScenarioClientAssembly -PathType Leaf)) {
        throw "AvaScope MCP scenario client was not found: $fullMcpScenarioClientAssembly"
    }
}

$projectPath = Join-Path $repositoryRoot "samples/AvaScope.ComplexWorkflowApp/AvaScope.ComplexWorkflowApp.csproj"
$smokeRoot = Join-Path $repositoryRoot ("artifacts/complex-workflow-smoke-" + [Guid]::NewGuid().ToString("N"))
$evidenceRoot = Join-Path $smokeRoot "owned-evidence"
$controlRoot = Join-Path $smokeRoot "control"
$unownedRoot = Join-Path $evidenceRoot "unowned-sentinel"
$unownedSentinel = Join-Path $unownedRoot "keep.txt"
$outsideSentinel = Join-Path $controlRoot "outside.txt"
New-Item -ItemType Directory -Path $controlRoot -Force | Out-Null
New-Item -ItemType Directory -Path $unownedRoot -Force | Out-Null
Set-Content -LiteralPath $unownedSentinel -Value "unrelated" -Encoding UTF8
Set-Content -LiteralPath $outsideSentinel -Value "unrelated" -Encoding UTF8
$callingProcessId = $PID
$successfulRunDirectories = @()

try {
    for ($index = 1; $index -le $RepeatCount; $index++) {
        $requestId = "complex-success-$index-$([Guid]::NewGuid().ToString('N'))"
        $runDirectory = Join-Path $evidenceRoot $requestId
        $requestPath = Join-Path $controlRoot "$requestId.json"
        $secret = "complex-secret-$index-$([Guid]::NewGuid().ToString('N'))"
        $request = New-SuccessRequest `
            -RunDirectory $runDirectory `
            -RequestId $requestId `
            -Secret $secret `
            -OptionalUi (($index % 2) -eq 1)
        $response = Invoke-WorkflowScenario -Request $request -RequestPath $requestPath -ExpectSuccess $true
        $successfulRunDirectories += $runDirectory

        Assert-WorkflowCondition ($response.value.build.status -eq "passed") "The scenario build stage did not pass."
        Assert-WorkflowCondition ($response.value.readiness.status -eq "ready") "The Bridge readiness stage did not pass."
        Assert-WorkflowCondition ($response.value.workflow.status -eq "passed") "The semantic workflow did not pass."
        Assert-WorkflowCondition ($response.value.cleanup.outcome -eq "terminated") "The owned application process was not terminated."
        Assert-WorkflowCondition ($response.value.workflow.reportPack.status -eq "passed") "The success reports do not agree with workflow status."
        Assert-WorkflowCondition (Test-Path -LiteralPath $response.value.timelinePath -PathType Leaf) "The scenario timeline was not created."
        Assert-WorkflowCondition (Test-Path -LiteralPath (Join-Path $runDirectory "action-audit.jsonl") -PathType Leaf) "The policy action audit was not created."
        Assert-WorkflowCondition (Test-Path -LiteralPath (Join-Path $runDirectory "final.png") -PathType Leaf) "The final screenshot was not created."

        $reportPath = Join-Path $runDirectory "reports/workflow-report.json"
        Assert-WorkflowCondition (Test-Path -LiteralPath $reportPath -PathType Leaf) "The machine-readable success report was not created."
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -Depth 32
        $steps = @($report.workflow.steps)
        Assert-WorkflowCondition ($steps.action -contains "drag") "The workflow did not execute gesture steps."
        Assert-WorkflowCondition ($steps.action -contains "custom_action") "The workflow did not execute the application-defined action."
        Assert-WorkflowCondition ($steps.action -contains "retry_until") "The workflow did not execute the bounded retry."
        if (($index % 2) -eq 1) {
            Assert-WorkflowCondition ($steps.stepId -contains "invoke-optional-ui") "The present optional UI did not execute its conditional branch."
        }
        else {
            Assert-WorkflowCondition ($steps.stepId -contains "inspect-required-ui") "The absent optional UI did not execute the fallback branch."
        }
        Assert-WorkflowCondition ($steps.topLevelAlias -contains "main") "The workflow did not preserve the main alias."
        Assert-WorkflowCondition ($steps.topLevelAlias -contains "details") "The workflow did not preserve the details alias."
        Assert-WorkflowCondition (@($steps.resolvedTopLevelId | Sort-Object -Unique).Count -ge 2) "The workflow did not resolve both registered windows."
        $gestureModes = @($steps | ForEach-Object { $_.input.gesture.executionMode } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        Assert-WorkflowCondition ($gestureModes -contains "automation_provider") "The standard drag did not use the range automation provider."
        Assert-WorkflowCondition ($gestureModes -contains "pointer_fallback") "The custom control drag did not use bounds-derived pointer input."
        Assert-WorkflowCondition (-not (($response | ConvertTo-Json -Depth 32).Contains($secret, [System.StringComparison]::Ordinal))) "The success response leaked the configured secret."
    }

    $failureId = "complex-failure-$([Guid]::NewGuid().ToString('N'))"
    $failureDirectory = Join-Path $evidenceRoot $failureId
    $failureRequestPath = Join-Path $controlRoot "$failureId.json"
    $failureSecret = "failure-secret-$([Guid]::NewGuid().ToString('N'))"
    $failureRequest = [ordered]@{
        requestId = $failureId
        build = [ordered]@{
            projectPath = $projectPath
            configuration = $Configuration
            framework = "net10.0"
            noRestore = $true
            arguments = @("--nologo")
            timeoutMs = 120000
        }
        launch = New-Launch -RunDirectory $failureDirectory -Secret $failureSecret -OptionalUi $false
        outputDirectory = $failureDirectory
        timelinePath = (Join-Path $failureDirectory "timeline.md")
        captureAfterEachStep = $false
        terminateLaunchedProcess = $true
        topLevelAliases = @(
            [ordered]@{ alias = "main"; selector = [ordered]@{ title = "AvaScope Complex Workflow"; kind = "Window" } }
        )
        steps = @(
            [ordered]@{
                id = "intentional-redacted-failure"
                action = "assert_state"
                topLevelAlias = "main"
                selector = [ordered]@{ automationId = "workflow-sensitive-token"; rendered = $true }
                assertProperty = "Text"
                expected = "This assertion must fail"
            }
        )
        evidence = New-Evidence -RunDirectory $failureDirectory -Secret $failureSecret
    }
    $failureResponse = Invoke-WorkflowScenario `
        -Request $failureRequest `
        -RequestPath $failureRequestPath `
        -ExpectSuccess $false

    Assert-WorkflowCondition ($failureResponse.value.failureStage -eq "workflow") "The intentional failure occurred outside the workflow stage."
    Assert-WorkflowCondition ($failureResponse.value.cleanup.outcome -eq "terminated") "The failed scenario did not terminate its owned process."
    Assert-WorkflowCondition ($failureResponse.value.workflow.reportPack.status -eq "failed") "The failure reports do not agree with workflow status."
    $failureReportPath = Join-Path $failureDirectory "reports/workflow-report.json"
    Assert-WorkflowCondition (Test-Path -LiteralPath $failureReportPath -PathType Leaf) "The machine-readable failure report was not created."
    $failureArtifacts = @(Get-ChildItem -LiteralPath (Join-Path $failureDirectory "failures") -Recurse -File)
    Assert-WorkflowCondition ($failureArtifacts.Name -contains "failure-screenshot.png") "The redacted failure screenshot was not created."
    foreach ($evidenceName in @("inspection.json", "visual-tree.json", "selector-candidates.json", "active-top-levels.json", "workflow-context.json")) {
        Assert-WorkflowCondition ($failureArtifacts.Name -contains $evidenceName) "Missing bounded failure evidence: $evidenceName"
    }
    foreach ($reportName in @("workflow-report.json", "workflow-report.md", "workflow-junit.xml")) {
        Assert-WorkflowCondition (Test-Path -LiteralPath (Join-Path $failureDirectory "reports/$reportName") -PathType Leaf) "Missing failure report: $reportName"
    }

    $textArtifacts = @(Get-ChildItem -LiteralPath $failureDirectory -Recurse -File |
        Where-Object { $_.Extension -in @(".json", ".jsonl", ".md", ".xml", ".txt", ".log") })
    $leakedArtifacts = @($textArtifacts | Where-Object {
        $content = Get-Content -LiteralPath $_.FullName -Raw
        $null -ne $content -and $content.Contains($failureSecret, [System.StringComparison]::Ordinal)
    })
    $persistedText = $textArtifacts | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw } | Out-String
    Assert-WorkflowCondition ($leakedArtifacts.Count -eq 0) "Failure evidence leaked the configured secret in: $($leakedArtifacts.FullName -join ', ')"
    Assert-WorkflowCondition ($persistedText.Contains("[REDACTED]", [System.StringComparison]::Ordinal) -or $persistedText.Contains("[EXCLUDED]", [System.StringComparison]::Ordinal)) "Failure evidence did not retain an explicit redaction marker."

    Assert-WorkflowCondition (Test-Path -LiteralPath $unownedSentinel -PathType Leaf) "Retention deleted an unrelated directory inside the evidence root."
    Assert-WorkflowCondition (Test-Path -LiteralPath $outsideSentinel -PathType Leaf) "Scenario cleanup deleted an unrelated external file."
    Assert-WorkflowCondition ($null -ne (Get-Process -Id $callingProcessId -ErrorAction SilentlyContinue)) "Scenario cleanup terminated the calling process."
    Assert-WorkflowCondition (@($successfulRunDirectories | Where-Object { Test-Path -LiteralPath $_ }).Count -eq 0) "Owned retention did not prune earlier successful runs."

    Write-Host "Complex $Surface workflow passed $RepeatCount repeat run(s), the redacted failure path, retention, and exact owned cleanup."
}
finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
}
