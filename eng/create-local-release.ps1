param(
    [string]$Configuration = "Release",
    [Alias("RuntimeIdentifiers")]
    [string[]]$ExecutableRuntimeIdentifiers = @("win-x64", "linux-x64", "osx-arm64", "osx-x64"),
    [string[]]$InstallerRuntimeIdentifiers,
    [ValidateSet("framework-dependent", "self-contained")]
    [string]$ExecutablePackageKind = "framework-dependent",
    [switch]$SkipTests,
    [switch]$SkipSampleSmoke
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

function Remove-RepoItem {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [switch]$Recurse
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    if (-not (Test-IsUnderDirectory -Path $resolvedPath -Directory $RepoRoot)) {
        throw "Refusing to delete a path outside the repository: $resolvedPath"
    }

    if ($Recurse) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    } else {
        Remove-Item -LiteralPath $resolvedPath -Force
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ($ExecutableRuntimeIdentifiers.Count -eq 0) {
    throw "At least one executable runtime identifier is required."
}

if (-not $PSBoundParameters.ContainsKey("InstallerRuntimeIdentifiers")) {
    $InstallerRuntimeIdentifiers = @($ExecutableRuntimeIdentifiers | Where-Object {
        $_.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase) -or
        $_.StartsWith("linux-", [System.StringComparison]::OrdinalIgnoreCase)
    })
}

foreach ($runtimeIdentifier in @($ExecutableRuntimeIdentifiers) + @($InstallerRuntimeIdentifiers)) {
    if ([string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        throw "Runtime identifier cannot be empty."
    }

    if ($runtimeIdentifier -notmatch "^[A-Za-z0-9_.-]+$") {
        throw "Runtime identifier contains unsupported characters: $runtimeIdentifier"
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "AvaScope.slnx"
$packageRoot = Join-Path $repoRoot "artifacts/packages"
$executableRoot = Join-Path $repoRoot "artifacts/executables"
$sampleRoot = Join-Path $repoRoot "artifacts/samples"

Push-Location $repoRoot
try {
    Write-Host "Creating AvaScope local release from: $repoRoot"
    Write-Host "Configuration: $Configuration"
    Write-Host "Executable runtime identifiers: $($ExecutableRuntimeIdentifiers -join ', ')"
    Write-Host "Installer runtime identifiers: $($InstallerRuntimeIdentifiers -join ', ')"
    Write-Host "Executable package kind: $ExecutablePackageKind"

    Invoke-DotNet -Arguments @("restore", $solutionPath)
    Invoke-DotNet -Arguments @("build", $solutionPath, "-c", $Configuration, "--no-restore")

    if (-not $SkipTests) {
        Invoke-DotNet -Arguments @("test", $solutionPath, "-c", $Configuration, "--no-build")
    }

    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $packageRoot -Filter "AvaScope.*.nupkg" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-RepoItem -Path $_.FullName -RepoRoot $repoRoot
    }

    $packProjects = @(
        "src/AvaScope.Protocol/AvaScope.Protocol.csproj",
        "src/AvaScope.Core/AvaScope.Core.csproj",
        "src/AvaScope.Bridge/AvaScope.Bridge.csproj"
    )

    foreach ($project in $packProjects) {
        Invoke-DotNet -Arguments @(
            "pack",
            (Join-Path $repoRoot $project),
            "-c",
            $Configuration,
            "--no-build",
            "--output",
            $packageRoot)
    }

    & (Join-Path $repoRoot "eng/package-executables.ps1") `
        -Configuration $Configuration `
        -RuntimeIdentifiers $ExecutableRuntimeIdentifiers `
        -PackageKind $ExecutablePackageKind
    if ($LASTEXITCODE -ne 0) {
        throw "eng/package-executables.ps1 failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $repoRoot "eng/package-installers.ps1") `
        -Configuration $Configuration `
        -RuntimeIdentifiers $InstallerRuntimeIdentifiers `
        -ExecutablePackageKind $ExecutablePackageKind
    if ($LASTEXITCODE -ne 0) {
        throw "eng/package-installers.ps1 failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $repoRoot "eng/verify-artifacts.ps1") `
        -ExecutableRuntimeIdentifiers $ExecutableRuntimeIdentifiers `
        -InstallerRuntimeIdentifiers $InstallerRuntimeIdentifiers `
        -ExecutablePackageKind $ExecutablePackageKind
    if ($LASTEXITCODE -ne 0) {
        throw "eng/verify-artifacts.ps1 failed with exit code $LASTEXITCODE."
    }

    $winRuntimeIdentifier = $InstallerRuntimeIdentifiers |
        Where-Object { $_.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1
    if ($winRuntimeIdentifier) {
        $releaseDirectory = Join-Path $executableRoot "avascope-$winRuntimeIdentifier-$ExecutablePackageKind"
        $releaseExe = Join-Path $releaseDirectory "avascope.exe"
        $installerPath = Join-Path $executableRoot "AvaScopeSetup.exe"
        if (-not (Test-Path -LiteralPath $releaseExe -PathType Leaf)) {
            throw "Packaged Windows avascope.exe was not produced: $releaseExe"
        }
        if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
            throw "Packaged Windows installer was not produced: $installerPath"
        }

        $previousInstallerArtifact = $env:AVASCOPE_INSTALLER_ARTIFACT
        try {
            $env:AVASCOPE_INSTALLER_ARTIFACT = $installerPath
            Invoke-DotNet -Arguments @(
                "test",
                $solutionPath,
                "-c",
                $Configuration,
                "--no-build",
                "--filter",
                "FullyQualifiedName~PackagedInstallerSupportsInstallRepairDoctorMcpAndUninstall")
        }
        finally {
            $env:AVASCOPE_INSTALLER_ARTIFACT = $previousInstallerArtifact
        }

        $doctorRoot = Join-Path $sampleRoot "doctor-smoke"
        Remove-RepoItem -Path $doctorRoot -RepoRoot $repoRoot
        & $releaseExe doctor `
            --manifest-dir (Join-Path $doctorRoot "sessions") `
            --preview-session-store (Join-Path $doctorRoot "preview-sessions")
        if ($LASTEXITCODE -ne 0) {
            throw "Packaged Windows doctor smoke test failed with exit code $LASTEXITCODE."
        }

        if (-not $SkipSampleSmoke) {
            New-Item -ItemType Directory -Path $sampleRoot -Force | Out-Null
            $sampleOutputPath = Join-Path $sampleRoot "getting-started-preview-release.png"
            Remove-RepoItem -Path $sampleOutputPath -RepoRoot $repoRoot

            & $releaseExe preview `
                (Join-Path $repoRoot "samples/AvaScope.GettingStartedApp/AvaScope.GettingStartedApp.csproj") `
                --view "Views/MainView.axaml" `
                --out $sampleOutputPath `
                --width 720 `
                --height 420 `
                --theme light `
                --design-data-type "AvaScope.GettingStartedApp.SamplePreviewData"

            if ($LASTEXITCODE -ne 0) {
                throw "Packaged Windows preview smoke test failed with exit code $LASTEXITCODE."
            }

            if (-not (Test-Path -LiteralPath $sampleOutputPath -PathType Leaf)) {
                throw "Packaged Windows preview smoke test did not create output: $sampleOutputPath"
            }
        }

        Write-Host ""
        Write-Host "Local release is ready."
        Write-Host "Use this executable for external project testing:"
        Write-Host $releaseExe
    } else {
        Write-Host ""
        Write-Host "Local release is ready. No Windows runtime identifier was requested, so no avascope.exe path is available."
    }

    Write-Host "Manifest:"
    Write-Host (Join-Path $repoRoot "artifacts/release-manifest.json")
}
finally {
    Pop-Location
}
