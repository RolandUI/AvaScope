#requires -Version 7.0
param(
    [string]$Configuration = 'Release',
    [switch]$Native,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$provider = Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider'
$root = Join-Path $repoRoot ('artifacts/provider-validation/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cli = Join-Path $repoRoot "src/AvaScope.Cli/bin/$Configuration/net10.0/avascope.dll"
$mcp = Join-Path $repoRoot "src/AvaScope.Mcp/bin/$Configuration/net10.0/AvaScope.Mcp.dll"
$client = Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$project = Join-Path $repoRoot 'samples/AvaScope.StandaloneHost/AvaScope.StandaloneHost.csproj'
$script:requestNumber = 0
$processes = [Collections.Generic.List[object]]::new()

function Invoke-Cli {
    param([string[]]$Arguments, [switch]$AllowFailure)
    $script:requestNumber++
    $log = Join-Path $root "cli-$script:requestNumber"
    $json = & dotnet $cli @Arguments 2> "$log.stderr.log"
    $exitCode = $LASTEXITCODE
    $json | Set-Content -LiteralPath "$log.json"
    if (-not $AllowFailure -and $exitCode -ne 0) { throw "CLI failed: $($Arguments[0]): $json" }
    return ($json -join [Environment]::NewLine | ConvertFrom-Json)
}

function Invoke-Mcp {
    param([string]$Tool, [hashtable]$Arguments, [switch]$AllowFailure)
    $script:requestNumber++
    $requestPath = Join-Path $root "mcp-$script:requestNumber-request.json"
    [IO.File]::WriteAllText($requestPath, ($Arguments | ConvertTo-Json -Depth 15))
    $json = & dotnet $client $mcp $requestPath $root $Tool 2> "$requestPath.stderr.log"
    $exitCode = $LASTEXITCODE
    $json | Set-Content -LiteralPath "$requestPath.result.json"
    if (-not $AllowFailure -and $exitCode -ne 0) { throw "MCP failed: ${Tool}: $json" }
    if ($json) { return ($json -join [Environment]::NewLine | ConvertFrom-Json) }
}

function Start-Sample {
    param([string]$Name, [string]$HostDirectory, [string]$ProviderDirectory = $provider,
        [string[]]$ExtraArguments = @(), [string]$PinnedVersion, [int]$ExitAfter = 120000)
    $caseRoot = Join-Path $root $Name
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = $caseRoot
    $info.ArgumentList.Add((Join-Path $HostDirectory 'StandaloneInspectionHost.dll'))
    if (-not $Native) { $info.ArgumentList.Add('--headless') }
    $info.ArgumentList.Add("--exit-after-ms=$ExitAfter")
    foreach ($argument in $ExtraArguments) { $info.ArgumentList.Add($argument) }
    $info.Environment['UI_INSPECTION_PROVIDER_PATH'] = $ProviderDirectory
    $info.Environment.Remove('UI_INSPECTION_PROVIDER_SHA256') | Out-Null
    $info.Environment.Remove('UI_INSPECTION_PROVIDER_VERSION') | Out-Null
    if ($PinnedVersion) { $info.Environment['UI_INSPECTION_PROVIDER_VERSION'] = $PinnedVersion }
    $manifestDirectory = Join-Path $caseRoot 'sessions'
    $info.Environment['AVASCOPE_BRIDGE_MANIFEST_DIR'] = $manifestDirectory
    $process = [Diagnostics.Process]::Start($info)
    $run = [pscustomobject]@{
        Process = $process; ManifestDirectory = $manifestDirectory; Root = $caseRoot
        Stdout = $process.StandardOutput.ReadToEndAsync(); Stderr = $process.StandardError.ReadToEndAsync()
    }
    $processes.Add($run)
    return $run
}

function Wait-Manifest {
    param($Run)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 20) {
        $files = @(Get-ChildItem -LiteralPath $Run.ManifestDirectory -Filter '*.json' -File -ErrorAction SilentlyContinue)
        if ($files.Count -eq 1) { return (Get-Content -Raw -LiteralPath $files[0].FullName | ConvertFrom-Json) }
        if ($Run.Process.HasExited) { throw "Sample exited before activation: $($Run.Stderr.Result)" }
        Start-Sleep -Milliseconds 50
    }
    if (-not $Run.Process.HasExited) { $Run.Process.Kill($true); $Run.Process.WaitForExit() }
    throw "Provider readiness timed out: $($Run.Stderr.Result)"
}

function Assert-Negative {
    param($Run, [string]$Diagnostic)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $Run.Process.HasExited -and $timer.Elapsed.TotalSeconds -lt 20) {
        if (@(Get-ChildItem -LiteralPath $Run.ManifestDirectory -Filter '*.json' -File -ErrorAction SilentlyContinue).Count -gt 0) {
            throw 'Disabled or invalid provider created a bridge manifest.'
        }
        Start-Sleep -Milliseconds 30
    }
    if (-not $Run.Process.HasExited -or $Run.Process.ExitCode -ne 0) { throw 'Negative sample did not exit normally.' }
    if ($Diagnostic -and -not $Run.Stderr.Result.Contains($Diagnostic)) { throw "Missing diagnostic ${Diagnostic}: $($Run.Stderr.Result)" }
    if (@(Get-ChildItem -LiteralPath $Run.ManifestDirectory -Filter '*.json' -File -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Negative sample leaked a manifest.' }
}

try {
    if (-not $SkipBuild) {
        & dotnet build (Join-Path $repoRoot 'AvaScope.slnx') -c $Configuration --nologo *> (Join-Path $root 'build.log')
        if ($LASTEXITCODE -ne 0) { throw "Solution build failed; see $root/build.log" }
        & (Join-Path $PSScriptRoot 'package-provider.ps1') -Configuration $Configuration *> (Join-Path $root 'package.log')
    }
    & (Join-Path $PSScriptRoot 'verify-provider.ps1')
    foreach ($variant in @('enabled','disabled','incompatible')) {
        $hostPath = Join-Path $root "host-$variant"
        $enabled = if ($variant -eq 'disabled') { 'false' } else { 'true' }
        $avaloniaVersion = if ($variant -eq 'incompatible') { '12.0.0' } else { '12.1.0' }
        & dotnet publish $project -c $Configuration -o $hostPath "-p:EnableUiInspection=$enabled" "-p:AvaloniaVersion=$avaloniaVersion" --nologo *> (Join-Path $root "build-$variant.log")
        if ($LASTEXITCODE -ne 0) { throw "Host $variant build failed; see $root/build-$variant.log" }
        if (@(Get-ChildItem -LiteralPath $hostPath -Recurse -File -Filter 'AvaScope.*.dll').Count -gt 0) { throw 'Host output contains an AvaScope assembly.' }
    }
    $enabledHost = Join-Path $root 'host-enabled'
    $missingBootstrapProject = Join-Path $root 'missing-bootstrap'
    New-Item -ItemType Directory -Path $missingBootstrapProject | Out-Null
    [IO.File]::WriteAllText((Join-Path $missingBootstrapProject 'Missing.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>AvaScope.Bridge</AssemblyName><IsPackable>false</IsPackable></PropertyGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $missingBootstrapProject 'Bootstrap.cs'), 'namespace AvaScope.Bridge; public static class Bootstrap { public static string WrongMethod() => "not-started"; }')
    & dotnet build (Join-Path $missingBootstrapProject 'Missing.csproj') -c $Configuration -o (Join-Path $root 'missing-bootstrap-output') --nologo *> (Join-Path $root 'build-missing-bootstrap.log')
    if ($LASTEXITCODE -ne 0) { throw 'Missing-bootstrap fixture build failed.' }
    $identity = Invoke-Cli -Arguments @('verify-provider','--directory',$provider)
    if (-not $identity.success -or $identity.value.activated) { throw 'Offline verification activated or failed.' }
    $mcpIdentity = Invoke-Mcp -Tool 'verify_provider' -Arguments @{
        directory = $provider; expectedVersion = $identity.value.providerVersion; expectedManifestSha256 = $identity.value.manifestSha256
    }
    if ($mcpIdentity.value.manifestSha256 -ne $identity.value.manifestSha256) { throw 'CLI/MCP provider identity mismatch.' }

    $run = Start-Sample -Name 'positive' -HostDirectory $enabledHost -PinnedVersion $identity.value.providerVersion
    $manifest = Wait-Manifest $run
    $attach = Invoke-Cli -Arguments @('attach','--process',"$($run.Process.Id)",'--manifest-dir',$run.ManifestDirectory)
    $sessionId = $attach.value.session.sessionId
    $topLevels = Invoke-Cli -Arguments @('list-top-levels','--session',$sessionId,'--manifest-dir',$run.ManifestDirectory)
    if ($topLevels.value.topLevels.Count -ne 1) { throw 'Existing main window was not registered once.' }
    $mainId = $topLevels.value.topLevels[0].id
    Invoke-Mcp -Tool 'visual_tree' -Arguments @{ sessionId=$sessionId; topLevelId=$mainId; maxDepth=6; manifestDirectory=$run.ManifestDirectory } | Out-Null
    Invoke-Mcp -Tool 'screenshot' -Arguments @{ sessionId=$sessionId; topLevelId=$mainId; outputPath=(Join-Path $run.Root 'main.png'); manifestDirectory=$run.ManifestDirectory } | Out-Null
    if ((Get-Item -LiteralPath (Join-Path $run.Root 'main.png')).Length -lt 64) { throw 'Screenshot is missing or empty.' }
    $open = Invoke-Cli -Arguments @('find-nodes','--session',$sessionId,'--top-level',$mainId,'--name','OpenWindow','--manifest-dir',$run.ManifestDirectory)
    if ($open.value.matches.Count -ne 1) { throw 'Open-window control was not unique.' }
    Invoke-Mcp -Tool 'input' -Arguments @{ sessionId=$sessionId; topLevelId=$mainId; action='invoke'; targetNodeId=$open.value.matches[0].node.nodeId; manifestDirectory=$run.ManifestDirectory } | Out-Null
    $topLevels = Invoke-Cli -Arguments @('list-top-levels','--session',$sessionId,'--manifest-dir',$run.ManifestDirectory)
    if ($topLevels.value.topLevels.Count -ne 2) { throw 'New child window was not registered automatically.' }
    $childId = ($topLevels.value.topLevels | Where-Object id -ne $mainId).id
    $close = Invoke-Cli -Arguments @('find-nodes','--session',$sessionId,'--top-level',$childId,'--name','CloseChild','--manifest-dir',$run.ManifestDirectory)
    Invoke-Cli -Arguments @('input','--session',$sessionId,'--top-level',$childId,'--action','invoke','--target-node',$close.value.matches[0].node.nodeId,'--manifest-dir',$run.ManifestDirectory) | Out-Null
    $afterClose = Invoke-Cli -Arguments @('list-top-levels','--session',$sessionId,'--manifest-dir',$run.ManifestDirectory)
    if ($afterClose.value.topLevels.Count -ne 1) { throw 'Closed child window remained registered.' }
    $quit = Invoke-Cli -Arguments @('find-nodes','--session',$sessionId,'--top-level',$mainId,'--name','Quit','--manifest-dir',$run.ManifestDirectory)
    # The app can close its transport before replying to its own quit action.
    Invoke-Mcp -Tool 'input' -Arguments @{ sessionId=$sessionId; topLevelId=$mainId; action='invoke'; targetNodeId=$quit.value.matches[0].node.nodeId; manifestDirectory=$run.ManifestDirectory } -AllowFailure | Out-Null
    if (-not $run.Process.WaitForExit(10000) -or $run.Process.ExitCode -ne 0) { throw 'Normal application shutdown failed.' }
    if (Test-Path -LiteralPath (Join-Path $run.ManifestDirectory "$sessionId.json")) { throw 'Shutdown leaked the session manifest.' }
    Write-Output 'Positive reflection/CLI/MCP/window lifecycle/screenshot/normal-shutdown lane passed.'

    $remote = Start-Sample -Name 'remote-close' -HostDirectory $enabledHost
    $remoteManifest = Wait-Manifest $remote
    Invoke-Mcp -Tool 'close_session' -Arguments @{ sessionId=$remoteManifest.sessionId; manifestDirectory=$remote.ManifestDirectory } | Out-Null
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ((Test-Path -LiteralPath (Join-Path $remote.ManifestDirectory "$($remoteManifest.sessionId).json")) -and $timer.Elapsed.TotalSeconds -lt 5) { Start-Sleep -Milliseconds 30 }
    if (Test-Path -LiteralPath (Join-Path $remote.ManifestDirectory "$($remoteManifest.sessionId).json")) { throw 'Remote close leaked its manifest.' }
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $remoteManifest.pipeName, [IO.Pipes.PipeDirection]::InOut)
    try {
        $connected = $false
        try { $pipe.Connect(200); $connected = $true } catch [TimeoutException] { } catch [IO.IOException] { }
        if ($connected) { throw 'Remote close left the named pipe listening.' }
    } finally { $pipe.Dispose() }

    Assert-Negative (Start-Sample -Name 'disabled' -HostDirectory (Join-Path $root 'host-disabled') -ExitAfter 700)
    Assert-Negative (Start-Sample -Name 'load-only' -HostDirectory $enabledHost -ExtraArguments @('--load-only') -ExitAfter 700) 'AVASCOPE_PROVIDER_LOADED_ONLY'
    Assert-Negative (Start-Sample -Name 'missing' -HostDirectory $enabledHost -ProviderDirectory (Join-Path $root 'absent') -ExitAfter 700) 'AVASCOPE_PROVIDER_MANIFEST_INVALID'
    Assert-Negative (Start-Sample -Name 'wrong-pin' -HostDirectory $enabledHost -PinnedVersion '99.0.0' -ExitAfter 700) 'AVASCOPE_PROVIDER_PIN_MISMATCH'
    Assert-Negative (Start-Sample -Name 'wrong-avalonia' -HostDirectory (Join-Path $root 'host-incompatible') -ExitAfter 700) 'AVASCOPE_AVALONIA_INCOMPATIBLE'
    $badProvider = Join-Path $root 'tampered-provider'
    Copy-Item -LiteralPath $provider -Destination $badProvider -Recurse
    [IO.File]::AppendAllText((Join-Path $badProvider 'AvaScope.Bridge.dll'), 'tampered')
    Assert-Negative (Start-Sample -Name 'tampered' -HostDirectory $enabledHost -ProviderDirectory $badProvider -ExitAfter 700) 'AVASCOPE_PROVIDER_CORRUPT'
    foreach ($invalidKind in @('bad-image','missing-bootstrap')) {
        $badAssembly = Join-Path $badProvider 'AvaScope.Bridge.dll'
        if ($invalidKind -eq 'bad-image') { [IO.File]::WriteAllText($badAssembly, 'Not a managed assembly') }
        else { Copy-Item -LiteralPath (Join-Path $root 'missing-bootstrap-output/AvaScope.Bridge.dll') -Destination $badAssembly -Force }
        $badManifestPath = Join-Path $badProvider 'provider-manifest.json'
        $badManifest = Get-Content -Raw -LiteralPath $badManifestPath | ConvertFrom-Json
        $entry = $badManifest.files | Where-Object path -eq 'AvaScope.Bridge.dll'
        $entry.length = (Get-Item -LiteralPath $badAssembly).Length
        $entry.sha256 = (Get-FileHash -LiteralPath $badAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($badManifestPath, ($badManifest | ConvertTo-Json -Depth 8))
        $diagnostic = if ($invalidKind -eq 'bad-image') { 'AVASCOPE_PROVIDER_ASSEMBLY_INVALID' } else { 'AVASCOPE_BOOTSTRAP_MISSING' }
        Assert-Negative (Start-Sample -Name $invalidKind -HostDirectory $enabledHost -ProviderDirectory $badProvider -ExitAfter 700) $diagnostic
    }
    Write-Output 'Disabled, load-only, missing, pin mismatch, incompatible Avalonia, tamper, invalid assembly, missing bootstrap and remote-cleanup lanes passed.'
    [IO.File]::WriteAllText((Join-Path $root 'validation.json'), (@{
        success=$true; backend= $(if ($Native) { 'native' } else { 'headless' }); providerVersion=$identity.value.providerVersion
        manifestSha256=$identity.value.manifestSha256; runtime=[Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
        completedAt=[DateTimeOffset]::UtcNow.ToString('O')
    } | ConvertTo-Json))
    Write-Output "Standalone provider validation evidence: $root"
}
finally {
    foreach ($run in $processes) {
        if (-not $run.Process.HasExited) { $run.Process.Kill($true); $run.Process.WaitForExit() }
        [IO.File]::WriteAllText((Join-Path $run.Root 'stdout.log'), $run.Stdout.Result)
        [IO.File]::WriteAllText((Join-Path $run.Root 'stderr.log'), $run.Stderr.Result)
        $run.Process.Dispose()
    }
}
