#requires -Version 7.0
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'This validation requires Linux and Xvfb, xdpyinfo, openbox, xprop and dbus-daemon/dbus-send.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repoRoot ('artifacts/x11-validation/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$dotnet = (Get-Command dotnet).Source
$cli = Join-Path $repoRoot "src/AvaScope.Cli/bin/$Configuration/net10.0/avascope.dll"
$mcp = Join-Path $repoRoot "src/AvaScope.Mcp/bin/$Configuration/net10.0/AvaScope.Mcp.dll"
$client = Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$project = Join-Path $repoRoot 'samples/AvaScope.StandaloneHost/AvaScope.StandaloneHost.csproj'
$hostOutput = Join-Path $root 'host'
& $dotnet publish $project -c $Configuration -o $hostOutput --artifacts-path (Join-Path $root 'build') -p:EnableUiInspection=true *> (Join-Path $root 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'X11 external-provider host build failed.' }
$profilePath = Join-Path $root 'profiles.json'
$profile = @{
    schemaVersion=1; profiles=@{native=@{
        provider=@{directory=(Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider')}
        scenario=@{
            launch=@{command='dotnet';argumentList=@((Join-Path $hostOutput 'StandaloneInspectionHost.dll'));timeoutMs=20000}
            x11Environment=@{mode='managed';windowManager=$true;sessionBus=$true}
            outputDirectory='runs/{runId}';captureVisualTree=$true
            steps=@(@{action='inspect';selector=@{name='NameField'}},@{action='focus';selector=@{name='NameField'}},@{action='screenshot'})
        }
    }}
}
[IO.File]::WriteAllText($profilePath, ($profile | ConvertTo-Json -Depth 15))
$displays = @()
foreach ($adapter in @('cli','mcp')) {
    if ($adapter -eq 'cli') { $json = & $dotnet $cli run-scenario --profile-file $profilePath --profile native }
    else {
        $requestPath = Join-Path $root 'mcp-request.json'
        [IO.File]::WriteAllText($requestPath, (@{profileFile=$profilePath;profileName='native'} | ConvertTo-Json))
        $json = & $dotnet $client $mcp $requestPath $root run_scenario
    }
    $exitCode = $LASTEXITCODE
    [IO.File]::WriteAllText((Join-Path $root "$adapter.json"), ($json -join [Environment]::NewLine))
    $result = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
    if ($exitCode -ne 0 -or $result.status -ne 'passed') { throw "Managed X11 $adapter scenario failed: $json" }
    if ($result.environment.backend -ne 'x11' -or $result.environment.tcpListening -ne 'disabled' -or $result.environment.status -ne 'closed' -or -not $result.environment.runtimeDirectoryRemoved) {
        throw 'Managed X11 provenance or cleanup failed.'
    }
    foreach ($helper in $result.environment.helpers) {
        if (-not $helper.exited -or (Get-Process -Id $helper.processId -ErrorAction SilentlyContinue)) { throw 'An owned helper survived scenario cleanup.' }
    }
    if ($result.topLevels[0].backend.backend -ne 'x11' -or
        $result.workflow.steps[1].input.provenance.route -ne 'avalonia_focus_api' -or
        $result.workflow.steps[2].screenshot.provenance.route -ne 'avalonia_render_target_bitmap' -or
        $result.workflow.steps[2].screenshot.provenance.backend.backend -ne 'x11') {
        throw 'Actual X11 operation provenance was lost through the adapter or workflow.'
    }
    $displays += $result.environment.display
}
$oldPath = $env:PATH
try {
    $env:PATH = Join-Path $root 'no-executables'
    $json = & $dotnet $cli run-scenario --profile-file $profilePath --profile native
    $result = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
    if ($LASTEXITCODE -ne 1 -or 'x11_dependency_missing' -notin $result.diagnostics.code -or -not $result.environment.runtimeDirectoryRemoved) {
        throw "Missing X11 prerequisites did not fail safely: $json"
    }
}
finally { $env:PATH = $oldPath }
[IO.File]::WriteAllText((Join-Path $root 'validation.json'), (@{success=$true;displays=$displays;backend='x11';surfaces=@('cli','mcp')} | ConvertTo-Json))
Write-Output "Managed X11 external-provider CLI/MCP scenarios, dependency failure and cleanup passed: $root"
