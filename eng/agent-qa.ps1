#requires -Version 7.4
param(
    [ValidateSet('Start','Status','Schemas','Call','Reset','Stop')][string]$Operation = 'Start',
    [ValidateSet('Direct','Standalone')][string]$Integration = 'Direct',
    [ValidateSet('Native','Headless')][string]$Backend = 'Native',
    [string]$RunDirectory,
    [string]$Configuration = 'Release',
    [string]$HostDirectory,
    [string]$ProviderDirectory,
    [ValidateRange(30,14400)][int]$LifetimeSeconds = 3600,
    [switch]$AuthorizeScreenCapture,
    [switch]$SkipBuild,
    [string]$Tool,
    [string]$ArgumentsPath,
    [switch]$AllowFailure
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$labRoot = Join-Path $repoRoot 'artifacts/agent-qa'
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $RunDirectory) {
    if ($Operation -ne 'Start') { throw 'Select an explicit -RunDirectory returned by Start.' }
    $RunDirectory = Join-Path $labRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
}
$root = [IO.Path]::GetFullPath($RunDirectory)
if (-not $root.StartsWith($labRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) {
    throw 'Agent QA run directories must stay beneath this repository artifacts/agent-qa directory.'
}
for ($ancestor = $root; $ancestor.Length -ge $labRoot.Length; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'QA run directory ancestors cannot be symbolic links.'
    }
}
$recordPath = Join-Path $root 'qa-run.json'

function Write-Json([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
}

function Invoke-Dotnet([string[]]$Arguments, [string]$LogName, [int]$TimeoutSeconds = 180) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = $repoRoot
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['AVASCOPE_RUN_STORE_DIR'] = Join-Path $root 'run-store'
    $start.Environment['AVASCOPE_RESPONSE_ARTIFACT_DIR'] = Join-Path $root 'evidence'
    $stdoutPath = Join-Path $root ($LogName + '.stdout.log')
    $stderrPath = Join-Path $root ($LogName + '.stderr.log')
    $outputFile = [IO.File]::Create($stdoutPath)
    $errorFile = [IO.File]::Create($stderrPath)
    $capture = [Threading.CancellationTokenSource]::new()
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.BaseStream.CopyToAsync($outputFile, $capture.Token)
    $stderr = $process.StandardError.BaseStream.CopyToAsync($errorFile, $capture.Token)
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw "QA command timed out: $LogName"
        }
        # A retained desktop child can inherit a pipe handle after the CLI exits.
        # Copy incrementally so evidence is available without waiting for child expiry.
        $complete = [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($stdout,$stderr))
        if (-not $complete.Wait(1000)) {
            $capture.Cancel()
            try { $complete.GetAwaiter().GetResult() } catch [OperationCanceledException] { }
        }
        $outputFile.Flush()
        $errorFile.Flush()
        $outputFile.Dispose()
        $errorFile.Dispose()
        return @{ exitCode = $process.ExitCode; text = [IO.File]::ReadAllText($stdoutPath) }
    }
    finally { $capture.Cancel(); $process.Dispose(); $outputFile.Dispose(); $errorFile.Dispose(); $capture.Dispose() }
}

function Copy-Snapshot([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Missing built directory: $Source" }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $Destination -Recurse -Force
}

function Call-Tool([string]$Name, [System.Collections.IDictionary]$Arguments) {
    $schema = $script:schemas.tools[$Name]
    if (-not $schema) { throw "Tool is absent from the selected MCP server: $Name" }
    if ($schema.properties.Contains('manifestDirectory')) { $Arguments['manifestDirectory'] = $script:run.manifestDirectory }
    $id = [DateTime]::UtcNow.ToString('HHmmssfff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $prefix = Join-Path 'calls' $id
    $requestFile = Join-Path $root ($prefix + '.request.json')
    Write-Json $requestFile $Arguments
    $before = Join-Path $root 'qa-state.json'
    if (Test-Path -LiteralPath $before) { Copy-Item -LiteralPath $before -Destination (Join-Path $root ($prefix + '.before.json')) }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $harnessError = $null
    try { $executed = Invoke-Dotnet @($script:run.clientAssembly, $script:run.mcpAssembly, $requestFile, $script:run.manifestDirectory, $Name, '--full-result') $prefix }
    catch { $harnessError = $_.Exception.Message; $executed = @{exitCode=-1;text=''} }
    $timer.Stop()
    $envelope = $null
    if ($executed.text.Trim()) {
        try { $envelope = $executed.text | ConvertFrom-Json -AsHashtable -Depth 100 }
        catch { $harnessError = 'The client response was not JSON; inspect the retained stdout/stderr.' }
    }
    $value = $envelope.structuredContent
    $passed = $executed.exitCode -eq 0 -and $envelope.isError -ne $true -and $value.success -eq $true
    $event = [ordered]@{
        schemaVersion=1; tool=$Name; timestamp=[DateTimeOffset]::UtcNow.ToString('O'); durationMs=$timer.ElapsedMilliseconds
        exitCode=$executed.exitCode; transportError=$envelope.isError; success=$passed
        harnessError=$harnessError
        requestPath=$requestFile; responsePath=(Join-Path $root ($prefix + '.stdout.log'))
        stderrPath=(Join-Path $root ($prefix + '.stderr.log')); mode='individual_public_mcp_call'
    }
    [IO.File]::AppendAllText((Join-Path $root 'calls.jsonl'), ($event | ConvertTo-Json -Compress) + [Environment]::NewLine)
    if (Test-Path -LiteralPath $before) { Copy-Item -LiteralPath $before -Destination (Join-Path $root ($prefix + '.after.json')) }
    return @{ success=$passed; value=$value; envelope=$envelope; evidence=$event }
}

if ($Operation -eq 'Start') {
    if (Test-Path -LiteralPath $root) { throw 'Start requires a fresh run directory; use Status for an existing session.' }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    if (-not $IsWindows) { [IO.File]::SetUnixFileMode($root, [IO.UnixFileMode]448) }
    New-Item -ItemType Directory -Path (Join-Path $root 'calls') | Out-Null
    $nativeBackend = if ($IsWindows) { 'win32' } elseif ($IsMacOS) { 'macos' } else { 'x11' }
    if ($Backend -eq 'Native' -and -not $IsWindows -and -not $IsMacOS -and -not $env:DISPLAY) {
        Write-Json (Join-Path $root 'blocked.json') @{ status='blocked'; reason='An explicitly selected X11 desktop (DISPLAY) is required. Use the existing managed X11 environment or a dedicated desktop worker.' }
        throw 'Native Linux QA requires an explicit DISPLAY; no headless fallback was selected.'
    }
    if (-not $SkipBuild) {
        $build = Invoke-Dotnet @('build', (Join-Path $repoRoot 'AvaScope.slnx'), '-c', $Configuration, '--nologo', '--disable-build-servers') 'build' 300
        if ($build.exitCode -ne 0) { throw "QA build failed: $root/build.stderr.log" }
    }
    Copy-Snapshot (Join-Path $repoRoot "src/AvaScope.Cli/bin/$Configuration/net10.0") (Join-Path $root 'tools')
    Copy-Snapshot (Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0") (Join-Path $root 'client')
    $cli = Join-Path $root 'tools/avascope.dll'
    $mcp = Join-Path $root 'tools/AvaScope.Mcp.dll'
    $client = Join-Path $root 'client/AvaScope.McpScenarioClient.dll'
    if ($HostDirectory) { Copy-Snapshot ([IO.Path]::GetFullPath($HostDirectory)) (Join-Path $root 'host') }
    else {
        $project = if ($Integration -eq 'Direct') { 'AvaScope.ComplexWorkflowApp' } else { 'AvaScope.StandaloneHost' }
        $publishArguments = @('publish', (Join-Path $repoRoot "samples/$project/$project.csproj"), '-c', $Configuration, '-o', (Join-Path $root 'host'), '--artifacts-path', (Join-Path $root 'build-host'), '--disable-build-servers', '--nologo')
        if ($Integration -eq 'Standalone') { $publishArguments += @('-p:EnableUiInspection=true','-p:EnableQaFixture=true') }
        $published = Invoke-Dotnet $publishArguments 'publish-host' 300
        if ($published.exitCode -ne 0) { throw "QA fixture publish failed: $root/publish-host.stdout.log" }
    }
    $hostAssembly = Join-Path $root $(if ($Integration -eq 'Direct') {'host/AvaScope.ComplexWorkflowApp.dll'} else {'host/StandaloneInspectionHost.dll'})
    if (-not (Test-Path -LiteralPath $hostAssembly)) { throw 'Selected host assembly is missing.' }
    $provider = $null
    if ($Integration -eq 'Standalone') {
        if (@(Get-ChildItem (Join-Path $root 'host') -Filter 'AvaScope.*.dll').Count) { throw 'Standalone host contains AvaScope assemblies.' }
        if (-not $ProviderDirectory) { $ProviderDirectory = Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider' }
        Copy-Snapshot ([IO.Path]::GetFullPath($ProviderDirectory)) (Join-Path $root 'provider')
        $provider = Join-Path $root 'provider'
        $verified = Invoke-Dotnet @($cli,'verify-provider','--directory',$provider) 'verify-provider'
        if ($verified.exitCode -ne 0) { throw 'Selected standalone provider failed verification.' }
    }
    $run = [ordered]@{
        schemaVersion=1; root=$root; status='starting'; integration=$Integration; requestedBackend=$Backend
        expectedBackend=$(if ($Backend -eq 'Native') {$nativeBackend} else {'headless'})
        startedAt=[DateTimeOffset]::UtcNow.ToString('O'); expiresAt=[DateTimeOffset]::UtcNow.AddSeconds($LifetimeSeconds).ToString('O')
        manifestDirectory=(Join-Path $root 'manifests'); cliAssembly=$cli; mcpAssembly=$mcp; clientAssembly=$client
        hostAssembly=$hostAssembly; providerDirectory=$provider; nativeCaptureAuthorized=$AuthorizeScreenCapture.IsPresent
        commit=(& git -C $repoRoot rev-parse HEAD); dirty=[bool](& git -C $repoRoot status --porcelain)
        sessionId=$null; topLevelId=$null; runId=$null
    }
    Write-Json $recordPath $run
    $empty = Join-Path $root 'empty.json'
    Write-Json $empty @{}
    $schemaResult = Invoke-Dotnet @($client,$mcp,$empty,$run.manifestDirectory,'--schemas') 'schemas'
    if ($schemaResult.exitCode -ne 0) { throw 'Selected MCP server did not provide schemas.' }
    $schemas = $schemaResult.text | ConvertFrom-Json -AsHashtable -Depth 100
    Write-Json (Join-Path $root 'schemas.json') $schemas
    $doctorPath = Join-Path $root 'doctor-request.json'
    # Doctor reports the enclosing repository as origin for snapshots beneath it.
    Write-Json $doctorPath @{assemblyPath=$hostAssembly;backend=$run.expectedBackend;expectedInstallationRoot=$repoRoot;providerDirectory=$provider;nativeScreenshot=$AuthorizeScreenCapture.IsPresent}
    $doctor = Invoke-Dotnet @($cli,'doctor','--target-request',$doctorPath,'--manifest-dir',$run.manifestDirectory) 'doctor'
    $run.doctorExitCode = $doctor.exitCode
    $null = Invoke-Dotnet @('--info') 'dotnet-info'
    $identityDirectories = @((Join-Path $root 'tools'),(Join-Path $root 'host'),(Join-Path $root 'client'))
    if ($provider) { $identityDirectories += $provider }
    $hashes = @(Get-ChildItem -LiteralPath $identityDirectories -File -Recurse | ForEach-Object {
        @{path=[IO.Path]::GetRelativePath($root,$_.FullName);length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    Write-Json (Join-Path $root 'artifact-identity.json') @{commit=$run.commit;dirty=$run.dirty;files=$hashes}
    Write-Json (Join-Path $root 'mcp-server.json') @{command='dotnet';args=@($mcp)}
    if ($doctor.exitCode -ne 0) {
        $run.status='blocked'; Write-Json $recordPath $run
        throw 'Selected environment failed doctor; inspect doctor.stdout.log. No fixture was launched.'
    }
    $launchArguments = @($hostAssembly, '--qa')
    if ($Backend -eq 'Headless') { $launchArguments += '--headless' }
    elseif ($Integration -eq 'Direct') { $launchArguments += '--native' }
    if ($AuthorizeScreenCapture) { $launchArguments += '--authorize-screen-capture' }
    $profile = @{
        schemaVersion=1; profiles=@{ interactive=@{
            scenario=@{
                launch=@{command='dotnet';argumentList=$launchArguments;manifestDirectory=$run.manifestDirectory;timeoutMs=30000}
                startupReadiness=@{waitForApplication=$true;waitForFrame=$true;timeoutMs=10000}
                steps=@(@{action='wait_for_node';selector=@{automationId='qa-reset';rendered=$true};timeoutMs=10000})
                outputDirectory=(Join-Path $root 'startup');terminateLaunchedProcess=$false;captureVisualTree=$true
            }
            environmentReferences=@{launch=@{
                AVASCOPE_QA_OUTPUT=@{name='AVASCOPE_QA_OUTPUT';secret=$false}
                AVASCOPE_QA_LIFETIME_SECONDS=@{name='AVASCOPE_QA_LIFETIME_SECONDS';secret=$false}
            }}
        }}
    }
    if ($provider) {
        $manifest = Get-Content (Join-Path $provider 'provider-manifest.json') -Raw | ConvertFrom-Json -AsHashtable
        $profile.profiles.interactive.provider = @{directory=$provider;version=$manifest.providerVersion;manifestSha256=(Get-FileHash (Join-Path $provider 'provider-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $profilePath = Join-Path $root 'profile.json'
    Write-Json $profilePath $profile
    $savedOutput = $env:AVASCOPE_QA_OUTPUT
    $savedLease = $env:AVASCOPE_QA_LIFETIME_SECONDS
    $run.startedAt = [DateTimeOffset]::UtcNow.ToString('O')
    $run.expiresAt = [DateTimeOffset]::UtcNow.AddSeconds($LifetimeSeconds).ToString('O')
    Write-Json $recordPath $run
    try {
        $env:AVASCOPE_QA_OUTPUT = $root
        $env:AVASCOPE_QA_LIFETIME_SECONDS = [string]$LifetimeSeconds
        $started = Invoke-Dotnet @($cli,'run-scenario','--profile-file',$profilePath,'--profile','interactive') 'startup'
    }
    finally { $env:AVASCOPE_QA_OUTPUT=$savedOutput; $env:AVASCOPE_QA_LIFETIME_SECONDS=$savedLease }
    $response = $started.text | ConvertFrom-Json -AsHashtable -Depth 100
    $run.sessionId = $response.value.sessionId
    $run.topLevelId = $response.value.topLevelId
    $run.runId = $response.value.runId
    $run.status = if ($started.exitCode -eq 0 -and $response.success -and $response.value.status -eq 'passed') {'ready'} else {'failed'}
    Write-Json $recordPath $run
    if ($run.status -ne 'ready') { throw "QA startup failed; retained diagnostics: $root. Use Stop to recover any owned session." }
    $levels = Invoke-Dotnet @($cli,'list-top-levels','--session',$run.sessionId,'--manifest-dir',$run.manifestDirectory) 'top-levels'
    $top = (($levels.text | ConvertFrom-Json -AsHashtable -Depth 100).value.topLevels | Where-Object id -eq $run.topLevelId)
    $run.observedBackend = $top.backend.backend
    $run.renderScaling = $top.renderScaling
    if ($levels.exitCode -ne 0 -or $run.observedBackend -ne $run.expectedBackend) {
        $run.status='blocked'; Write-Json $recordPath $run
        throw 'The actual backend differs from the requested backend; inspect top-levels evidence and stop the owned session.'
    }
    $null = Invoke-Dotnet @($cli,'session-capabilities','--session',$run.sessionId,'--manifest-dir',$run.manifestDirectory) 'session-capabilities'
    Write-Json $recordPath $run
    $run | ConvertTo-Json -Depth 10
    return
}

if (-not (Test-Path -LiteralPath $recordPath)) { throw 'No QA run record exists in the selected directory.' }
$run = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
if ($run.schemaVersion -ne 1 -or -not [string]::Equals($run.root,$root,$comparison)) { throw 'QA run identity does not match the selected directory.' }
$schemas = Get-Content (Join-Path $root 'schemas.json') -Raw | ConvertFrom-Json -AsHashtable -Depth 100
switch ($Operation) {
    'Schemas' { $schemas | ConvertTo-Json -Depth 100 }
    'Status' {
        $observed = Call-Tool 'list_top_levels' @{sessionId=$run.sessionId}
        @{run=$run; observation=$observed; appState=$(if (Test-Path (Join-Path $root 'qa-state.json')) {Get-Content (Join-Path $root 'qa-state.json') -Raw | ConvertFrom-Json})} | ConvertTo-Json -Depth 100
    }
    'Call' {
        if (-not $Tool -or -not $ArgumentsPath) { throw 'Call requires -Tool and -ArgumentsPath (a JSON object of exact public tool arguments).' }
        if ((Get-Item -LiteralPath $ArgumentsPath).Length -gt 1MB) { throw 'Tool arguments are limited to 1 MiB.' }
        $arguments = Get-Content -LiteralPath $ArgumentsPath -Raw | ConvertFrom-Json -AsHashtable -Depth 100
        if ($arguments -isnot [Collections.IDictionary]) { throw 'Tool arguments must be a JSON object.' }
        $called = Call-Tool $Tool $arguments
        $called | ConvertTo-Json -Depth 100
        if (-not $called.success -and -not $AllowFailure) { throw "Public tool failed; original envelope retained: $($called.evidence.responsePath)" }
    }
    'Reset' {
        $called = Call-Tool 'run_workflow' @{request=@{
            sessionId=$run.sessionId;topLevelId=$run.topLevelId
            steps=@(@{action='invoke';selector=@{automationId='qa-reset';actionable=$true}})
            outputDirectory=(Join-Path $root ('reset-' + [Guid]::NewGuid().ToString('N')))
        }}
        $called | ConvertTo-Json -Depth 100
        if (-not $called.success -or $called.value.value.status -ne 'passed') { throw 'QA reset did not pass.' }
    }
    'Stop' {
        if ($run.runId -and (-not $run.sessionId -or $run.status -in @('failed','blocked','cleanup_failed'))) {
            # Startup can fail before a session ownership marker exists. The private
            # run record already owns the exact launched process, including start identity.
            $recoveryArguments = @($run.cliAssembly,'recover-run','--run',$run.runId,'--store-dir',(Join-Path $root 'run-store'))
            $inspected = Invoke-Dotnet ($recoveryArguments + @('--operation','inspect')) 'stop-recovery-inspect'
            $inspection = $inspected.text | ConvertFrom-Json -AsHashtable -Depth 100
            if ($inspected.exitCode -ne 0 -or -not $inspection.success -or $inspection.value.active) {
                throw 'Owned QA recovery could not inspect an inactive run; inspect stop-recovery-inspect.stdout.log.'
            }
            # This read-only observation describes already-exited children. Only
            # recover-run may authorize termination; a reused PID must still be refused.
            $alreadyExited = @($inspection.value.processes).Count -gt 0
            foreach ($owned in $inspection.value.processes) {
                try {
                    $observed = [Diagnostics.Process]::GetProcessById([int]$owned.processId)
                    try { if (-not $observed.HasExited) { $alreadyExited = $false } }
                    finally { $observed.Dispose() }
                }
                catch [ArgumentException] { }
                catch { $alreadyExited = $false }
            }
            $stopped = Invoke-Dotnet ($recoveryArguments + @('--operation','cleanup')) 'stop-recovery'
            $result = $stopped.text | ConvertFrom-Json -AsHashtable -Depth 100
            $run.cleanupMethod = 'run_recovery'
            if ($stopped.exitCode -ne 0 -or -not $result.success -or $result.value.state -ne 'cleaned') {
                $run.cleanupStatus = 'failed'; Write-Json $recordPath $run
                throw 'Owned QA recovery failed; inspect stop-recovery.stdout.log. Startup evidence was retained.'
            }
            $run.cleanupStatus = 'cleaned'
            $run.cleanupOutcome = if ($alreadyExited) { 'already_exited' } else { 'cleaned' }
            $run.sessionId = $result.value.sessionId
            if ($result.value.outcome -in @('failed','cancelled')) { $run.status = 'failed' }
            elseif ($run.status -eq 'cleanup_failed') { $run.status = 'stopped' }
            $run.stoppedAt = [DateTimeOffset]::UtcNow.ToString('O')
            Write-Json $recordPath $run
            $result | ConvertTo-Json -Depth 100
            break
        }
        if (-not $run.sessionId) {
            $manifests = @(Get-ChildItem -LiteralPath $run.manifestDirectory -Filter '*.json' -ErrorAction SilentlyContinue | Where-Object Name -NotLike '.*')
            if ($manifests.Count -ne 1) { throw 'Startup has no unique session identity. Inspect startup and run-recovery evidence; do not kill an unverified process.' }
            # close-session verifies the launch ownership marker and live process identity.
            $run.sessionId = (Get-Content $manifests[0].FullName -Raw | ConvertFrom-Json).sessionId
        }
        $stopped = Invoke-Dotnet @($run.cliAssembly,'close-session','--session',$run.sessionId,'--manifest-dir',$run.manifestDirectory,'--terminate-launched-process','true') 'stop'
        $result = $stopped.text | ConvertFrom-Json -AsHashtable -Depth 100
        if ($stopped.exitCode -ne 0 -or $result.value.outcome -notin @('terminated','already_exited')) {
            $run.status='cleanup_failed'; Write-Json $recordPath $run
            throw 'Owned QA cleanup failed; inspect stop.stdout.log and existing run-recovery tooling.'
        }
        $run.status='stopped'; $run.stoppedAt=[DateTimeOffset]::UtcNow.ToString('O'); Write-Json $recordPath $run
        $result | ConvertTo-Json -Depth 100
    }
}
