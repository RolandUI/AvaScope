param(
    [string]$Version = "",
    [string]$CommitSubject = "",
    [string]$Repository = "RolandUI/AvaScope",
    [string]$BuildPropsPath = "Directory.Build.props"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$buildPropsFullPath = if ([System.IO.Path]::IsPathRooted($BuildPropsPath)) {
    $BuildPropsPath
} else {
    Join-Path $repoRoot $BuildPropsPath
}

if (-not (Test-Path -LiteralPath $buildPropsFullPath -PathType Leaf)) {
    throw "Build props file does not exist."
}

[xml]$buildProps = Get-Content -LiteralPath $buildPropsFullPath
$buildVersion = [string]$buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $buildVersion
}

if ([string]::IsNullOrWhiteSpace($Version) -or $Version -ne $buildVersion) {
    throw "Release version must match Directory.Build.props."
}
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
    throw "Release version must be a semantic version."
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

$milestone = 'v' + ($Version -split '[-+]', 2)[0]
$isPrerelease = ($Version -split '\+', 2)[0].Contains('-')
$issueJson = & gh issue list --repo $Repository --milestone $milestone --state open --limit 1000 --json 'number,labels,milestone'
if ($LASTEXITCODE -ne 0) {
    throw "Could not verify release acceptance on GitHub."
}
if ([string]::IsNullOrWhiteSpace(($issueJson | Out-String))) {
    throw "GitHub returned no release acceptance data."
}

$issues = @(($issueJson | Out-String | ConvertFrom-Json) | Where-Object { $null -ne $_ })
if ($issues.Count -ge 1000) {
    throw "The release milestone exceeds the validation limit."
}
$trackers = @($issues | Where-Object { @($_.labels | ForEach-Object { $_.name }) -contains 'type:release' })
if ($trackers.Count -ne 1) {
    throw "The release milestone must have exactly one open release tracker."
}
$tracker = $trackers[0]
$labels = @($tracker.labels | ForEach-Object { $_.name })
$statusLabels = @($labels | Where-Object { $_ -like 'status:*' })
if ($tracker.milestone.title -ne $milestone -or
    $labels -notcontains 'type:release' -or
    $statusLabels.Count -ne 1 -or $statusLabels[0] -ne 'status:review') {
    throw "The release tracker must match the version and have type:release and only status:review."
}
if (-not $isPrerelease -and $issues.Count -ne 1) {
    throw "Stable release acceptance requires all other milestone issues to be closed or moved."
}

Write-Host "Release commit validated."
Write-Host "Version: $Version"
Write-Host "Commit subject: $CommitSubject"
Write-Host "Release acceptance: $Repository#$($tracker.number)"
