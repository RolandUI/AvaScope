#requires -Version 7.4
param(
    [string]$Configuration = 'Release',
    [ValidateSet('Cli','Mcp')][string[]]$Surfaces = @('Cli','Mcp'),
    [ValidateRange(2,4)][int]$RepeatCount = 2,
    [switch]$Native,
    [switch]$SkipBuild,
    [switch]$SchemaOnly,
    [string]$OnboardingReport,
    [string]$SummaryPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repoRoot ('artifacts/agent-evaluations/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
if (-not $IsWindows) { [IO.File]::SetUnixFileMode($root, [IO.UnixFileMode]448) }
$cli = Join-Path $repoRoot "src/AvaScope.Cli/bin/$Configuration/net10.0/avascope.dll"
$mcp = Join-Path $repoRoot "src/AvaScope.Mcp/bin/$Configuration/net10.0/AvaScope.Mcp.dll"
$client = Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$recipes = (Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'docs/agent-recipes.json') | ConvertFrom-Json -AsHashtable).recipes
$events = [Collections.Generic.List[object]]::new()
$cases = [Collections.Generic.List[object]]::new()
$completed = $false
$mcpProcess = $null
$savedEnvironment = @{}
foreach ($name in @('AVASCOPE_RUN_STORE_DIR','AVASCOPE_PROFILE_TEST_SECRET','AVASCOPE_RECIPE_HEALTHY','AVASCOPE_RECIPE_BROKEN')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Expand-RecipeValue($Value, [hashtable]$Bindings) {
    if ($Value -is [Collections.IDictionary]) {
        $map = @{}
        foreach ($key in $Value.Keys) { $map[$key] = Expand-RecipeValue $Value[$key] $Bindings }
        return $map
    }
    if ($Value -is [array]) { return ,@($Value | ForEach-Object { Expand-RecipeValue $_ $Bindings }) }
    if ($Value -is [string]) {
        return [regex]::Replace($Value, '\$\{([A-Za-z][A-Za-z0-9]*)\}', {
            param($match)
            if (-not $Bindings.ContainsKey($match.Groups[1].Value)) { throw 'A recipe binding is missing.' }
            [string]$Bindings[$match.Groups[1].Value]
        })
    }
    return $Value
}

function Invoke-RecipeProcess([string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        # Tools emit one JSON line. Waiting for inherited pipe EOF can hang while an owned app stays open.
        $line = $process.StandardOutput.ReadLineAsync()
        $errorText = $process.StandardError.ReadToEndAsync()
        if (-not $line.Wait(180000) -or -not $process.WaitForExit(10000)) {
            throw 'A bounded recipe invocation timed out; inspect the owned run before retrying.'
        }
        if (-not $line.Result) {
            throw ('Recipe process returned no structured response. ' + $(if ($errorText.IsCompletedSuccessfully) { $errorText.Result } else { 'stderr is still held by a child process.' }))
        }
        return @{text=$line.Result;exitCode=$process.ExitCode}
    }
    finally { if (-not $process.HasExited) { $process.Kill() }; $process.Dispose() }
}

function Invoke-Recipe([string]$Id, [string]$Surface, [hashtable]$Bindings, [string]$CaseId) {
    $recipe = @($recipes | Where-Object id -eq $Id)
    if ($recipe.Count -ne 1) { throw "Unknown or duplicate recipe: $Id" }
    $recipe = $recipe[0]
    $arguments = Expand-RecipeValue $recipe.arguments $Bindings
    $argumentsJson = $arguments | ConvertTo-Json -Depth 60
    $schema = $schemas.tools[$recipe.tool] | ConvertTo-Json -Depth 100 -Compress
    if (-not (Test-Json -Json $argumentsJson -Schema $schema -ErrorAction SilentlyContinue)) {
        throw "Published recipe does not match the current MCP schema: $Id"
    }
    if ($SchemaOnly) { return }
    $requestPath = Join-Path $root ('request-' + [Guid]::NewGuid().ToString('N') + '.json')
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        if ($Surface -eq 'Mcp') {
            $command = @{tool=$recipe.tool;arguments=$arguments} | ConvertTo-Json -Depth 60 -Compress
            $mcpProcess.StandardInput.WriteLine($command)
            $mcpProcess.StandardInput.Flush()
            $line = $mcpProcess.StandardOutput.ReadLineAsync()
            if (-not $line.Wait(180000) -or -not $line.Result) { throw 'The persistent MCP recipe connection did not return a bounded response.' }
            $executed = @{text=$line.Result;exitCode=$(if (($line.Result | ConvertFrom-Json).success) {0} else {1})}
        }
        else {
            $cliArguments = switch ($Id) {
                'capabilities' { @('--require', $arguments.requiredCapabilities) }
                'integration-guide' { @('--project', $arguments.projectPath) }
                'verify-provider' { @('--directory', $arguments.directory) }
                'readiness' {
                    [IO.File]::WriteAllText($requestPath, ($arguments.request | ConvertTo-Json -Depth 60))
                    @('--target-request', $requestPath)
                }
                { $_ -in @('resolve-profile','launch-profile') } { @('--profile-file', $arguments.profileFile, '--profile', $arguments.profileName) }
                { $_ -in @('first-attach','session-capabilities') } { @('--session', $arguments.sessionId, '--manifest-dir', $arguments.manifestDirectory) }
                { $_ -in @('observe','multi-window') } {
                    [IO.File]::WriteAllText($requestPath, ($arguments.request | ConvertTo-Json -Depth 60))
                    @('--request', $requestPath, '--manifest-dir', $arguments.manifestDirectory)
                }
                'cleanup' { @('--run', $arguments.request.runId, '--operation', 'cleanup', '--store-dir', $arguments.storeDirectory) }
                default { throw 'No CLI mapping exists for this recipe.' }
            }
            $executed = Invoke-RecipeProcess -Arguments (@($cli, $recipe.cli) + $cliArguments)
        }
        $exitCode = $executed.exitCode
        $output = $executed.text
        $response = $output | ConvertFrom-Json -AsHashtable -Depth 100
        $timer.Stop()
        $result = if ($Id -eq 'readiness' -and $Surface -eq 'Cli') { $response.value.target } else { $response.value }
        $event = [ordered]@{
            sequence=$events.Count + 1; caseId=$CaseId; recipe=$Id; surface=$Surface; tool=$recipe.tool
            durationMs=$timer.ElapsedMilliseconds; exitCode=$exitCode; success=$response.success
            status=$result.status; failureStage=$result.failureStage; errorCode=$response.error.code
            retryDisposition='none'; mode='deterministic_conformance'
        }
        $events.Add($event)
        # Only redacted outputs are retained; binding files are always removed below.
        [IO.File]::WriteAllText((Join-Path $root ("result-$($events.Count).json")), $output.Replace($Bindings.secret, '[REDACTED]'))
        [IO.File]::AppendAllText((Join-Path $root 'tool-events.jsonl'), ($event | ConvertTo-Json -Depth 8 -Compress) + "`n")
        return @{ response=$response; value=$result; exitCode=$exitCode }
    }
    finally { if (Test-Path -LiteralPath $requestPath) { Remove-Item -LiteralPath $requestPath -Force } }
}

try {
    if (-not $SkipBuild) {
        & dotnet build (Join-Path $repoRoot 'AvaScope.slnx') -c $Configuration *> (Join-Path $root 'build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Evaluation build failed.' }
        if (-not $SchemaOnly) { & (Join-Path $PSScriptRoot 'package-provider.ps1') -Configuration $Configuration *> (Join-Path $root 'provider.log') }
    }
    $empty = Join-Path $root 'schema-request.json'
    [IO.File]::WriteAllText($empty, '{}')
    $schemaJson = (& dotnet $client $mcp $empty $root --schemas | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'The documented stdio server configuration did not initialize.' }
    $schemas = $schemaJson | ConvertFrom-Json -AsHashtable -Depth 100
    [IO.File]::WriteAllText((Join-Path $root 'schemas.json'), $schemaJson)
    [IO.File]::WriteAllText((Join-Path $root 'mcp-server.json'), (@{ command='dotnet'; args=@($mcp) } | ConvertTo-Json))
    $bindings = @{
        projectPath=(Join-Path $root 'InspectionHost.csproj'); providerDirectory=(Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider')
        assemblyPath=(Join-Path $root 'InspectionHost.dll'); backend='headless'; profileFile=(Join-Path $root 'profile.json')
        sessionId='schema-session'; manifestDirectory=(Join-Path $root 'manifests'); evidenceRoot=(Join-Path $root 'evidence')
        observationDirectory=(Join-Path $root 'evidence/observe'); workflowDirectory=(Join-Path $root 'evidence/workflow')
        secret=('recipe-private-' + [Guid]::NewGuid().ToString('N')); runId='schema-run'; storeDirectory=(Join-Path $root 'run-store')
    }
    if ($Native) { $bindings.backend = if ($IsWindows) {'win32'} elseif ($IsMacOS) {'macos'} else {'x11'} }
    # Check all published examples without dispatch, even when a later environment preflight fails.
    $schemaFlag = $SchemaOnly
    $SchemaOnly = $true
    foreach ($recipe in $recipes) { Invoke-Recipe $recipe.id 'Mcp' $bindings 'schema' }
    $SchemaOnly = $schemaFlag
    if ($SchemaOnly) { Write-Output "All $($recipes.Count) published recipes match live MCP schemas: $root"; return }

    $env:AVASCOPE_RUN_STORE_DIR = $bindings.storeDirectory
    if (-not $OnboardingReport) {
        $OnboardingReport = Join-Path $root 'onboarding.json'
        & (Join-Path $PSScriptRoot 'test-integration-onboarding.ps1') -Configuration $Configuration -SkipBuild -Native:$Native -SummaryPath $OnboardingReport *> (Join-Path $root 'onboarding.log')
    }
    $onboarding = Get-Content -Raw -LiteralPath $OnboardingReport | ConvertFrom-Json -AsHashtable
    if (-not $onboarding.success -or [bool]$onboarding.native -ne $Native.IsPresent) { throw 'Onboarding evidence must pass on the selected backend.' }
    $bindings.projectPath = Join-Path $onboarding.root 'standalone/InspectionHost.csproj'
    $bindings.assemblyPath = Join-Path $onboarding.root 'standalone/output-enabled/InspectionHost.dll'
    $env:AVASCOPE_PROFILE_TEST_SECRET = $bindings.secret
    # A persistent MCP server inherits environment once. Select an explicit reference per trial.
    $env:AVASCOPE_RECIPE_HEALTHY = '0'
    $env:AVASCOPE_RECIPE_BROKEN = '1'
    $cases.Add(@{caseId='clean-project'; completed=$true; observedClassification='none'; expectedClassification='none'; toolCalls=$onboarding.toolCalls; report=$OnboardingReport})

    foreach ($surface in $Surfaces) {
        if ($surface -eq 'Mcp') {
            $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.RedirectStandardInput = $true
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            foreach ($argument in @($client,$mcp,$empty,$bindings.manifestDirectory,'--stdio-session')) { $start.ArgumentList.Add($argument) }
            $mcpProcess = [Diagnostics.Process]::Start($start)
            $mcpErrors = $mcpProcess.StandardError.ReadToEndAsync()
        }
        foreach ($id in @('capabilities','integration-guide','verify-provider','readiness')) {
            $result = Invoke-Recipe $id $surface $bindings "$surface-preflight"
            if (-not $result.response.success -or ($id -eq 'readiness' -and $result.value.status -ne 'available')) { throw "Recipe preflight failed: $id" }
        }
        for ($repeat = 1; $repeat -le $RepeatCount; $repeat++) {
            foreach ($seed in @('clean','environment','application')) {
                $caseId = "$surface-$seed-$repeat"
                $startCount = $events.Count
                $caseRoot = Join-Path $bindings.evidenceRoot $caseId
                $bindings.observationDirectory = Join-Path $caseRoot 'observe'
                $bindings.workflowDirectory = Join-Path $caseRoot 'workflow'
                $bindings.profileFile = Join-Path $root "$caseId-profile.json"
                $launchArgs = @($bindings.assemblyPath)
                if (-not $Native) { $launchArgs += '--headless' }
                $environmentReferences = @{
                    AVASCOPE_PROFILE_TEST_SECRET=@{name='AVASCOPE_PROFILE_TEST_SECRET'}
                    AVASCOPE_RECIPE_BROKEN_APP=@{name=$(if ($seed -eq 'application') {'AVASCOPE_RECIPE_BROKEN'} else {'AVASCOPE_RECIPE_HEALTHY'});secret=$false}
                }
                foreach ($name in @('DISPLAY','XAUTHORITY','WAYLAND_DISPLAY','XDG_RUNTIME_DIR','DBUS_SESSION_BUS_ADDRESS','LANG','LC_ALL')) {
                    if ([Environment]::GetEnvironmentVariable($name)) { $environmentReferences[$name] = @{name=$name;secret=$false} }
                }
                $command = if ($seed -eq 'environment') { Join-Path $root 'nonexistent-dotnet' } else { 'dotnet' }
                $profile = @{schemaVersion=1;profiles=@{inspect=@{
                    provider=@{directory=$bindings.providerDirectory};environmentReferences=@{launch=$environmentReferences}
                    scenario=@{
                        launch=@{command=$command;argumentList=$launchArgs;timeoutMs=15000;manifestDirectory=$bindings.manifestDirectory}
                        outputDirectory=(Join-Path $caseRoot 'launch-{runId}');terminateLaunchedProcess=$false
                        steps=@(@{action='assert_state';selector=@{name='SafeField'};assertProperty='Text';expected='Inspection ready'})
                    }
                }}}
                [IO.File]::WriteAllText($bindings.profileFile, ($profile | ConvertTo-Json -Depth 30))
                $resolved = Invoke-Recipe 'resolve-profile' $surface $bindings $caseId
                if (-not $resolved.response.success) { throw 'Profile recipe did not resolve.' }
                $launched = Invoke-Recipe 'launch-profile' $surface $bindings $caseId
                $bindings.runId = $launched.value.runId
                try {
                    if ($seed -eq 'environment') {
                        if ($launched.response.success -or $launched.value.failureStage -ne 'launch' -or $launched.value.workflow) { throw 'The environment seed was not identified before application work.' }
                        $classification = 'environment'
                    }
                    else {
                        if (-not $launched.response.success) { throw "A clean launch failed: $caseId" }
                        $bindings.sessionId = $launched.value.sessionId
                        foreach ($id in @('first-attach','session-capabilities','observe')) {
                            $observed = Invoke-Recipe $id $surface $bindings $caseId
                            if (-not $observed.response.success) { throw "The live recipe failed: $id" }
                        }
                        $flow = Invoke-Recipe 'multi-window' $surface $bindings $caseId
                        if ($seed -eq 'clean') {
                            if ($flow.value.status -ne 'passed') { throw 'The clean multi-window flow did not pass.' }
                            $classification = 'none'
                        }
                        else {
                            $last = $flow.value.steps[-1]
                            if ($flow.value.status -ne 'failed' -or $last.verification.observation.value -ne 'Broken') { throw 'The application seed was not identified by its observed postcondition.' }
                            $classification = 'application'
                        }
                        if (-not $flow.value.reportPack -or -not (Test-Path -LiteralPath $flow.value.reportPack.reportDirectory)) { throw 'Workflow evidence was not retained.' }
                    }
                }
                finally {
                    if ($bindings.runId) {
                        $cleaned = Invoke-Recipe 'cleanup' $surface $bindings $caseId
                        if (-not $cleaned.response.success -or $cleaned.value.state -ne 'cleaned') { throw "Owned evaluation cleanup failed: $caseId" }
                    }
                }
                $expected = if ($seed -eq 'clean') {'none'} else {$seed}
                $cases.Add(@{caseId=$caseId;completed=($classification -eq $expected);expectedClassification=$expected;observedClassification=$classification;toolCalls=$events.Count-$startCount;unnecessaryRetries=0})
            }
        }
        if ($mcpProcess) {
            $mcpProcess.StandardInput.Close()
            if (-not $mcpProcess.WaitForExit(10000)) { $mcpProcess.Kill() }
            $mcpProcess.Dispose()
            $mcpProcess = $null
        }
    }
    $summary = @{
        schemaVersion=1;mode='deterministic_conformance';completed=$true;root=$root;server=$schemas.server
        platform=[Runtime.InteropServices.RuntimeInformation]::OSDescription;backend=$bindings.backend;repeatCount=$RepeatCount
        recipesSha256=(Get-FileHash -LiteralPath (Join-Path $repoRoot 'docs/agent-recipes.json') -Algorithm SHA256).Hash.ToLowerInvariant()
        client='AvaScope recipe harness';model=$null;agentOutcomesMeasured=$false
        toolCalls=$events.Count+$onboarding.toolCalls;unnecessaryRetries=0;cases=$cases.ToArray();events=$events.ToArray()
    } | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText((Join-Path $root 'evaluation.json'), $summary)
    if ($SummaryPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($SummaryPath), $summary) }
    $completed = $true
    Write-Output "Recipe schemas, clean-project onboarding and repeated clean/environment/application cases passed: $root"
}
finally {
    if ($mcpProcess) {
        $mcpProcess.StandardInput.Close()
        if (-not $mcpProcess.WaitForExit(10000)) { $mcpProcess.Kill() }
        $mcpProcess.Dispose()
    }
    if (-not $completed -and -not $SchemaOnly -and $bindings -and (Test-Path -LiteralPath $bindings.storeDirectory)) {
        $recovery = @()
        try {
            $listed = Invoke-RecipeProcess -Arguments @($cli, 'list-agent-runs', '--store-dir', $bindings.storeDirectory, '--max-results', '100')
            $runs = ($listed.text | ConvertFrom-Json -AsHashtable).value
            foreach ($run in $runs | Where-Object { -not $_.active }) {
                $cleaned = Invoke-RecipeProcess -Arguments @($cli, 'recover-run', '--run', $run.runId, '--operation', 'cleanup', '--store-dir', $bindings.storeDirectory)
                $response = $cleaned.text | ConvertFrom-Json -AsHashtable
                $recovery += @{runId=$run.runId;success=$response.success;state=$response.value.state;errorCode=$response.error.code}
            }
        }
        catch { $recovery += @{success=$false;state='manual_recovery_required'} }
        [IO.File]::WriteAllText((Join-Path $root 'evaluation-partial.json'), (@{
            schemaVersion=1;mode='deterministic_conformance';completed=$false;cases=$cases.ToArray();events=$events.ToArray();recovery=$recovery
        } | ConvertTo-Json -Depth 20))
    }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
