#requires -Version 7.0
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repoRoot ('artifacts/native-input/' + [Guid]::NewGuid().ToString('N'))
$hostOutput = Join-Path $root 'host'
New-Item -ItemType Directory -Force -Path $root | Out-Null
& dotnet publish (Join-Path $repoRoot 'samples/AvaScope.StandaloneHost/AvaScope.StandaloneHost.csproj') -c $Configuration -o $hostOutput --artifacts-path (Join-Path $root 'build') -p:EnableUiInspection=true -p:EnableTableFixture=true *> (Join-Path $root 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Native input host publish failed: $root/build.log" }
if (@(Get-ChildItem $hostOutput -Filter 'AvaScope.*.dll').Count -ne 0) { throw 'The reflection host unexpectedly contains AvaScope assemblies.' }
$previous = @{}
foreach ($name in @('AVASCOPE_NATIVE_INPUT_HOST','AVASCOPE_NATIVE_INPUT_PROVIDER','AVASCOPE_NATIVE_INPUT_OUTPUT')) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:AVASCOPE_NATIVE_INPUT_HOST = Join-Path $hostOutput 'StandaloneInspectionHost.dll'
    $env:AVASCOPE_NATIVE_INPUT_PROVIDER = Join-Path $repoRoot 'artifacts/providers/avascope-bridge-provider'
    $env:AVASCOPE_NATIVE_INPUT_OUTPUT = Join-Path $root 'evidence'
    & dotnet test (Join-Path $repoRoot 'tests/AvaScope.Tests/AvaScope.Tests.csproj') -c $Configuration --no-build --filter 'FullyQualifiedName~NativeInputIntegrationTests' *> (Join-Path $root 'test.log')
    if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $root 'test.log'); throw "Native input/dialog validation failed: $root" }
    if (-not (Test-Path (Join-Path $env:AVASCOPE_NATIVE_INPUT_OUTPUT 'validation.json'))) { throw 'Native input gate did not produce its validation evidence.' }
    Write-Output "Native owned-window input, real dialogs, predefined result and ownership gate passed: $root"
}
finally { foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) } }
