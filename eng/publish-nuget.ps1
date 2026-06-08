param(
    [string]$PackageRoot = "artifacts/packages",
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey,
    [int]$TimeoutSeconds = 300,
    [switch]$SkipDuplicate,
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

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        $safeArguments = @()
        $maskNext = $false
        foreach ($argument in $Arguments) {
            if ($maskNext) {
                $safeArguments += "***"
                $maskNext = $false
                continue
            }

            $safeArguments += $argument
            if ($argument -eq "--api-key" -or $argument -eq "-k" -or $argument -eq "--symbol-api-key" -or $argument -eq "-sk") {
                $maskNext = $true
            }
        }

        throw "dotnet $($safeArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ([string]::IsNullOrWhiteSpace($Source)) {
    throw "NuGet source cannot be empty."
}

if ($TimeoutSeconds -le 0) {
    throw "TimeoutSeconds must be greater than zero."
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = $env:AVASCOPE_NUGET_API_KEY
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $ApiKey = $env:NUGET_API_KEY
}

if (-not $DryRun -and [string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "Set AVASCOPE_NUGET_API_KEY or pass -ApiKey before publishing."
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRootPath = Resolve-RepoPath -Path $PackageRoot -RepoRoot $repoRoot

if (-not (Test-IsUnderDirectory -Path $packageRootPath -Directory $repoRoot)) {
    throw "PackageRoot must stay inside the repository: $packageRootPath"
}

if (-not (Test-Path -LiteralPath $packageRootPath -PathType Container)) {
    throw "Package artifact directory does not exist: $packageRootPath. Run eng/create-local-release.ps1 first."
}

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot "Directory.Build.props")
$version = [string]$buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from Directory.Build.props."
}

$expectedPackageNames = @(
    "AvaScope.Protocol.$version.nupkg",
    "AvaScope.Core.$version.nupkg",
    "AvaScope.Bridge.$version.nupkg"
)

$expectedPackages = foreach ($packageName in $expectedPackageNames) {
    $packagePath = Join-Path $packageRootPath $packageName
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Required package artifact does not exist: $packagePath. Run eng/create-local-release.ps1 first."
    }

    Get-Item -LiteralPath $packagePath
}

$expectedPackageNameSet = @{}
foreach ($packageName in $expectedPackageNames) {
    $expectedPackageNameSet[$packageName] = $true
}

Get-ChildItem -LiteralPath $packageRootPath -Filter "AvaScope.*.nupkg" -File | ForEach-Object {
    if (-not $expectedPackageNameSet.ContainsKey($_.Name)) {
        throw "Unexpected AvaScope package artifact found: $($_.FullName). Remove stale artifacts or update this script intentionally."
    }
}

Write-Host "NuGet source: $Source"
Write-Host "Version: $version"
Write-Host "Packages:"
$expectedPackages | ForEach-Object {
    Write-Host "  $($_.FullName)"
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run complete. No packages were pushed."
    return
}

foreach ($package in $expectedPackages) {
    $arguments = @(
        "nuget",
        "push",
        $package.FullName,
        "--source",
        $Source,
        "--api-key",
        $ApiKey,
        "--no-symbols",
        "--timeout",
        $TimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--force-english-output"
    )

    if ($SkipDuplicate) {
        $arguments += "--skip-duplicate"
    }

    Write-Host ""
    Write-Host "Pushing $($package.Name)..."
    Invoke-DotNet -Arguments $arguments
}

Write-Host ""
Write-Host "NuGet publish completed."
