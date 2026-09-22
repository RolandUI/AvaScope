#requires -Version 7.0
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repoRoot ('artifacts/readiness-validation/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cli = Join-Path $repoRoot "src/AvaScope.Cli/bin/$Configuration/net10.0/avascope.dll"
$mcp = Join-Path $repoRoot "src/AvaScope.Mcp/bin/$Configuration/net10.0/AvaScope.Mcp.dll"
$client = Join-Path $repoRoot "tests/AvaScope.McpScenarioClient/bin/$Configuration/net10.0/AvaScope.McpScenarioClient.dll"
$assembly = Join-Path $repoRoot "samples/AvaScope.StandaloneHost/bin/$Configuration/net10.0/StandaloneInspectionHost.dll"
$request = @{
    assemblyPath=$assembly
    providerDirectory=(Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider')
    backend=$(if ($IsLinux) {'x11'} elseif ($IsMacOS) {'macos'} else {'win32'})
}
$requestPath = Join-Path $root 'target.json'
[IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json))
$json = & dotnet $cli doctor --target-request $requestPath
$exitCode = $LASTEXITCODE
[IO.File]::WriteAllText((Join-Path $root 'cli.json'), ($json -join [Environment]::NewLine))
$response = ($json -join [Environment]::NewLine | ConvertFrom-Json).value.target
if ($exitCode -ne 0 -or $response.status -ne 'available' -or $response.bridgeActivated) { throw "Native CLI readiness failed: $json" }
$mcpRequestPath = Join-Path $root 'mcp-target.json'
[IO.File]::WriteAllText($mcpRequestPath, (@{request=$request} | ConvertTo-Json -Depth 8))
$json = & dotnet $client $mcp $mcpRequestPath $root doctor_target
$exitCode = $LASTEXITCODE
[IO.File]::WriteAllText((Join-Path $root 'mcp.json'), ($json -join [Environment]::NewLine))
$response = ($json -join [Environment]::NewLine | ConvertFrom-Json).value
if ($exitCode -ne 0 -or $response.status -ne 'available' -or $response.bridgeActivated) { throw "Native MCP readiness failed: $json" }

# An incomplete but otherwise real build must fail the dependency stage without loading the host.
$incomplete = Join-Path $root 'incomplete'
New-Item -ItemType Directory -Force -Path $incomplete | Out-Null
foreach ($extension in @('.dll','.runtimeconfig.json','.deps.json')) {
    Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($assembly, $extension)) -Destination $incomplete
}
$badRequest = @{assemblyPath=(Join-Path $incomplete 'StandaloneInspectionHost.dll');backend='headless'}
[IO.File]::WriteAllText($requestPath, ($badRequest | ConvertTo-Json))
$json = & dotnet $cli doctor --target-request $requestPath
$response = ($json -join [Environment]::NewLine | ConvertFrom-Json).value.target
if ($LASTEXITCODE -ne 1 -or 'target_dependency_missing' -notin $response.checks.error.code) { throw "Missing dependencies were not identified: $json" }

if ($IsLinux) {
    $oldDisplay = $env:DISPLAY
    $oldAuthority = $env:XAUTHORITY
    try {
        foreach ($case in @(@{display='';code='target_display_missing'},@{display=':65530';code='target_display_inaccessible'})) {
            $env:DISPLAY = $case.display
            $env:XAUTHORITY = '/nonexistent/private-authorization-material'
            [IO.File]::WriteAllText($requestPath, (@{backend='x11'} | ConvertTo-Json))
            $json = & dotnet $cli doctor --target-request $requestPath
            $response = ($json -join [Environment]::NewLine | ConvertFrom-Json).value.target
            if ($LASTEXITCODE -ne 1 -or $case.code -notin $response.checks.error.code) { throw "X11 failure was not identified: $json" }
            if (($json -join '').Contains('private-authorization-material')) { throw 'X11 authorization material leaked into diagnostics.' }
        }
    }
    finally { $env:DISPLAY = $oldDisplay; $env:XAUTHORITY = $oldAuthority }
}
[IO.File]::WriteAllText((Join-Path $root 'validation.json'), (@{success=$true;backend=$request.backend} | ConvertTo-Json))
Write-Output "Native CLI/MCP readiness and missing-dependency/display failure checks passed: $root"
