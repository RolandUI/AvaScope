param(
    [Parameter(Mandatory = $true)]
    [string]$CliAssembly,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fullCliAssembly = [System.IO.Path]::GetFullPath($CliAssembly)
if (-not (Test-Path -LiteralPath $fullCliAssembly -PathType Leaf)) {
    throw "Packaged AvaScope CLI assembly was not found: $fullCliAssembly"
}

$projectPath = Join-Path $repositoryRoot "tests/AvaScope.LifecycleTestApp/AvaScope.LifecycleTestApp.csproj"
$smokeRoot = Join-Path $repositoryRoot ("artifacts/lifecycle-smoke-" + [Guid]::NewGuid().ToString("N"))
$manifestDirectory = Join-Path $smokeRoot "manifests"
$launchDirectory = Join-Path $smokeRoot "launch"
$markerPath = Join-Path $smokeRoot "marker.txt"
$timelinePath = Join-Path $smokeRoot "timeline.md"
$requestPath = Join-Path $smokeRoot "scenario.json"
$secretValue = "packaged-lifecycle-secret"
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null

try {
    $request = [ordered]@{
        requestId = "packaged-lifecycle-smoke"
        build = [ordered]@{
            projectPath = $projectPath
            configuration = $Configuration
            framework = "net10.0"
            noRestore = $true
            arguments = @("--nologo")
            timeoutMs = 120000
        }
        launch = [ordered]@{
            projectPath = $projectPath
            argumentList = @("--marker", $markerPath)
            configuration = $Configuration
            framework = "net10.0"
            noBuild = $true
            manifestDirectory = $manifestDirectory
            outputDirectory = $launchDirectory
            environment = [ordered]@{
                AVASCOPE_LIFECYCLE_TEST_SECRET = $secretValue
            }
            timeoutMs = 15000
        }
        outputDirectory = $smokeRoot
        timelinePath = $timelinePath
        terminateLaunchedProcess = $true
        steps = @(
            [ordered]@{
                id = "wait"
                action = "wait"
                waitMs = 1
            }
        )
    }
    $request | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $requestPath -Encoding UTF8

    $json = (& dotnet $fullCliAssembly run-scenario --request $requestPath | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Packaged lifecycle scenario failed with exit code $LASTEXITCODE. Output: $json"
    }

    $response = $json | ConvertFrom-Json -Depth 20
    if (-not $response.success -or $response.value.status -ne "passed") {
        throw "Packaged lifecycle scenario did not pass. Output: $json"
    }

    if ($response.value.build.status -ne "passed" -or
        $response.value.readiness.status -ne "ready" -or
        $response.value.workflow.status -ne "passed" -or
        $response.value.cleanup.outcome -ne "terminated") {
        throw "Packaged lifecycle stages did not all pass. Output: $json"
    }

    if ($response.value.topLevels.Count -ne 1 -or $response.value.topLevels[0].id -ne "topLevel:lifecycle") {
        throw "Packaged lifecycle scenario did not return the registered top level. Output: $json"
    }

    if ((Get-Content -LiteralPath $markerPath -Raw).Trim() -ne $secretValue) {
        throw "Packaged lifecycle app did not receive its environment value."
    }

    if ($json.Contains($secretValue, [System.StringComparison]::Ordinal)) {
        throw "Packaged lifecycle output leaked a launch environment value."
    }

    if (-not (Test-Path -LiteralPath $timelinePath -PathType Leaf)) {
        throw "Packaged lifecycle timeline was not created."
    }

    Write-Host "Packaged lifecycle scenario passed with build, launch, attach, workflow, evidence, and owned cleanup."
}
finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
}
