param(
    [string]$Version = "",
    [string]$CommitSubject = "",
    [string]$RequiredState = "Release Candidate",
    [string]$PlanPath = "docs/RELEASE_PLAN.md",
    [string]$BuildPropsPath = "Directory.Build.props"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$planFullPath = Join-Path $repoRoot $PlanPath
$buildPropsFullPath = Join-Path $repoRoot $BuildPropsPath

if (-not (Test-Path -LiteralPath $planFullPath -PathType Leaf)) {
    throw "Release plan does not exist: $planFullPath"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (-not (Test-Path -LiteralPath $buildPropsFullPath -PathType Leaf)) {
        throw "Build props file does not exist: $buildPropsFullPath"
    }

    [xml]$buildProps = Get-Content -LiteralPath $buildPropsFullPath
    $Version = [string]$buildProps.Project.PropertyGroup.Version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "A release version is required."
}

if ([string]::IsNullOrWhiteSpace($CommitSubject)) {
    $gitSubject = git -C $repoRoot log -1 --pretty=%s
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read the current git commit subject."
    }

    $CommitSubject = ($gitSubject | Out-String).Trim()
}

$expectedSubject = "Release $Version"
if ($CommitSubject -ne $expectedSubject) {
    throw "Release commit subject must be '$expectedSubject'. Actual subject: '$CommitSubject'."
}

$plan = Get-Content -LiteralPath $planFullPath -Raw
$currentTargetMatch = [regex]::Match(
    $plan,
    "(?ms)^## Current Release Target\s*(?<section>.*?)(?=^## |\z)")
if (-not $currentTargetMatch.Success) {
    throw "Release plan must contain a '## Current Release Target' section."
}

$currentTarget = $currentTargetMatch.Groups["section"].Value
$versionPattern = 'Target Version:\s*`?' + [regex]::Escape($Version) + '`?'
if ($currentTarget -notmatch $versionPattern) {
    throw "Release plan must declare Target Version '$Version'."
}

$statePattern = 'Release State:\s*`?' + [regex]::Escape($RequiredState) + '`?'
if ($currentTarget -notmatch $statePattern) {
    throw "Release plan must be in '$RequiredState' state before this release action."
}

Write-Host "Release commit validated."
Write-Host "Version: $Version"
Write-Host "Commit subject: $CommitSubject"
Write-Host "Required release state: $RequiredState"
