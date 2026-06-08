param(
    [string]$PackageRoot = "artifacts/packages",
    [string]$ExecutableRoot = "artifacts/executables",
    [string[]]$ExecutableRuntimeIdentifiers = @("win-x64", "linux-x64"),
    [ValidateSet("framework-dependent", "self-contained")]
    [string]$ExecutablePackageKind = "framework-dependent",
    [string]$OutputPath = "artifacts/release-manifest.json"
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

function Get-RepoRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $repoPrefix = $RepoRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is not under the repository: $fullPath"
    }

    return $fullPath.Substring($repoPrefix.Length).Replace("\", "/")
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRootPath = Resolve-RepoPath -Path $PackageRoot -RepoRoot $repoRoot
$executableRootPath = Resolve-RepoPath -Path $ExecutableRoot -RepoRoot $repoRoot
$outputPathValue = Resolve-RepoPath -Path $OutputPath -RepoRoot $repoRoot

foreach ($path in @($packageRootPath, $executableRootPath, $outputPathValue)) {
    if (-not (Test-IsUnderDirectory -Path $path -Directory $repoRoot)) {
        throw "Artifact verification paths must stay inside the repository: $path"
    }
}

if (-not (Test-Path -LiteralPath $packageRootPath -PathType Container)) {
    throw "Package artifact directory does not exist: $packageRootPath"
}

if (-not (Test-Path -LiteralPath $executableRootPath -PathType Container)) {
    throw "Executable artifact directory does not exist: $executableRootPath"
}

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot "Directory.Build.props")
$version = $buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from Directory.Build.props."
}

$requiredArtifacts = @(
    [pscustomobject]@{
        Kind = "nuget-package"
        Path = Join-Path $packageRootPath "AvaScope.Protocol.$version.nupkg"
    },
    [pscustomobject]@{
        Kind = "nuget-package"
        Path = Join-Path $packageRootPath "AvaScope.Core.$version.nupkg"
    },
    [pscustomobject]@{
        Kind = "nuget-package"
        Path = Join-Path $packageRootPath "AvaScope.Bridge.$version.nupkg"
    }
)

foreach ($runtimeIdentifier in $ExecutableRuntimeIdentifiers) {
    if ([string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        throw "Executable runtime identifier cannot be empty."
    }

    if ($runtimeIdentifier -notmatch "^[A-Za-z0-9_.-]+$") {
        throw "Executable runtime identifier contains unsupported characters: $runtimeIdentifier"
    }

    $requiredArtifacts += [pscustomobject]@{
        Kind = "executable-zip"
        Path = Join-Path $executableRootPath "avascope-$runtimeIdentifier-$ExecutablePackageKind.zip"
    }
}

$expectedPackageNames = @{}
foreach ($artifact in $requiredArtifacts | Where-Object { $_.Kind -eq "nuget-package" }) {
    $expectedPackageNames[(Split-Path -Leaf $artifact.Path)] = $true
}

Get-ChildItem -LiteralPath $packageRootPath -Filter "AvaScope.*.nupkg" -File | ForEach-Object {
    if (-not $expectedPackageNames.ContainsKey($_.Name)) {
        throw "Unexpected AvaScope package artifact is not covered by the manifest: $($_.FullName)"
    }
}

$expectedExecutableZipNames = @{}
foreach ($artifact in $requiredArtifacts | Where-Object { $_.Kind -eq "executable-zip" }) {
    $expectedExecutableZipNames[(Split-Path -Leaf $artifact.Path)] = $true
}

Get-ChildItem -LiteralPath $executableRootPath -Filter "avascope-*.zip" -File | ForEach-Object {
    if (-not $expectedExecutableZipNames.ContainsKey($_.Name)) {
        throw "Unexpected executable ZIP artifact is not covered by the manifest: $($_.FullName)"
    }
}

$artifacts = foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath $artifact.Path -PathType Leaf)) {
        throw "Required artifact does not exist: $($artifact.Path)"
    }

    $file = Get-Item -LiteralPath $artifact.Path
    $hash = Get-FileHash -LiteralPath $artifact.Path -Algorithm SHA256
    [pscustomobject]@{
        kind = $artifact.Kind
        name = $file.Name
        relativePath = Get-RepoRelativePath -Path $file.FullName -RepoRoot $repoRoot
        sizeBytes = $file.Length
        sha256 = $hash.Hash.ToLowerInvariant()
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    product = "AvaScope"
    version = $version
    executablePackageKind = $ExecutablePackageKind
    artifacts = @($artifacts | Sort-Object kind, name)
}

$outputDirectory = Split-Path -Parent $outputPathValue
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$json = $manifest | ConvertTo-Json -Depth 5
Set-Content -LiteralPath $outputPathValue -Value $json -Encoding utf8

Write-Host "Verified $($manifest.artifacts.Count) release artifacts."
Write-Host "Wrote artifact manifest: $outputPathValue"
$manifest.artifacts | Format-Table -AutoSize
