param(
    [string]$OutputDirectory = "artifacts/release-guard"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$guardPath = Join-Path $PSScriptRoot "validate-release-commit.ps1"
$fixtureDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null
$propsPath = Join-Path $fixtureDirectory "release-guard-fixture.props"
if (Test-Path -LiteralPath $propsPath) {
    throw "Release guard fixture already exists; refusing to replace it."
}

$script:response = ''
$script:exitCode = 0
$script:expectedMilestone = 'v1.6.0'
$script:passed = 0

# The real guard runs unchanged; only its external GitHub command is replaced.
function gh {
    $arguments = @($args)
    if (($arguments -join ' ') -ne "issue list --repo RolandUI/AvaScope --milestone $expectedMilestone --state open --limit 1000 --json number,labels,milestone") {
        throw "Unexpected GitHub query."
    }
    $global:LASTEXITCODE = $exitCode
    $response
}

function Test-GuardCase {
    param(
        [string]$Name,
        [string]$Response,
        [string]$ExpectedError = '',
        [string]$Version = '1.6.0',
        [string]$BuildVersion = '1.6.0',
        [string]$Subject = 'Release 1.6.0',
        [int]$ExitCode = 0
    )

    $script:response = $Response
    $script:exitCode = $ExitCode
    Set-Content -LiteralPath $propsPath -Value "<Project><PropertyGroup><Version>$BuildVersion</Version></PropertyGroup></Project>"
    $failure = $null
    try {
        & $guardPath -Version $Version -CommitSubject $Subject -BuildPropsPath $propsPath *> $null
    } catch {
        $failure = $_.Exception.Message
    }
    if ([string]::IsNullOrWhiteSpace($ExpectedError)) {
        if ($null -ne $failure) { throw "${Name}: unexpected failure: $failure" }
    } elseif ($null -eq $failure -or $failure -notlike $ExpectedError) {
        throw "${Name}: expected '$ExpectedError', received '$failure'."
    }
    $script:passed++
}

$ready = '{"number":1,"labels":[{"name":"type:release"},{"name":"status:review"}],"milestone":{"title":"v1.6.0"}}'
$feature = '{"number":2,"labels":[{"name":"type:feature"}],"milestone":{"title":"v1.6.0"}}'
try {
    Test-GuardCase 'ready stable release' "[$ready]"
    Test-GuardCase 'version from build props' "[$ready]" -Version ''
    Test-GuardCase 'prerelease with retained acceptance gaps' "[$ready,$feature]" -Version '1.6.0-rc.1' -BuildVersion '1.6.0-rc.1' -Subject 'Release 1.6.0-rc.1'
    Test-GuardCase 'stable build metadata' "[$ready]" -Version '1.6.0+build-2' -BuildVersion '1.6.0+build-2' -Subject 'Release 1.6.0+build-2'
    Test-GuardCase 'wrong commit subject' "[$ready]" -Subject 'Update version' -ExpectedError 'Release commit subject must be*'
    Test-GuardCase 'version mismatch' "[$ready]" -Version '1.6.1' -ExpectedError 'Release version must match*'
    Test-GuardCase 'malformed version' "[$ready]" -Version 'invalid' -BuildVersion 'invalid' -ExpectedError 'Release version must be a semantic version*'
    Test-GuardCase 'empty build version' "[$ready]" -Version '' -BuildVersion '' -ExpectedError 'Release version must match*'
    Test-GuardCase 'missing tracker' '[]' -ExpectedError '*exactly one open release tracker*'
    Test-GuardCase 'feature is not release acceptance' "[$feature]" -ExpectedError '*exactly one open release tracker*'
    Test-GuardCase 'duplicate release trackers' "[$ready,$ready]" -ExpectedError '*exactly one open release tracker*'
    Test-GuardCase 'wrong milestone' ("[$ready]".Replace('v1.6.0', 'v1.7.0')) -ExpectedError '*tracker must match*'
    Test-GuardCase 'not accepted' ("[$ready]".Replace('status:review', 'status:in-progress')) -ExpectedError '*tracker must match*'
    Test-GuardCase 'conflicting status labels' ("[$ready]".Replace('{"name":"status:review"}', '{"name":"status:review"},{"name":"status:blocked"}')) -ExpectedError '*tracker must match*'
    Test-GuardCase 'stable milestone incomplete' "[$ready,$feature]" -ExpectedError 'Stable release acceptance requires*'
    Test-GuardCase 'GitHub command failure' "[$ready]" -ExitCode 1 -ExpectedError 'Could not verify release acceptance*'
    Test-GuardCase 'empty GitHub response' '' -ExpectedError 'GitHub returned no release acceptance data*'
    Test-GuardCase 'invalid GitHub JSON' 'invalid-json' -ExpectedError '*'
    Test-GuardCase 'truncated issue selection' ('[' + ((1..1000 | ForEach-Object { $ready }) -join ',') + ']') -ExpectedError '*exceeds the validation limit*'
    Write-Host "Release guard: $script:passed cases passed. No network calls or publication."
} finally {
    Remove-Item -LiteralPath $propsPath -ErrorAction SilentlyContinue
}
