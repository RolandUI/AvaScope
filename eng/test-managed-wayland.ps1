#requires -Version 7.0
param([string]$Configuration = 'Release', [string]$CliAssembly, [string]$McpAssembly)
$ErrorActionPreference = 'Stop'
if (-not $IsLinux) { throw 'This validation requires Linux, Weston 13, wayland-utils, xkb-data and Mesa EGL software libraries.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repo ('artifacts/wayland-validation/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$dotnet = (Get-Command dotnet).Source
$cli = if ($CliAssembly) { [IO.Path]::GetFullPath($CliAssembly) } else { Join-Path $repo "src/AvaScope.Cli/bin/$Configuration/net10.0/avascope.dll" }
$mcp = if ($McpAssembly) { [IO.Path]::GetFullPath($McpAssembly) } else { Join-Path $repo "src/AvaScope.Mcp/bin/$Configuration/net10.0/AvaScope.Mcp.dll" }
$client = Join-Path $repo "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$hostOutput = Join-Path $root 'host'
& $dotnet publish (Join-Path $repo 'samples/AvaScope.StandaloneHost/AvaScope.StandaloneHost.csproj') -c $Configuration -o $hostOutput --artifacts-path (Join-Path $root 'build') -p:EnableUiInspection=true -p:EnableWaylandFixture=true *> (Join-Path $root 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Wayland host build failed; see $root/build.log." }
if (@(Get-ChildItem $hostOutput -Filter 'AvaScope.*.dll').Count) { throw 'The standalone host contains AvaScope assemblies.' }
$profilePath = Join-Path $root 'profiles.json'
$summaries = @()
foreach ($case in @('cli-1','mcp-1','cli-2','mcp-2','cli-failure','mcp-failure','cli-wrong-backend')) {
    $capture = $null
    $scale = if ($case.EndsWith('-2')) { 2 } else { 1 }
    $expected = if ($case.EndsWith('-failure')) { 'deliberately incorrect' } else { 'Wayland text' }
    $hostArgs = @((Join-Path $hostOutput 'StandaloneInspectionHost.dll'), '--exit-after-ms=60000', $(if ($case.EndsWith('wrong-backend')) { '--headless' } else { '--wayland' }))
    $profile = @{schemaVersion=1;profiles=@{native=@{
        provider=@{directory=(Join-Path $repo 'artifacts/providers/avascope-bridge-provider')}
        scenario=@{
            launch=@{command=$dotnet;argumentList=$hostArgs;timeoutMs=20000}
            waylandEnvironment=@{width=640;height=480;scale=$scale;keyboardLayout='us'}
            outputDirectory='runs/{runId}';captureVisualTree=$true
            steps=@(
                @{id='inspect';action='inspect';selector=@{name='NameField'}},
                @{id='clear';action='clear_text';selector=@{name='NameField'}},
                @{id='type';action='type_text';selector=@{name='NameField'};text='Wayland text'},
                @{id='verify';action='wait_for_state';selector=@{name='NameField'};waitCondition=@{kind='text';expected=$expected;valueType='string'};timeoutMs=1000},
                @{id='capture';action='screenshot';captureAfterRender=$true})
        }
    }}}
    [IO.File]::WriteAllText($profilePath, ($profile | ConvertTo-Json -Depth 20))
    if ($case.StartsWith('cli')) { $json = & $dotnet $cli run-scenario --profile-file $profilePath --profile native }
    else {
        $request = Join-Path $root 'mcp-request.json'
        [IO.File]::WriteAllText($request, (@{profileFile=$profilePath;profileName='native'} | ConvertTo-Json))
        $json = & $dotnet $client $mcp $request $root run_scenario
    }
    $exitCode = $LASTEXITCODE
    [IO.File]::WriteAllText((Join-Path $root "$case.json"), ($json -join [Environment]::NewLine))
    $result = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
    if (-not $result -or $result.environment.status -ne 'closed' -or -not $result.environment.runtimeDirectoryRemoved -or $result.environment.tcpListening -ne 'disabled') {
        throw "Wayland $case environment/cleanup failed: $json"
    }
    foreach ($helper in $result.environment.helpers) {
        if (-not $helper.exited -or (Get-Process -Id $helper.processId -ErrorAction SilentlyContinue)) { throw 'An owned compositor survived.' }
    }
    if ($case.EndsWith('wrong-backend')) {
        if ($result.status -ne 'failed' -or 'wayland_host_backend_mismatch' -notin $result.diagnostics.code) { throw 'Wrong backend was not rejected.' }
    } elseif ($case.EndsWith('-failure')) {
        if ($result.status -ne 'failed') { throw 'The intentional failed assertion passed.' }
    } else {
        if ($exitCode -ne 0 -or $result.status -ne 'passed') { throw "Wayland $case scenario failed: $json" }
        $capture = ($result.workflow.steps | Where-Object stepId -eq 'capture').screenshot
        if ($result.topLevels[0].backend.backend -ne 'wayland' -or $capture.provenance.backend.backend -ne 'wayland' -or
            $capture.provenance.route -ne 'avalonia_render_target_bitmap' -or $capture.provenance.renderScaling -ne $scale -or
            $capture.pixelWidth -ne [Math]::Ceiling($result.topLevels[0].width*$scale) -or
            $capture.pixelHeight -ne [Math]::Ceiling($result.topLevels[0].height*$scale) -or -not (Test-Path $capture.filePath)) { throw 'Native Wayland capture/backend/scale evidence is incorrect.' }
        if ($result.environment.wayland.outputPixelWidth -ne 640*$scale -or $result.environment.wayland.outputPixelHeight -ne 480*$scale) { throw 'Observed compositor geometry is incorrect.' }
        if ($result.topLevels[0].backend.inputRoutes -match 'native_') { throw 'Wayland falsely advertises native input.' }
    }
    $summaries += @{case=$case;status=$result.status;environment=$result.environment;topLevels=$result.topLevels;screenshot=$capture}
}
$previous = $env:PATH
try {
    $env:PATH = Join-Path $root 'no-executables'
    $json = & $dotnet $cli run-scenario --profile-file $profilePath --profile native
    $result = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
    if ($LASTEXITCODE -ne 1 -or 'wayland_dependency_missing' -notin $result.diagnostics.code -or -not $result.environment.runtimeDirectoryRemoved) { throw 'Missing prerequisites did not fail safely.' }
    [IO.File]::WriteAllText((Join-Path $root 'missing-dependency.json'), ($json -join [Environment]::NewLine))
} finally { $env:PATH = $previous }
[IO.File]::WriteAllText((Join-Path $root 'validation.json'), (@{success=$true;lane='native_wayland';xwayland='explicitly_unsupported';runs=$summaries} | ConvertTo-Json -Depth 35))
Write-Output "Controlled Wayland CLI/MCP 1x/2x, failed workflow, wrong backend, prerequisites and cleanup passed: $root"
