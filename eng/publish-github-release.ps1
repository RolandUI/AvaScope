param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    [string]$PackageRoot = "artifacts/packages",
    [string]$ExecutableRoot = "artifacts/executables",
    [string]$ManifestPath = "artifacts/release-manifest.json",
    [string[]]$ExecutableRuntimeIdentifiers = @("win-x64", "linux-x64"),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Test-IsUnderDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)

    return $fullPath.Equals($fullDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith(
            $fullDirectory + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Path))
}

function Invoke-GitHubCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    throw "Tag cannot be empty."
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRootPath = Resolve-RepoPath -Path $PackageRoot -RepoRoot $repoRoot
$executableRootPath = Resolve-RepoPath -Path $ExecutableRoot -RepoRoot $repoRoot
$manifestPathValue = Resolve-RepoPath -Path $ManifestPath -RepoRoot $repoRoot

foreach ($path in @($packageRootPath, $executableRootPath, $manifestPathValue)) {
    if (-not (Test-IsUnderDirectory -Path $path -Directory $repoRoot)) {
        throw "Release artifact paths must stay inside the repository: $path"
    }
}

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot "Directory.Build.props")
$version = [string]$buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from Directory.Build.props."
}

$expectedTag = "v$version"
if ($Tag -ne $expectedTag) {
    throw "Release tag '$Tag' does not match package version '$version'. Expected tag '$expectedTag'."
}

$assetPaths = @(
    (Join-Path $packageRootPath "AvaScope.Protocol.$version.nupkg"),
    (Join-Path $packageRootPath "AvaScope.Core.$version.nupkg"),
    (Join-Path $packageRootPath "AvaScope.Bridge.$version.nupkg")
)

foreach ($runtimeIdentifier in $ExecutableRuntimeIdentifiers) {
    if ([string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        throw "Executable runtime identifier cannot be empty."
    }

    if ($runtimeIdentifier -notmatch "^[A-Za-z0-9_.-]+$") {
        throw "Executable runtime identifier contains unsupported characters: $runtimeIdentifier"
    }

    $assetPaths += Join-Path $executableRootPath "avascope-$runtimeIdentifier-framework-dependent.zip"
}

$assetPaths += $manifestPathValue

foreach ($assetPath in $assetPaths) {
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Required GitHub Release asset does not exist: $assetPath. Run eng/create-local-release.ps1 first."
    }
}

Write-Host "GitHub Release tag: $Tag"
Write-Host "Version: $version"
Write-Host "Assets:"
$assetPaths | ForEach-Object {
    Write-Host "  $_"
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run complete. No GitHub Release was created or updated."
    return
}

if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN) -and [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    throw "Set GH_TOKEN or GITHUB_TOKEN before creating or updating a GitHub Release."
}

& gh release view $Tag *> $null
$releaseExists = $LASTEXITCODE -eq 0

if ($releaseExists) {
    Write-Host ""
    Write-Host "GitHub Release already exists. Uploading assets with --clobber: $Tag"
    $arguments = @("release", "upload", $Tag) + $assetPaths + @("--clobber")
    Invoke-GitHubCli -Arguments $arguments
} else {
    Write-Host ""
    Write-Host "Creating GitHub Release: $Tag"
    $arguments = @(
        "release",
        "create",
        $Tag
    ) + $assetPaths + @(
        "--title",
        "AvaScope $version",
        "--generate-notes",
        "--verify-tag"
    )

    Invoke-GitHubCli -Arguments $arguments
}

Write-Host ""
Write-Host "GitHub Release publish completed."
